using OllamaSharp;
using OllamaSharp.Models;
using SumTube.Configuration;
using SumTube.Utils;

namespace SumTube.Services;

/// <summary>
/// Handles communication with Ollama API for summarization using OllamaSharp library
/// </summary>
public class OllamaApiService : IDisposable
{
    private readonly OllamaApiClient _ollamaClient;
    private readonly string _modelName;
    private readonly OllamaApiOptions _apiOptions;
    private readonly YouTubeConfig _youTubeConfig;
    private bool _disposed = false;

    public OllamaApiService(string baseUrl, string? modelName = null, OllamaApiOptions? apiOptions = null)
    {
        var config = ConfigurationService.Instance;
        
        _ollamaClient = new OllamaApiClient(baseUrl.TrimEnd('/'));
        
        _modelName = modelName ?? config.Ollama.DefaultModel;
        _apiOptions = apiOptions ?? config.Ollama.ApiOptions;
        _youTubeConfig = config.YouTube;
        
        Logger.Debug("OLLAMA_API", $"OllamaApiService initialized with endpoint: {baseUrl}");
        Logger.Debug("OLLAMA_API", $"Model: {_modelName}");
        Logger.DebugConfiguration("OllamaApiOptions", new Dictionary<string, object>
        {
            ["Temperature"] = _apiOptions.Temperature,
            ["TopP"] = _apiOptions.TopP,
            ["MaxTokens"] = _apiOptions.MaxTokens
        });
    }

    /// <summary>
    /// Generates a detailed Korean summary of the provided transcript
    /// </summary>
    public async Task<string> GenerateDetailedSummaryAsync(string transcript, CancellationToken cancellationToken = default)
    {
        return await GenerateSummaryInternalAsync(transcript, false, cancellationToken);
    }

    /// <summary>
    /// Generates an ultra-detailed and comprehensive Korean summary of the provided transcript
    /// </summary>
    public async Task<string> GenerateUltraDetailedSummaryAsync(string transcript, CancellationToken cancellationToken = default)
    {
        return await GenerateSummaryInternalAsync(transcript, true, cancellationToken);
    }

    /// <summary>
    /// Internal method for generating summaries with different detail levels
    /// </summary>
    private async Task<string> GenerateSummaryInternalAsync(string transcript, bool isUltraDetailed, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ArgumentException("스크립트가 제공되지 않았습니다.", nameof(transcript));
        }

        var detailLevel = isUltraDetailed ? "초상세" : "상세한";
        Logger.Info($"🤖 AI 모델을 사용하여 {detailLevel} 요약을 생성하고 있습니다...");
        Logger.Debug("OLLAMA_API", $"Starting {(isUltraDetailed ? "ultra-detailed" : "detailed")} summary generation for transcript of {transcript.Length} characters");

