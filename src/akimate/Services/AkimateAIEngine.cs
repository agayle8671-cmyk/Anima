using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace akimate.Services;

/// <summary>
/// Central AI engine that manages the Semantic Kernel instance,
/// registered services, and agent orchestration.
/// Supports both Cloud API (OpenAI-compatible) and local inference modes.
/// </summary>
public sealed class AkimateAIEngine : IDisposable
{
    private Kernel? _kernel;
    private bool _disposed;

    public event EventHandler<string>? LogMessage;

    /// <summary>Whether the AI engine is initialized and ready.</summary>
    public bool IsReady => _kernel != null;

    /// <summary>The current inference mode.</summary>
    public string InferenceMode { get; private set; } = "cloud";

    /// <summary>The current active Kernel instance.</summary>
    public Kernel? Kernel => _kernel;

    /// <summary>
    /// Initialize the AI engine with cloud API keys.
    /// Uses OpenAI-compatible endpoints (works with OpenAI, Azure, or local proxies).
    /// </summary>
    public void InitializeCloud(string apiKey, string model = "gpt-4o-mini", string? endpoint = null)
    {
        var builder = Kernel.CreateBuilder();

        if (!string.IsNullOrEmpty(endpoint))
        {
            // Custom endpoint (Azure, local proxy, etc.)
            builder.AddOpenAIChatCompletion(
                modelId: model,
                apiKey: apiKey,
                httpClient: new System.Net.Http.HttpClient { BaseAddress = new Uri(endpoint) });
        }
        else
        {
            // Standard OpenAI
            builder.AddOpenAIChatCompletion(
                modelId: model,
                apiKey: apiKey);
        }

        _kernel = builder.Build();
        InferenceMode = "cloud";
        Log($"AI Engine initialized (cloud: {model})");
    }

    /// <summary>
    /// Initialize the AI engine with a local ONNX/Ollama endpoint.
    /// Points to a local OpenAI-compatible server (Ollama, LM Studio, etc.)
    /// </summary>
    public void InitializeLocal(string localEndpoint = "http://localhost:11434/v1", string model = "qwen2.5:7b")
    {
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: model,
            apiKey: "local",  // Ollama doesn't need a real key
            httpClient: new System.Net.Http.HttpClient { BaseAddress = new Uri(localEndpoint) });

        _kernel = builder.Build();
        InferenceMode = "local";
        Log($"AI Engine initialized (local: {model} @ {localEndpoint})");
    }

    /// <summary>
    /// Send a prompt to the LLM and get a text response.
    /// This is the core method used by all agents.
    /// </summary>
    public async Task<string> ChatAsync(string systemPrompt, string userMessage, CancellationToken ct = default)
    {
        if (_kernel == null)
            throw new InvalidOperationException("AI Engine not initialized. Call InitializeCloud() or InitializeLocal() first.");

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userMessage);

        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7,
        };

        var result = await chatService.GetChatMessageContentAsync(history, settings, _kernel, ct);
        return result.Content ?? "";
    }

    /// <summary>
    /// Send a prompt with streaming response.
    /// </summary>
    public async Task StreamChatAsync(string systemPrompt, string userMessage, Action<string> onChunk, CancellationToken ct = default)
    {
        if (_kernel == null)
            throw new InvalidOperationException("AI Engine not initialized.");

        var chatService = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);
        history.AddUserMessage(userMessage);

        var settings = new OpenAIPromptExecutionSettings
        {
            MaxTokens = 4096,
            Temperature = 0.7,
        };

        await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(history, settings, _kernel, ct))
        {
            if (chunk.Content != null)
                onChunk(chunk.Content);
        }
    }

    private void Log(string msg) => LogMessage?.Invoke(this, msg);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}
