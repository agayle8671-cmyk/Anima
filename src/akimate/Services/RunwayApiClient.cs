using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace akimate.Services;

/// <summary>
/// Client for the Runway Gen-3 Alpha API.
/// Handles image-to-video and text-to-video generation with async polling.
/// </summary>
public sealed class RunwayApiClient : IDisposable
{
    private const string BaseUrl = "https://api.dev.runwayml.com/v1";
    private readonly HttpClient _http;
    private bool _disposed;

    public event EventHandler<string>? LogMessage;

    public RunwayApiClient(string apiKey)
    {
        _http = new HttpClient
        {
            BaseAddress = new Uri(BaseUrl),
            Timeout = TimeSpan.FromMinutes(5)
        };
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        _http.DefaultRequestHeaders.Add("X-Runway-Version", "2024-11-06");
    }

    /// <summary>
    /// Generate a video from a text prompt using Runway Gen-3.
    /// </summary>
    public async Task<RunwayGenerationResult> GenerateFromTextAsync(
        string prompt,
        int durationSeconds = 5,
        string ratio = "16:9",
        CancellationToken ct = default)
    {
        var request = new
        {
            promptText = prompt,
            model = "gen3a_turbo",
            duration = durationSeconds,
            ratio,
            watermark = false
        };

        return await SubmitAndPollAsync("image_to_video", request, ct);
    }

    /// <summary>
    /// Generate a video from a reference image + text prompt.
    /// </summary>
    public async Task<RunwayGenerationResult> GenerateFromImageAsync(
        string imageUrl,
        string prompt,
        int durationSeconds = 5,
        string ratio = "16:9",
        CancellationToken ct = default)
    {
        var request = new
        {
            promptImage = imageUrl,
            promptText = prompt,
            model = "gen3a_turbo",
            duration = durationSeconds,
            ratio,
            watermark = false
        };

        return await SubmitAndPollAsync("image_to_video", request, ct);
    }

    /// <summary>
    /// Generate a video from a local image file + text prompt.
    /// First uploads the image to a data URL, then submits the generation.
    /// </summary>
    public async Task<RunwayGenerationResult> GenerateFromImageFileAsync(
        string localImagePath,
        string prompt,
        int durationSeconds = 5,
        string ratio = "16:9",
        CancellationToken ct = default)
    {
        var bytes = await File.ReadAllBytesAsync(localImagePath, ct);
        var ext = Path.GetExtension(localImagePath).ToLower().TrimStart('.');
        var mime = ext switch
        {
            "jpg" or "jpeg" => "image/jpeg",
            "png" => "image/png",
            "webp" => "image/webp",
            _ => "image/png"
        };
        var dataUrl = $"data:{mime};base64,{Convert.ToBase64String(bytes)}";
        return await GenerateFromImageAsync(dataUrl, prompt, durationSeconds, ratio, ct);
    }

    private async Task<RunwayGenerationResult> SubmitAndPollAsync(string endpoint, object request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Log($"Submitting generation to /{endpoint}...");
        var response = await _http.PostAsync(endpoint, content, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            Log($"Runway API error ({response.StatusCode}): {responseJson}");
            return new RunwayGenerationResult
            {
                Status = "error",
                Error = $"HTTP {response.StatusCode}: {responseJson}"
            };
        }

        var submitResult = JsonSerializer.Deserialize<RunwaySubmitResponse>(responseJson);
        if (submitResult?.Id == null)
        {
            return new RunwayGenerationResult { Status = "error", Error = "No task ID returned" };
        }

        Log($"Task submitted: {submitResult.Id}");

        // Poll for completion
        return await PollForCompletionAsync(submitResult.Id, ct);
    }

    private async Task<RunwayGenerationResult> PollForCompletionAsync(string taskId, CancellationToken ct)
    {
        var maxAttempts = 120; // 10 minutes at 5-second intervals
        for (var i = 0; i < maxAttempts; i++)
        {
            await Task.Delay(5000, ct);

            var response = await _http.GetAsync($"tasks/{taskId}", ct);
            var json = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                Log($"Poll error ({response.StatusCode}): {json}");
                continue;
            }

            var result = JsonSerializer.Deserialize<RunwayTaskResponse>(json);
            if (result == null) continue;

            Log($"Task {taskId}: status={result.Status} progress={result.Progress}");

            switch (result.Status)
            {
                case "SUCCEEDED":
                    return new RunwayGenerationResult
                    {
                        Status = "ok",
                        VideoUrl = result.Output?.FirstOrDefault(),
                        TaskId = taskId
                    };
                case "FAILED":
                    return new RunwayGenerationResult
                    {
                        Status = "error",
                        Error = result.Failure ?? "Generation failed",
                        TaskId = taskId
                    };
                case "CANCELLED":
                    return new RunwayGenerationResult
                    {
                        Status = "cancelled",
                        TaskId = taskId
                    };
            }
        }

        return new RunwayGenerationResult { Status = "timeout", TaskId = taskId };
    }

    /// <summary>
    /// Download a generated video to a local file.
    /// </summary>
    public async Task<string> DownloadVideoAsync(string videoUrl, string outputDir, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var filename = $"runway_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
        var filepath = Path.Combine(outputDir, filename);

        using var response = await _http.GetAsync(videoUrl, ct);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        await File.WriteAllBytesAsync(filepath, bytes, ct);

        Log($"Video downloaded: {filepath} ({bytes.Length / 1024}KB)");
        return filepath;
    }

    private void Log(string msg) => LogMessage?.Invoke(this, msg);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _http.Dispose();
    }
}

// ── Response Models ──────────────────────────────────────────────────

public class RunwaySubmitResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }
}

public class RunwayTaskResponse
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("progress")]
    public float Progress { get; set; }

    [JsonPropertyName("failure")]
    public string? Failure { get; set; }

    [JsonPropertyName("output")]
    public string[]? Output { get; set; }
}

public class RunwayGenerationResult
{
    public string Status { get; set; } = "";
    public string? VideoUrl { get; set; }
    public string? Error { get; set; }
    public string? TaskId { get; set; }
}
