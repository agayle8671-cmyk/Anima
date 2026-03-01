using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace akimate.Services;

/// <summary>
/// Manages the Blender headless process lifecycle and provides
/// a TCP socket client for sending commands and receiving responses.
/// 
/// GPL INSULATION: All communication is via TCP sockets on localhost.
/// Zero shared memory or linking between C# and Blender/Python code.
/// </summary>
public sealed class BlenderService : IDisposable
{
    // ── Configuration ──────────────────────────────────────────────────
    private const string Host = "127.0.0.1";
    private const int Port = 9700;
    private const int ConnectTimeoutMs = 45_000;  // 45s for cold start
    private const int CommandTimeoutMs = 120_000;

    // ── State ──────────────────────────────────────────────────────────
    private Process? _blenderProcess;
    private TcpClient? _tcpClient;
    private NetworkStream? _stream;
    private string _blenderPath = "";
    private string _daemonScriptPath = "";
    private string _readyFilePath = "";
    private string _renderDir = "";
    private bool _disposed;

    // ── Events ─────────────────────────────────────────────────────────
    public event EventHandler<string>? LogMessage;
    public event EventHandler? BlenderStarted;
    public event EventHandler? BlenderStopped;
    public event EventHandler<string>? BlenderCrashed;

    /// <summary>Whether Blender is currently running and connected.</summary>
    public bool IsConnected => _tcpClient?.Connected == true && _blenderProcess?.HasExited == false;

    /// <summary>The render output directory for the current session.</summary>
    public string RenderDirectory => _renderDir;

    // ── Lifecycle ──────────────────────────────────────────────────────

    /// <summary>
    /// Starts Blender in headless mode with the daemon script.
    /// Waits for the ready signal, then establishes a TCP connection.
    /// </summary>
    public async Task StartAsync(string blenderExePath, CancellationToken ct = default)
    {
        _blenderPath = blenderExePath;

        // Locate the daemon script next to the exe
        var appDir = AppContext.BaseDirectory;
        _daemonScriptPath = Path.Combine(appDir, "Blender", "daemon.py");
        if (!File.Exists(_daemonScriptPath))
        {
            throw new FileNotFoundException($"Blender daemon script not found at: {_daemonScriptPath}");
        }

        // Set up paths
        var sessionDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".akimate");
        _renderDir = Path.Combine(sessionDir, "renders");
        _readyFilePath = Path.Combine(sessionDir, "blender_ready.signal");
        Directory.CreateDirectory(sessionDir);
        Directory.CreateDirectory(_renderDir);

        // Delete any stale ready signal
        if (File.Exists(_readyFilePath))
            File.Delete(_readyFilePath);

        // Kill any stale Blender daemon processes using our port
        KillStaleBlenderDaemons();

        Log("Starting Blender daemon...");
        Log($"  Executable: {_blenderPath}");
        Log($"  Script: {_daemonScriptPath}");
        Log($"  Port: {Port}");

        // Launch Blender headless
        var startInfo = new ProcessStartInfo
        {
            FileName = _blenderPath,
            Arguments = $"--background --python \"{_daemonScriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            // IMPORTANT: Do NOT use the Environment initializer — it replaces the
            // entire environment, stripping PATH and system vars Blender needs.
            // Use EnvironmentVariables to ADD vars on top of the inherited env.
        };

        // Add our vars to the inherited environment
        startInfo.EnvironmentVariables["AKIMATE_PORT"] = Port.ToString();
        startInfo.EnvironmentVariables["AKIMATE_RENDER_DIR"] = _renderDir;
        startInfo.EnvironmentVariables["AKIMATE_READY_FILE"] = _readyFilePath;

        _blenderProcess = Process.Start(startInfo);
        if (_blenderProcess == null)
        {
            throw new InvalidOperationException("Failed to start Blender process.");
        }

        // Read stdout/stderr in background
        _ = Task.Run(() => ReadProcessOutput(_blenderProcess.StandardOutput, "stdout"), ct);
        _ = Task.Run(() => ReadProcessOutput(_blenderProcess.StandardError, "stderr"), ct);

        // Monitor for crashes
        _ = Task.Run(() => MonitorProcess(), ct);

        Log($"Blender process started (PID: {_blenderProcess.Id})");

