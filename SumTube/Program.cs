using System.CommandLine;
using SumTube.Services;
using SumTube.Utils;

namespace SumTube;

/// <summary>
/// Main entry point for SumTube application
/// Portable YouTube video summarizer using yt-dlp and Ollama
/// </summary>
class Program
{
    private static RuntimeSetupService? _runtimeSetup;
    private static OllamaProcessService? _ollamaProcess;
    private static YouTubeService? _youtubeService;
    private static OllamaApiService? _ollamaApiService;

    static async Task<int> Main(string[] args)
    {
        // 테스트를 위한 기본 인자 설정
        if (args.Length is 0)
        {
            args =
            [
                "--url", "https://www.youtube.com/watch?v=5V249a2hPf8",
                "--debug"
            ];
        }

        Console.OutputEncoding = System.Text.Encoding.UTF8;
        
        // Load configuration first
        await ConfigurationService.Instance.LoadAsync();
        
        // Show application header
        ShowHeader();

        // Set up cancellation handling
        using var cancellationTokenSource = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cancellationTokenSource.Cancel();
            Logger.Info("\n🛑 종료 요청을 받았습니다. 정리 중...");
        };

        try
        {
            return await RunApplicationAsync(args, cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            Logger.Info("🚪 애플리케이션이 사용자에 의해 취소되었습니다.");
            return 1;
        }
        catch (Exception ex)
        {
            Logger.Error("예기치 않은 오류가 발생했습니다", ex);
            return 1;
        }
        finally
        {
            await CleanupAsync();
        }
    }

    /// <summary>
    /// Main application logic
    /// </summary>
    private static async Task<int> RunApplicationAsync(string[] args, CancellationToken cancellationToken)
    {
        // Define command line options
        var urlOption = new Option<string>("--url", "-u")
        {
            Description = "YouTube 영상 URL을 지정합니다.",
            Required = true
        };

        var modelOption = new Option<string>("--model", "-m")
        {
            Description = "사용할 Ollama AI 모델을 지정합니다. (예: llama3.1:8b, gemma2:9b)"
        };

        var debugOption = new Option<bool>("--debug", "-d")
        {
            Description = "디버그 모드를 활성화하여 상세한 로그를 출력합니다."
        };

        var rootCommand = new RootCommand("SumTube - YouTube 영상 AI 요약기");
        rootCommand.Options.Add(urlOption);
        rootCommand.Options.Add(modelOption);
        rootCommand.Options.Add(debugOption);

        // Parse arguments manually for simplicity
        var parseResult = rootCommand.Parse(args);
        
        if (parseResult.Errors.Count > 0)
        {
            foreach (var error in parseResult.Errors)
            {
                Logger.Error($"오류: {error.Message}");
            }
            return 1;
        }

        // Enable debug mode if specified
        var debugMode = parseResult.GetValue(debugOption);
        Logger.IsDebugMode = debugMode;

        if (debugMode)
        {
            Logger.Info("🐛 디버그 모드가 활성화되었습니다. 상세한 로그가 출력됩니다.");
            Logger.Debug("STARTUP", "Command line arguments parsed successfully");
            Logger.Debug("STARTUP", $"Arguments: {string.Join(" ", args)}");
        }

        var url = parseResult.GetValue(urlOption);
        if (string.IsNullOrWhiteSpace(url))
        {
            Logger.Error("YouTube URL이 제공되지 않았습니다.");
            Logger.Info("사용법: SumTube --url \"https://www.youtube.com/watch?v=VIDEO_ID\" [--model MODEL_NAME] [--debug]");
            return 1;
        }

        var modelName = parseResult.GetValue(modelOption);
        
        Logger.Debug("STARTUP", $"Parsed options - URL: {url}, Model: {modelName ?? "default"}, Debug: {debugMode}");
        
        await ProcessVideoAsync(url, modelName, cancellationToken);
        return 0;
    }