        var prompt = isUltraDetailed 
            ? CreateUltraDetailedSummaryPrompt(transcript)
            : CreateDetailedSummaryPrompt(transcript);
        Logger.Debug("OLLAMA_API", $"Generated prompt length: {prompt.Length} characters");
        
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var request = new GenerateRequest
            {
                Model = _modelName,
                Prompt = prompt,
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = _apiOptions.Temperature,
                    TopP = _apiOptions.TopP,
                    NumPredict = isUltraDetailed ? _apiOptions.MaxTokens * 2 : _apiOptions.MaxTokens // Ultra-detailed gets more tokens
                }
            };

            Logger.Debug("OLLAMA_API", $"Sending generate request to model: {_modelName}");
            Logger.Debug("OLLAMA_API", $"Request options - Temperature: {_apiOptions.Temperature}, TopP: {_apiOptions.TopP}, MaxTokens: {request.Options.NumPredict}");

            var responseBuilder = new System.Text.StringBuilder();
            var tokenCount = 0;
            
            await foreach (var response in _ollamaClient.Generate(request, cancellationToken))
            {
                if (response?.Response != null)
                {
                    responseBuilder.Append(response.Response);
                    tokenCount++;
                    
                    if (tokenCount % 10 == 0) // Log every 10 tokens to avoid spam
                    {
                        Logger.Debug("OLLAMA_API", $"Received {tokenCount} response chunks, current length: {responseBuilder.Length}");
                    }
                }
                
                if (response?.Done == true)
                {
                    Logger.Debug("OLLAMA_API", "Generation completed (Done=true received)");
                    break;
                }
            }

            stopwatch.Stop();
            var finalResponse = responseBuilder.ToString();
            
            Logger.Debug("OLLAMA_API", $"Generation completed in {stopwatch.ElapsedMilliseconds}ms");
            Logger.Debug("OLLAMA_API", $"Total response chunks: {tokenCount}");
            Logger.Debug("OLLAMA_API", $"Final response length: {finalResponse.Length} characters");
            
            if (string.IsNullOrWhiteSpace(finalResponse))
            {
                Logger.Error("AI 모델로부터 유효한 응답을 받지 못했습니다.");
                throw new InvalidOperationException("AI 모델로부터 유효한 응답을 받지 못했습니다.");
            }

            Logger.Info($"✅ {detailLevel} 요약이 생성되었습니다.");
            Logger.DebugPerformance($"AI {(isUltraDetailed ? "Ultra-Detailed" : "Detailed")} Summary Generation", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["InputLength"] = transcript.Length,
                ["PromptLength"] = prompt.Length,
                ["OutputLength"] = finalResponse.Length,
                ["ResponseChunks"] = tokenCount,
                ["Model"] = _modelName,
                ["DetailLevel"] = isUltraDetailed ? "Ultra" : "Standard"
            });
            
            var formattedOutput = FormatSummaryOutput(finalResponse, isUltraDetailed);
            Logger.Debug("OLLAMA_API", $"Formatted output length: {formattedOutput.Length} characters");
            
            return formattedOutput;
        }
        catch (HttpRequestException ex)
        {
            stopwatch.Stop();
            Logger.Error("Ollama API 호출 중 오류가 발생했습니다", ex);
            throw new InvalidOperationException($"Ollama API 호출 중 오류가 발생했습니다: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex)
        {
            stopwatch.Stop();
            Logger.Error("요약 생성 시간이 초과되었습니다", ex);
            throw new InvalidOperationException("요약 생성 시간이 초과되었습니다. 스크립트가 너무 클 수 있습니다.", ex);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            Logger.Error("AI 요약 생성 중 오류가 발생했습니다", ex);
            throw new InvalidOperationException($"AI 요약 생성 중 오류가 발생했습니다: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Creates a detailed summary prompt optimized for Korean output
    /// </summary>
    private string CreateDetailedSummaryPrompt(string transcript)
    {
        var originalLength = transcript.Length;
        
        // Truncate if transcript is too long (keep last part as it's often most important)
        if (transcript.Length > _youTubeConfig.MaxTranscriptLength)
        {
            transcript = "..." + transcript.Substring(transcript.Length - _youTubeConfig.MaxTranscriptLength);
            Logger.Debug("OLLAMA_API", $"Transcript truncated from {originalLength} to {transcript.Length} characters");
        }

        var prompt = $"""
아래의 원본 스크립트를 읽고 내용을 상세히 한국어로 요약해줘. 이 때 최상위 타이틀은 "## 요약" 으로 시작해줘. 그리고 프롬프트 수행에 대한 추가 문장은 완전히 빼줘

---

**원본 스크립트:**
{transcript}

위 스크립트를 바탕으로 지침에 따라 상세한 한국어 요약을 작성해줘:
""";

        Logger.Debug("OLLAMA_API", $"Standard detailed prompt structure created with {prompt.Length} characters");
        
        return prompt;
    }

    /// <summary>
    /// Creates an ultra-detailed summary prompt optimized for comprehensive Korean output
    /// </summary>
    private string CreateUltraDetailedSummaryPrompt(string transcript)
    {
        var originalLength = transcript.Length;
        
        // For ultra-detailed, allow more transcript content
        var maxLength = (int)(_youTubeConfig.MaxTranscriptLength * 1.5);
        if (transcript.Length > maxLength)
        {
            transcript = "..." + transcript.Substring(transcript.Length - maxLength);
            Logger.Debug("OLLAMA_API", $"Transcript truncated from {originalLength} to {transcript.Length} characters for ultra-detailed summary");
        }

        var prompt = $"""
아래의 원본 스크립트를 읽고 내용을 중요한 내용은 하나도 빠짐없이 완전 상세히 한국어로 요약해줘. 이 때 최상위 타이틀은 "## 요약" 으로 시작해줘. 그리고 프롬프트 수행에 대한 추가 문장은 완전히 빼줘

---

**원본 스크립트:**
{transcript}

위 스크립트를 바탕으로 지침에 따라 **극도로 상세하고 포괄적인** 한국어 요약을 작성해줘:
""";

        Logger.Debug("OLLAMA_API", $"Ultra-detailed prompt structure created with {prompt.Length} characters");
        
        return prompt;
    }

    /// <summary>
    /// Formats the summary output for better readability
    /// </summary>
    private static string FormatSummaryOutput(string summary, bool isUltraDetailed = false)
    {
        Logger.Debug("OLLAMA_API", $"Formatting summary output (input length: {summary.Length}, ultra-detailed: {isUltraDetailed})");
        
        // Clean up any formatting issues
        summary = summary.Trim();
        
        // Ensure proper line breaks
        summary = summary.Replace("\n\n\n", "\n\n");

        // Add decorative border
        var border = new string('═', 80);
        var title = isUltraDetailed ? "🎬 YOUTUBE 영상 초상세 요약" : "🎬 YOUTUBE 영상 상세 요약";
        var footer = isUltraDetailed ? "✨ SumTube 초상세 모드로 생성된 요약입니다" : "✨ SumTube로 생성된 요약입니다";
        
        var formattedSummary = $"""
{border}
{title}
{border}

{summary}

{border}
{footer}
{border}
""";

        Logger.Debug("OLLAMA_API", $"Summary formatted (output length: {formattedSummary.Length})");
        return formattedSummary;
    }

    /// <summary>
    /// Tests connection to Ollama API
    /// </summary>
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Debug("OLLAMA_API", "Testing API connection by listing models");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var models = await _ollamaClient.ListLocalModels(cancellationToken);
            
            stopwatch.Stop();
            Logger.Debug("OLLAMA_API", $"Connection test successful in {stopwatch.ElapsedMilliseconds}ms");
            Logger.Debug("OLLAMA_API", $"Found {models.Count()} local models");
            
            return true;
        }
        catch (Exception ex)
        {
            Logger.Debug("OLLAMA_API", $"Connection test failed: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Checks if the specified model is available
    /// </summary>
    public async Task<bool> IsModelAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.Debug("OLLAMA_API", $"Checking if model '{_modelName}' is available");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            var models = await _ollamaClient.ListLocalModels(cancellationToken);
            var isAvailable = models.Any(m => m.Name == _modelName);
            
            stopwatch.Stop();
            Logger.Debug("OLLAMA_API", $"Model availability check completed in {stopwatch.ElapsedMilliseconds}ms");
            Logger.Debug("OLLAMA_API", $"Model '{_modelName}' available: {isAvailable}");
            
            if (Logger.IsDebugMode && models.Any())
            {
                Logger.Debug("OLLAMA_API", $"Available models: {string.Join(", ", models.Select(m => m.Name))}");
            }
            
            return isAvailable;
        }
        catch (Exception ex)
        {
            Logger.Debug("OLLAMA_API", $"Model availability check failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Logger.Debug("OLLAMA_API", "OllamaApiService disposed");
        // OllamaApiClient doesn't implement IDisposable in current version
    }
}