        // Wait for ready signal file
        var deadline = DateTime.UtcNow.AddMilliseconds(ConnectTimeoutMs);
        while (!File.Exists(_readyFilePath))
        {
            if (DateTime.UtcNow > deadline)
                throw new TimeoutException("Blender daemon did not become ready within timeout.");
            if (_blenderProcess.HasExited)
                throw new InvalidOperationException($"Blender process exited with code {_blenderProcess.ExitCode} before becoming ready.");
            await Task.Delay(200, ct);
        }

        Log("Blender daemon is ready — connecting TCP...");

        // Connect TCP
        _tcpClient = new TcpClient();
        await _tcpClient.ConnectAsync(Host, Port, ct);
        _stream = _tcpClient.GetStream();

        Log("TCP connection established.");
        BlenderStarted?.Invoke(this, EventArgs.Empty);

        // Verify with ping
        var pingResult = await SendCommandAsync("ping", new { }, ct);
        Log($"Ping response: Blender {pingResult.GetProperty("result").GetProperty("blender_version").GetString()}");
    }

    /// <summary>
    /// Gracefully shuts down the Blender process and closes the TCP connection.
    /// </summary>
    public async Task StopAsync()
    {
        Log("Stopping Blender daemon...");

        _stream?.Dispose();
        _stream = null;

        _tcpClient?.Dispose();
        _tcpClient = null;

        if (_blenderProcess != null && !_blenderProcess.HasExited)
        {
            try
            {
                _blenderProcess.Kill(entireProcessTree: true);
                await _blenderProcess.WaitForExitAsync();
            }
            catch { /* Process may already be gone */ }
        }

        _blenderProcess?.Dispose();
        _blenderProcess = null;

        // Clean up ready signal
        if (File.Exists(_readyFilePath))
            File.Delete(_readyFilePath);

        Log("Blender daemon stopped.");
        BlenderStopped?.Invoke(this, EventArgs.Empty);
    }

    // ── Command Protocol ───────────────────────────────────────────────

    /// <summary>
    /// Sends a JSON command to Blender and waits for the response.
    /// </summary>
    public async Task<JsonElement> SendCommandAsync(string command, object? parameters = null, CancellationToken ct = default)
    {
        if (_stream == null)
            throw new InvalidOperationException("Not connected to Blender.");

        var request = new
        {
            command,
            @params = parameters ?? new { },
            request_id = Guid.NewGuid().ToString()
        };

        var json = JsonSerializer.Serialize(request);
        var payload = Encoding.UTF8.GetBytes(json);

        // Send length-prefixed message
        var lenBytes = BitConverter.GetBytes((uint)payload.Length);
        if (BitConverter.IsLittleEndian)
            Array.Reverse(lenBytes); // Network byte order (big-endian)

        await _stream.WriteAsync(lenBytes, ct);
        await _stream.WriteAsync(payload, ct);
        await _stream.FlushAsync(ct);

        // Read response — use caller's token if provided, otherwise apply default 2-min timeout
        CancellationToken readToken;
        CancellationTokenSource? ownedCts = null;
        if (ct == CancellationToken.None)
        {
            ownedCts = new CancellationTokenSource(CommandTimeoutMs);
            readToken = ownedCts.Token;
        }
        else
        {
            readToken = ct; // Caller controls the timeout (e.g. 20 min for animation render)
        }

        try
        {
            var responseLenBytes = await ReadExactAsync(_stream, 4, readToken);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(responseLenBytes);
            var responseLen = (int)BitConverter.ToUInt32(responseLenBytes);

            var responsePayload = await ReadExactAsync(_stream, responseLen, readToken);
            var responseJson = Encoding.UTF8.GetString(responsePayload);

            var doc = JsonDocument.Parse(responseJson);
            var root = doc.RootElement;

            if (root.TryGetProperty("status", out var status) && status.GetString() == "error")
            {
                var error = root.TryGetProperty("error", out var errProp) ? errProp.GetString() : "Unknown error";
                Log($"Command '{command}' failed: {error}");
            }

            return root;
        }
        finally
        {
            ownedCts?.Dispose();
        }
    } // end SendCommandAsync

    // ── Convenience Methods ────────────────────────────────────────────

    /// <summary>Send a ping to verify the connection is alive.</summary>
    public async Task<JsonElement> PingAsync(CancellationToken ct = default)
        => await SendCommandAsync("ping", null, ct);

    /// <summary>Get current scene information.</summary>
    public async Task<JsonElement> GetSceneInfoAsync(CancellationToken ct = default)
        => await SendCommandAsync("scene_info", null, ct);

    /// <summary>Create a primitive object.</summary>
    public async Task<JsonElement> CreateObjectAsync(string type, double[] location, string name = "", CancellationToken ct = default)
        => await SendCommandAsync("create_object", new { type, location, name }, ct);

    /// <summary>Delete an object by name.</summary>
    public async Task<JsonElement> DeleteObjectAsync(string name, CancellationToken ct = default)
        => await SendCommandAsync("delete_object", new { name }, ct);

    /// <summary>Set an object's transform.</summary>
    public async Task<JsonElement> SetTransformAsync(string name, double[]? location = null, double[]? rotation = null, double[]? scale = null, CancellationToken ct = default)
        => await SendCommandAsync("set_transform", new { name, location, rotation, scale }, ct);

    /// <summary>Insert a keyframe with optional stepped interpolation.</summary>
    public async Task<JsonElement> SetKeyframeAsync(string name, int frame, string dataPath = "location", string interpolation = "CONSTANT", CancellationToken ct = default)
        => await SendCommandAsync("set_keyframe", new { name, frame, data_path = dataPath, interpolation }, ct);

    /// <summary>Render a single frame and return the file path.</summary>
    public async Task<JsonElement> RenderFrameAsync(int frame = 1, int resX = 1920, int resY = 1080, string engine = "BLENDER_EEVEE_NEXT", CancellationToken ct = default)
        => await SendCommandAsync("render_frame", new { frame, resolution_x = resX, resolution_y = resY, engine }, ct);

    /// <summary>Execute arbitrary Python code in Blender.</summary>
    public async Task<JsonElement> ExecutePythonAsync(string code, CancellationToken ct = default)
        => await SendCommandAsync("execute_python", new { code }, ct);

    // ── Private Helpers ────────────────────────────────────────────────

    private static async Task<byte[]> ReadExactAsync(NetworkStream stream, int count, CancellationToken ct)
    {
        var buffer = new byte[count];
        var offset = 0;
        while (offset < count)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, count - offset), ct);
            if (read == 0)
                throw new IOException("Connection closed while reading.");
            offset += read;
        }
        return buffer;
    }

    private void ReadProcessOutput(StreamReader reader, string label)
    {
        try
        {
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (line != null)
                {
                    Log($"[Blender {label}] {line}");
                }
            }
        }
        catch { /* Process exited */ }
    }

    private async void MonitorProcess()
    {
        if (_blenderProcess == null) return;

        try
        {
            await _blenderProcess.WaitForExitAsync();
            if (!_disposed)
            {
                var exitCode = _blenderProcess.ExitCode;
                Log($"Blender process exited with code {exitCode}");
                BlenderCrashed?.Invoke(this, $"Blender exited with code {exitCode}");
            }
        }
        catch { /* Expected during shutdown */ }
    }

    /// <summary>
    /// Kills any stale Blender processes that might be holding port 9700.
    /// This prevents "address already in use" errors on restart.
    /// </summary>
    private void KillStaleBlenderDaemons()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("blender"))
            {
                // Skip our own managed process
                if (_blenderProcess != null && proc.Id == _blenderProcess.Id)
                    continue;

                try
                {
                    // Only kill headless Blender instances (no main window)
                    if (proc.MainWindowHandle == IntPtr.Zero)
                    {
                        Log($"Killing stale Blender process (PID: {proc.Id})");
                        proc.Kill(entireProcessTree: true);
                        proc.WaitForExit(3000);
                    }
                }
                catch { /* Process may already be gone */ }
            }

            // Brief pause to let the OS release the port
            System.Threading.Thread.Sleep(500);
        }
        catch { /* No processes to kill */ }
    }

    private void Log(string message)
    {
        Debug.WriteLine($"[BlenderService] {message}");
        LogMessage?.Invoke(this, message);
    }

    // ── IDisposable ────────────────────────────────────────────────────

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _stream?.Dispose();
        _tcpClient?.Dispose();

        if (_blenderProcess != null && !_blenderProcess.HasExited)
        {
            try { _blenderProcess.Kill(entireProcessTree: true); } catch { }
        }

        _blenderProcess?.Dispose();

        if (File.Exists(_readyFilePath))
            File.Delete(_readyFilePath);
    }
}