    /// <summary>
    /// Processes the YouTube video and generates summary
    /// </summary>
    private static async Task ProcessVideoAsync(string youtubeUrl, string? modelName, CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        
        try
        {
            var config = ConfigurationService.Instance;

            // Log configuration in debug mode
            if (Logger.IsDebugMode)
            {
                Logger.DebugConfiguration("Ollama", new Dictionary<string, object>
                {
                    ["Port"] = config.Ollama.Port,
                    ["DefaultModel"] = config.Ollama.DefaultModel,
                    ["ConnectionTimeoutMinutes"] = config.Ollama.ConnectionTimeoutMinutes,
                    ["ServerStartupTimeoutSeconds"] = config.Ollama.ServerStartupTimeoutSeconds
                });
            }

            // Override model name if provided via command line
            var selectedModel = modelName ?? config.Ollama.DefaultModel;
            if (!string.IsNullOrWhiteSpace(modelName))
            {
                Logger.Info($"🎯 명령줄에서 지정한 모델을 사용합니다: {selectedModel}");
                Logger.Debug("MODEL", $"Model override from command line: {selectedModel}");
            }
            else
            {
                Logger.Info($"🎯 설정 파일의 기본 모델을 사용합니다: {selectedModel}");
                Logger.Debug("MODEL", $"Using default model from configuration: {selectedModel}");
            }

            // Step 1: Initialize runtime environment
            Logger.Debug("RUNTIME", "Initializing runtime setup service");
            _runtimeSetup = new RuntimeSetupService();
            await _runtimeSetup.InitializeAsync(cancellationToken);
            Logger.Debug("RUNTIME", "Runtime setup completed");

            // Step 2: Start Ollama server with configured settings
            Logger.Debug("OLLAMA", $"Starting Ollama process from path: {_runtimeSetup.GetOllamaPath()}");
            _ollamaProcess = new OllamaProcessService(_runtimeSetup.GetOllamaPath());
            await _ollamaProcess.StartAsync(cancellationToken);
            Logger.Debug("OLLAMA", $"Ollama server started on port {config.Ollama.Port}");

            // Step 3: Ensure and validate specified model
            Logger.Debug("MODEL", $"Beginning model validation for: {selectedModel}");
            var validationResult = await _ollamaProcess.EnsureModelAsync(selectedModel, cancellationToken);
            
            if (!validationResult.IsValid)
            {
                Logger.Error($"{selectedModel} 모델 검증에 실패했습니다: {validationResult.ErrorMessage}");
                throw new InvalidOperationException($"{selectedModel} 모델 검증에 실패했습니다: {validationResult.ErrorMessage}");
            }

            // Display validation results
            Logger.Info($"📊 모델 검증 완료:");
            Logger.Info($"   • 모델명: {validationResult.ModelName}");
            Logger.Info($"   • 검증 시간: {validationResult.ValidationTime.TotalSeconds:F1}초");
            
            if (validationResult.WasRedownloaded)
            {
                Logger.Info($"   • 상태: 재다운로드됨");
            }
            
            if (validationResult.ModelInfo != null)
            {
                Logger.Info($"   • 모델 정보: {validationResult.ModelInfo.Family} ({validationResult.ModelInfo.Parameters})");
                Logger.Debug("MODEL", $"Model details - Family: {validationResult.ModelInfo.Family}, Parameters: {validationResult.ModelInfo.Parameters}, CreatedAt: {validationResult.ModelInfo.CreatedAt}");
            }

            // Step 4: Initialize services with configuration
            Logger.Debug("SERVICES", "Initializing YouTube and Ollama API services");
            _youtubeService = new YouTubeService(_runtimeSetup.GetYtDlpPath(), _runtimeSetup.GetPythonPath());
            _ollamaApiService = new OllamaApiService(
                _ollamaProcess.GetApiUrl(), 
                selectedModel,
                _ollamaProcess.GetApiOptions()
            );
            Logger.Debug("SERVICES", $"Services initialized - YT-DLP: {_runtimeSetup.GetYtDlpPath()}, Ollama API: {_ollamaProcess.GetApiUrl()}");

            // Step 5: Test Ollama connection
            Logger.Info("🔗 Ollama API 연결을 확인하고 있습니다...");
            Logger.Debug("API", "Testing Ollama API connection");
            
            if (!await _ollamaApiService.TestConnectionAsync(cancellationToken))
            {
                Logger.Error("Ollama 서버에 연결할 수 없습니다.");
                throw new InvalidOperationException("Ollama 서버에 연결할 수 없습니다.");
            }
            Logger.Debug("API", "Ollama API connection test successful");

            Logger.Debug("API", $"Checking if model {selectedModel} is available via API");
            if (!await _ollamaApiService.IsModelAvailableAsync(cancellationToken))
            {
                Logger.Error($"{selectedModel} 모델을 API에서 찾을 수 없습니다.");
                throw new InvalidOperationException($"{selectedModel} 모델을 API에서 찾을 수 없습니다.");
            }
            Logger.Debug("API", $"Model {selectedModel} is available via API");

            Logger.Info($"✅ 모든 준비가 완료되었습니다. {selectedModel} 모델로 영상 처리를 시작합니다.\n");

            // Step 6: Extract transcript from YouTube video
            Logger.Debug("YOUTUBE", $"Starting transcript extraction for URL: {youtubeUrl}");
            var transcriptStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var transcript = await _youtubeService.ExtractTranscriptAsync(youtubeUrl, cancellationToken);
            transcriptStopwatch.Stop();
            
            Logger.Debug("YOUTUBE", $"Transcript extraction completed in {transcriptStopwatch.ElapsedMilliseconds}ms");
            Logger.Debug("YOUTUBE", $"Transcript length: {transcript.Length} characters");

            // Step 7: Generate detailed Korean summary
            Logger.Debug("AI", "Starting AI summary generation");
            var summaryStopwatch = System.Diagnostics.Stopwatch.StartNew();
            var summary = await _ollamaApiService.GenerateDetailedSummaryAsync(transcript, cancellationToken);
            summaryStopwatch.Stop();
            
            Logger.Debug("AI", $"Summary generation completed in {summaryStopwatch.ElapsedMilliseconds}ms");
            Logger.Debug("AI", $"Summary length: {summary.Length} characters");

            // Step 8: Display results
            Logger.Info("\n" + summary);

            stopwatch.Stop();
            Logger.DebugPerformance("Total Process", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
            {
                ["TranscriptExtractionMs"] = transcriptStopwatch.ElapsedMilliseconds,
                ["SummaryGenerationMs"] = summaryStopwatch.ElapsedMilliseconds,
                ["TranscriptLength"] = transcript.Length,
                ["SummaryLength"] = summary.Length
            });

        }
        catch (ArgumentException ex)
        {
            Logger.Error($"입력 오류: {ex.Message}");
            Logger.Debug("ERROR", $"ArgumentException details: {ex}");
        }
        catch (InvalidOperationException ex)
        {
            Logger.Error($"처리 오류: {ex.Message}");
            Logger.Debug("ERROR", $"InvalidOperationException details: {ex}");
        }
        catch (Exception ex)
        {
            Logger.Error("예기치 않은 오류", ex);
            Logger.Debug("ERROR", $"Unexpected exception details: {ex}");
        }
    }

