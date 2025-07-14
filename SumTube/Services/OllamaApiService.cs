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
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new ArgumentException("스크립트가 제공되지 않았습니다.", nameof(transcript));
        }

        Logger.Info("🤖 AI 모델을 사용하여 상세한 요약을 생성하고 있습니다...");
        Logger.Debug("OLLAMA_API", $"Starting summary generation for transcript of {transcript.Length} characters");

        var prompt = CreateDetailedSummaryPrompt(transcript);
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
                    NumPredict = _apiOptions.MaxTokens
                }
            };

            Logger.Debug("OLLAMA_API", $"Sending generate request to model: {_modelName}");
            Logger.Debug("OLLAMA_API", $"Request options - Temperature: {_apiOptions.Temperature}, TopP: {_apiOptions.TopP}, MaxTokens: {_apiOptions.MaxTokens}");

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

            Logger.Info("✅ 상세한 요약이 생성되었습니다.");
            Logger.DebugPerformance("AI Summary Generation", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["InputLength"] = transcript.Length,
                ["PromptLength"] = prompt.Length,
                ["OutputLength"] = finalResponse.Length,
                ["ResponseChunks"] = tokenCount,
                ["Model"] = _modelName
            });
            
            var formattedOutput = FormatSummaryOutput(finalResponse);
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
다음은 YouTube 영상의 스크립트입니다. 이 내용을 바탕으로 매우 상세하고 정확한 한국어 요약을 작성해주세요.

**요약 작성 지침:**
1. 반드시 한국어로 작성
2. 주요 내용을 빠뜨리지 않고 상세히 설명
3. 논리적 구조로 정리 (도입부, 주요 내용, 결론)
4. 핵심 키워드와 중요한 개념 강조
5. 구체적인 예시나 수치가 있다면 포함
6. 원문의 의미와 뉘앙스 보존

**출력 형식:**
## 📌 영상 요약

### 🎯 핵심 주제
[영상의 주요 주제와 목적을 2-3문장으로 요약]

### 📋 주요 내용
[영상의 핵심 내용을 체계적으로 정리. 각 포인트를 자세히 설명]

### 💡 핵심 포인트
[기억해야 할 중요한 점들을 항목별로 정리]

### 🎯 결론 및 시사점
[영상의 결론과 시청자가 얻을 수 있는 인사이트]

---

**원본 스크립트:**
{transcript}

위 스크립트를 바탕으로 지침에 따라 상세한 한국어 요약을 작성해주세요:
""";

        Logger.Debug("OLLAMA_API", $"Prompt structure created with {prompt.Length} characters");
        
        return prompt;
    }

    /// <summary>
    /// Formats the summary output for better readability
    /// </summary>
    private static string FormatSummaryOutput(string summary)
    {
        Logger.Debug("OLLAMA_API", $"Formatting summary output (input length: {summary.Length})");
        
        // Clean up any formatting issues
        summary = summary.Trim();
        
        // Ensure proper line breaks
        summary = summary.Replace("\n\n\n", "\n\n");
        
        // Add decorative border
        var border = new string('═', 80);
        var formattedSummary = $"""
{border}
🎬 YOUTUBE 영상 상세 요약
{border}

{summary}

{border}
✨ SumTube로 생성된 요약입니다
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