    /// <summary>
    /// Shows application header and information
    /// </summary>
    private static void ShowHeader()
    {
        var config = ConfigurationService.Instance;
        var header = $"""
╔══════════════════════════════════════════════════════════════════════════════╗
║                                  SumTube                                     ║
║                        YouTube 영상 AI 요약 프로그램                          ║
║                                                                              ║
║  • yt-dlp를 사용한 자막/스크립트 추출                                          ║
║  • Ollama AI 모델로 한국어 상세 요약                                           ║
║  • 완전한 포터블 실행 환경                                                     ║
║  • 자동 업데이트 지원                                                         ║
║  • 명령줄 모델 선택 지원                                                       ║
║  • 고급 모델 검증 및 자동 복구                                                  ║
║  • 디버그 모드 지원                                                           ║
║                                                                              ║
║  기본 모델: {config.Ollama.DefaultModel}                                      ║
║                                                                              ║
╚══════════════════════════════════════════════════════════════════════════════╝

""";
        Logger.Info(header);
    }

    /// <summary>
    /// Cleans up resources before application exit
    /// </summary>
    private static async Task CleanupAsync()
    {
        Logger.Info("🧹 리소스를 정리하고 있습니다...");
        Logger.Debug("CLEANUP", "Starting resource cleanup");

        try
        {
            // Dispose services in reverse order
            _ollamaApiService?.Dispose();
            Logger.Debug("CLEANUP", "OllamaApiService disposed");
            
            if (_ollamaProcess != null)
            {
                await _ollamaProcess.StopAsync();
                _ollamaProcess.Dispose();
                Logger.Debug("CLEANUP", "OllamaProcessService stopped and disposed");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("정리 중 오류", ex);
        }

        Logger.Info("👋 SumTube를 사용해 주셔서 감사합니다!");
        Logger.Debug("CLEANUP", "Application cleanup completed");
    }
}
