using System.Diagnostics;
using OllamaSharp;
using OllamaSharp.Models;
using SumTube.Configuration;
using SumTube.Utils;
using ModelInfo = SumTube.Models.ModelInfo;

namespace SumTube.Services;

/// <summary>
/// Manages Ollama process lifecycle with OllamaSharp integration
/// </summary>
public class OllamaProcessService : IDisposable
{
    private Process? _ollamaProcess;
    private readonly string _ollamaPath;
    private readonly OllamaConfig _config;
    private readonly CancellationTokenSource _cancellationTokenSource;
    private bool _disposed = false;

    public OllamaProcessService(string ollamaPath, OllamaConfig? config = null)
    {
        _ollamaPath = ollamaPath;
        _config = config ?? ConfigurationService.Instance.Ollama;
        _cancellationTokenSource = new CancellationTokenSource();
        
        Logger.Debug("OLLAMA", $"OllamaProcessService가 초기화되었습니다. 경로: {ollamaPath}");
        Logger.Debug("OLLAMA", $"설정 - 포트: {_config.Port}, 기본모델: {_config.DefaultModel}");
    }

    /// <summary>
    /// Starts Ollama server in quiet mode on custom port
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_ollamaProcess != null && !_ollamaProcess.HasExited)
        {
            Logger.Debug("OLLAMA", "Ollama 프로세스가 이미 실행 중입니다. 시작을 건너뜁니다");
            return; // Already running
        }

        Logger.Info($"🚀 Ollama 서버를 포트 {_config.Port}에서 시작하고 있습니다...");
        Logger.Debug("OLLAMA", $"Ollama 서버를 시작합니다. 호스트: 127.0.0.1:{_config.Port}");

        var modelsPath = Path.Combine(Path.GetDirectoryName(_ollamaPath)!, "models");
        Logger.Debug("OLLAMA", $"모델 디렉토리: {modelsPath}");

        _ollamaProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ollamaPath,
                Arguments = "serve",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    ["OLLAMA_HOST"] = $"127.0.0.1:{_config.Port}",
                    ["OLLAMA_MODELS"] = modelsPath
                }
            },
            EnableRaisingEvents = true
        };

        // Handle process exit
        _ollamaProcess.Exited += (sender, e) =>
        {
            if (!_disposed)
            {
                Logger.Warning("Ollama 프로세스가 예기치 않게 종료되었습니다.");
                Logger.Debug("OLLAMA", $"Ollama 프로세스가 예기치 않게 종료되었습니다. 종료 코드: {_ollamaProcess?.ExitCode}");
            }
        };

        Logger.DebugProcessStart(_ollamaPath, "serve", Path.GetDirectoryName(_ollamaPath));
        _ollamaProcess.Start();

        // Wait for server to be ready
        await WaitForServerReadyAsync(cancellationToken);
        Logger.Info("✅ Ollama 서버가 준비되었습니다.");
    }

    /// <summary>
    /// Waits for Ollama server to become ready using OllamaSharp
    /// </summary>
    private async Task WaitForServerReadyAsync(CancellationToken cancellationToken)
    {
        var maxAttempts = _config.ServerStartupTimeoutSeconds;
        var attempt = 0;

        Logger.Info("⏳ Ollama 서버가 시작되기를 기다리는 중");
        Logger.Debug("OLLAMA", $"서버 준비 상태를 기다리는 중. 최대 시도 횟수: {maxAttempts}");

        var ollamaClient = new OllamaApiClient($"http://127.0.0.1:{_config.Port}");
        var stopwatch = Stopwatch.StartNew();

        while (attempt < maxAttempts && !cancellationToken.IsCancellationRequested)
        {
            try
            {
                Logger.Debug("OLLAMA", $"준비 상태 확인 시도 {attempt + 1}/{maxAttempts}");
                await ollamaClient.ListLocalModels(cancellationToken);
                stopwatch.Stop();
                Logger.Debug("OLLAMA", $"서버가 {stopwatch.ElapsedMilliseconds}ms 후 {attempt + 1}번째 시도에서 준비되었습니다");
                Console.WriteLine(); // New line after dots
                return; // Server is ready
            }
            catch (Exception ex)
            {
                Logger.Debug("OLLAMA", $"준비 상태 확인 실패: {ex.Message}");
                // Server not ready yet
            }

            Console.Write(".");
            await Task.Delay(1000, cancellationToken);
            attempt++;
        }

        stopwatch.Stop();
        Logger.Debug("OLLAMA", $"서버 준비 상태 확인 시간 초과. 소요 시간: {stopwatch.ElapsedMilliseconds}ms");
        Console.WriteLine(); // New line after dots
        throw new TimeoutException("Ollama 서버가 시작되지 않았습니다.");
    }

    /// <summary>
    /// Ensures the specified model is available and validates its integrity
    /// </summary>
    public async Task<SumTube.Models.ModelValidationResult> EnsureModelAsync(string? modelName = null, CancellationToken cancellationToken = default)
    {
        modelName ??= _config.DefaultModel;
        Logger.Info($"🔍 {modelName} 모델을 확인하고 있습니다...");
        Logger.Debug("MODEL", $"모델 검증을 시작합니다: {modelName}");

        var validationResult = new SumTube.Models.ModelValidationResult
        {
            ModelName = modelName
        };

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var ollamaClient = new OllamaApiClient($"http://127.0.0.1:{_config.Port}");

            // Step 1: Check if model exists in the list
            Logger.Debug("MODEL", "1단계: 모델 존재 여부 확인");
            validationResult.ModelExists = await CheckModelExistsAsync(ollamaClient, modelName, cancellationToken);
            Logger.Debug("MODEL", $"모델 존재 여부: {validationResult.ModelExists}");
            
            if (!validationResult.ModelExists)
            {
                Logger.Info($"📥 {modelName} 모델을 다운로드하고 있습니다...");
                Logger.Debug("MODEL", $"모델을 찾을 수 없습니다. 다운로드를 시작합니다: {modelName}");
                await PullModelAsync(modelName, cancellationToken);
                validationResult.WasRedownloaded = true;
                validationResult.ModelExists = true;
                Logger.Debug("MODEL", "모델 다운로드가 완료되었습니다");
            }

            // Step 2: Validate model integrity if enabled
            if (_config.ModelValidation.EnableIntegrityCheck)
            {
                Logger.Info($"🔐 {modelName} 모델 무결성을 검증하고 있습니다...");
                Logger.Debug("MODEL", "2단계: 모델 무결성 검증");
                var integrityValid = await ValidateModelIntegrityAsync(ollamaClient, modelName, cancellationToken);
                validationResult.InfoRetrieved = integrityValid;
                Logger.Debug("MODEL", $"무결성 검사 결과: {integrityValid}");
                
                if (!integrityValid)
                {
                    Logger.Warning($"{modelName} 모델 무결성 검증 실패. 재다운로드합니다...");
                    Logger.Debug("MODEL", "무결성 검사 실패. 모델을 제거하고 재다운로드합니다");
                    await RemoveModelAsync(modelName, cancellationToken);
                    await PullModelAsync(modelName, cancellationToken);
                    validationResult.WasRedownloaded = true;
                }
            }
            else
            {
                Logger.Debug("MODEL", "설정에서 무결성 검사가 비활성화되었습니다");
            }

            // Step 3: Perform functional test if enabled
            if (_config.ModelValidation.EnableFunctionalTest)
            {
                Logger.Info($"🧪 {modelName} 모델 기능을 테스트하고 있습니다...");
                Logger.Debug("MODEL", "3단계: 기능 테스트 수행");
                var functionalTest = await PerformFunctionalTestAsync(ollamaClient, modelName, cancellationToken);
                validationResult.FunctionalTestPassed = functionalTest.IsSuccess;
                validationResult.TestResponse = functionalTest.Response;
                Logger.Debug("MODEL", $"기능 테스트 결과: {functionalTest.IsSuccess}");
                Logger.Debug("MODEL", $"테스트 응답 길이: {functionalTest.Response?.Length ?? 0}");
                
                if (!functionalTest.IsSuccess)
                {
                    Logger.Warning($"{modelName} 모델 기능 테스트 실패. 재다운로드합니다...");
                    Logger.Debug("MODEL", "기능 테스트 실패. 모델을 제거하고 재다운로드합니다");
                    await RemoveModelAsync(modelName, cancellationToken);
                    await PullModelAsync(modelName, cancellationToken);
                    validationResult.WasRedownloaded = true;
                    
                    // Retry functional test after redownload
                    Logger.Debug("MODEL", "재다운로드 후 기능 테스트를 재시도합니다");
                    functionalTest = await PerformFunctionalTestAsync(ollamaClient, modelName, cancellationToken);
                    validationResult.FunctionalTestPassed = functionalTest.IsSuccess;
                    validationResult.TestResponse = functionalTest.Response;
                    Logger.Debug("MODEL", $"재시도 기능 테스트 결과: {functionalTest.IsSuccess}");
                }
            }
            else
            {
                Logger.Debug("MODEL", "설정에서 기능 테스트가 비활성화되었습니다");
            }

            // Step 4: Get model information
            Logger.Debug("MODEL", "4단계: 모델 정보 조회");
            validationResult.ModelInfo = await GetModelInfoAsync(ollamaClient, modelName, cancellationToken);
            if (validationResult.ModelInfo != null)
            {
                Logger.Debug("MODEL", $"모델 정보 조회됨 - 패밀리: {validationResult.ModelInfo.Family}, 파라미터: {validationResult.ModelInfo.Parameters}");
            }

            validationResult.IsValid = validationResult.ModelExists && 
                                     (!_config.ModelValidation.EnableIntegrityCheck || validationResult.InfoRetrieved) &&
                                     (!_config.ModelValidation.EnableFunctionalTest || validationResult.FunctionalTestPassed);

            stopwatch.Stop();
            validationResult.ValidationTime = stopwatch.Elapsed;

            if (validationResult.IsValid)
            {
                var statusMessage = validationResult.WasRedownloaded ? "재다운로드 후 " : "";
                Logger.Info($"✅ {modelName} 모델이 {statusMessage}검증되었습니다. (소요시간: {validationResult.ValidationTime.TotalSeconds:F1}초)");
                Logger.DebugPerformance($"모델 검증 ({modelName})", stopwatch.ElapsedMilliseconds, new Dictionary<string, object>
                {
                    ["재다운로드됨"] = validationResult.WasRedownloaded,
                    ["무결성검사활성화"] = _config.ModelValidation.EnableIntegrityCheck,
                    ["기능테스트활성화"] = _config.ModelValidation.EnableFunctionalTest
                });
            }
            else
            {
                validationResult.ErrorMessage = "모델 검증에 실패했습니다.";
                Logger.Error($"{modelName} 모델 검증 실패: {validationResult.ErrorMessage}");
            }

            return validationResult;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            validationResult.ValidationTime = stopwatch.Elapsed;
            validationResult.IsValid = false;
            validationResult.ErrorMessage = ex.Message;
            
            Logger.Error("모델 검증 중 오류가 발생했습니다", ex);
            throw;
        }
    }

    /// <summary>
    /// Checks if the model exists in Ollama using OllamaSharp
    /// </summary>
    private async Task<bool> CheckModelExistsAsync(OllamaApiClient ollamaClient, string modelName, CancellationToken cancellationToken)
    {
        try
        {
            Logger.Debug("MODEL", $"API를 통해 모델 존재 여부를 확인합니다: {modelName}");
            var models = await ollamaClient.ListLocalModels(cancellationToken);
            var exists = models.Any(m => m.Name == modelName);
            Logger.Debug("MODEL", $"{models.Count()}개의 로컬 모델을 찾았습니다. 대상 모델 존재 여부: {exists}");
            
            if (Logger.IsDebugMode && models.Any())
            {
                Logger.Debug("MODEL", $"사용 가능한 모델: {string.Join(", ", models.Select(m => m.Name))}");
            }
            
            return exists;
        }
        catch (Exception ex)
        {
            Logger.Debug("MODEL", $"모델 존재 여부 확인 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Validates model integrity by retrieving model information using OllamaSharp
    /// </summary>
    private async Task<bool> ValidateModelIntegrityAsync(OllamaApiClient ollamaClient, string modelName, CancellationToken cancellationToken)
    {
        try
        {
            Logger.Debug("MODEL", $"모델 무결성을 검증합니다: {modelName}");
            var modelInfo = await ollamaClient.ShowModel(modelName, cancellationToken);
            var isValid = modelInfo != null && !string.IsNullOrWhiteSpace(modelInfo.Modelfile);
            
            Logger.Debug("MODEL", $"모델 정보 조회됨: {modelInfo != null}");
            if (modelInfo != null)
            {
                Logger.Debug("MODEL", $"모델파일 길이: {modelInfo.Modelfile?.Length ?? 0}");
            }
            
            return isValid;
        }
        catch (Exception ex)
        {
            Logger.Debug("MODEL", $"모델 무결성 검증 실패: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Performs a functional test on the model using OllamaSharp
    /// </summary>
    private async Task<(bool IsSuccess, string? Response)> PerformFunctionalTestAsync(OllamaApiClient ollamaClient, string modelName, CancellationToken cancellationToken)
    {
        try
        {
            Logger.Debug("MODEL", $"모델에 대해 기능 테스트를 수행합니다: {modelName}");
            Logger.Debug("MODEL", $"테스트 프롬프트: '{_config.ModelValidation.TestPrompt}'");
            
            var request = new GenerateRequest
            {
                Model = modelName,
                Prompt = _config.ModelValidation.TestPrompt,
                Stream = false,
                Options = new RequestOptions
                {
                    Temperature = 0.1f,
                    NumPredict = 50
                }
            };

            var responseBuilder = new System.Text.StringBuilder();
            var stopwatch = Stopwatch.StartNew();
            
            await foreach (var response in ollamaClient.Generate(request, cancellationToken))
            {
                if (response?.Response != null)
                {
                    responseBuilder.Append(response.Response);
                }
                
                if (response?.Done == true)
                    break;
            }

            stopwatch.Stop();
            var finalResponse = responseBuilder.ToString();
            
            Logger.Debug("MODEL", $"기능 테스트가 {stopwatch.ElapsedMilliseconds}ms에 완료되었습니다");
            Logger.Debug("MODEL", $"응답 길이: {finalResponse.Length}, 기대 최소 길이: {_config.ModelValidation.ExpectedResponseLength}");
            
            if (!string.IsNullOrWhiteSpace(finalResponse))
            {
                var isValid = finalResponse.Length >= _config.ModelValidation.ExpectedResponseLength;
                Logger.Debug("MODEL", $"기능 테스트 결과: {isValid}");
                
                if (Logger.IsDebugMode)
                {
                    var previewLength = Math.Min(100, finalResponse.Length);
                    Logger.Debug("MODEL", $"응답 미리보기: '{finalResponse.Substring(0, previewLength)}{(finalResponse.Length > previewLength ? "..." : "")}'");
                }
                
                return (isValid, finalResponse);
            }

            Logger.Debug("MODEL", "기능 테스트 실패: 빈 응답");
            return (false, null);
        }
        catch (Exception ex)
        {
            Logger.Debug("MODEL", $"기능 테스트가 예외로 인해 실패했습니다: {ex.Message}");
            return (false, $"오류: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets detailed model information using OllamaSharp
    /// </summary>
    private async Task<ModelInfo?> GetModelInfoAsync(OllamaApiClient ollamaClient, string modelName, CancellationToken cancellationToken)
    {
        try
        {
            Logger.Debug("MODEL", $"상세 모델 정보를 조회합니다: {modelName}");
            var modelDetails = await ollamaClient.ShowModel(modelName, cancellationToken);
            
            if (modelDetails == null) 
            {
                Logger.Debug("MODEL", "API에서 모델 세부 정보가 반환되지 않았습니다");
                return null;
            }

            var modelInfo = new ModelInfo
            {
                Name = modelName,
                Family = modelDetails.Details?.Family,
                Parameters = modelDetails.Details?.ParameterSize,
                CreatedAt = DateTime.Now // Use current time since ModifiedAt might not be available
            };

            Logger.Debug("MODEL", $"모델 정보가 생성되었습니다 - 패밀리: {modelInfo.Family}, 파라미터: {modelInfo.Parameters}");
            return modelInfo;
        }
        catch (Exception ex)
        {
            Logger.Debug("MODEL", $"모델 정보 조회 실패: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Removes a corrupted model
    /// </summary>
    private async Task RemoveModelAsync(string modelName, CancellationToken cancellationToken)
    {
        try
        {
            Logger.Info($"🗑️ 손상된 {modelName} 모델을 제거하고 있습니다...");
            Logger.Debug("MODEL", $"모델을 제거합니다: {modelName}");
            
            var removeProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ollamaPath,
                    Arguments = $"rm {modelName}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    Environment =
                    {
                        ["OLLAMA_HOST"] = $"127.0.0.1:{_config.Port}"
                    }
                }
            };

            var stopwatch = Stopwatch.StartNew();
            Logger.DebugProcessStart(_ollamaPath, $"rm {modelName}");
            
            removeProcess.Start();
            await removeProcess.WaitForExitAsync(cancellationToken);
            
            stopwatch.Stop();
            Logger.DebugProcessExit(_ollamaPath, removeProcess.ExitCode, stopwatch.ElapsedMilliseconds);
            
            if (removeProcess.ExitCode == 0)
            {
                Logger.Info($"✅ {modelName} 모델이 제거되었습니다.");
            }
            else
            {
                Logger.Warning($"모델 제거가 완료되지 않았습니다. 종료 코드: {removeProcess.ExitCode}");
            }
        }
        catch (Exception ex)
        {
            Logger.Error("모델 제거 중 오류", ex);
        }
    }

    /// <summary>
    /// Downloads the specified model with enhanced progress display
    /// </summary>
    private async Task PullModelAsync(string modelName, CancellationToken cancellationToken)
    {
        var progress = new DownloadProgressDisplay($"{modelName} 모델");
        progress.Start();
        
        Logger.Debug("MODEL", $"모델 다운로드를 시작합니다: {modelName}");

        var pullProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _ollamaPath,
                Arguments = $"pull {modelName}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                Environment =
                {
                    ["OLLAMA_HOST"] = $"127.0.0.1:{_config.Port}"
                }
            }
        };

        var progressLines = new List<string>();
        var hasProgressInfo = false;
        var stopwatch = Stopwatch.StartNew();

        Logger.DebugProcessStart(_ollamaPath, $"pull {modelName}");

        pullProcess.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                progressLines.Add(e.Data);
                Logger.Debug("MODEL", $"Pull 출력: {e.Data}");
                
                // Try to parse Ollama's progress output
                if (TryParseOllamaProgress(e.Data, out var bytesRead, out var totalBytes))
                {
                    progress.UpdateProgress(bytesRead, totalBytes);
                    hasProgressInfo = true;
                    Logger.Debug("MODEL", $"진행률 파싱됨 - {bytesRead:N0}/{totalBytes:N0} 바이트");
                }
                else if (!hasProgressInfo)
                {
                    // Show raw output if we can't parse progress
                    Console.WriteLine($"📦 {e.Data}");
                }
            }
        };

        pullProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Logger.Warning($"{e.Data}");
                Logger.Debug("MODEL", $"Pull 오류 출력: {e.Data}");
            }
        };

        pullProcess.Start();
        pullProcess.BeginOutputReadLine();
        pullProcess.BeginErrorReadLine();
        
        await pullProcess.WaitForExitAsync(cancellationToken);

        stopwatch.Stop();
        Logger.DebugProcessExit(_ollamaPath, pullProcess.ExitCode, stopwatch.ElapsedMilliseconds);

        if (pullProcess.ExitCode == 0)
        {
            progress.Complete();
            Logger.Debug("MODEL", $"모델 다운로드가 {stopwatch.ElapsedMilliseconds}ms에 성공적으로 완료되었습니다");
        }
        else
        {
            progress.Fail($"종료 코드: {pullProcess.ExitCode}");
            Logger.Error($"모델 다운로드에 실패했습니다. 종료 코드: {pullProcess.ExitCode}");
            throw new InvalidOperationException($"모델 다운로드에 실패했습니다. 종료 코드: {pullProcess.ExitCode}");
        }
    }

    /// <summary>
    /// Tries to parse Ollama's progress output
    /// </summary>
    private static bool TryParseOllamaProgress(string output, out long bytesRead, out long totalBytes)
    {
        bytesRead = 0;
        totalBytes = 0;

        try
        {
            // Ollama typically outputs progress like: "pulling 12.3 MB / 45.6 MB"
            if (output.Contains("pulling") && output.Contains("/"))
            {
                var parts = output.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < parts.Length - 2; i++)
                {
                    if (parts[i] == "pulling" && parts[i + 2] == "/")
                    {
                        if (TryParseSizeString(parts[i + 1], out bytesRead) &&
                            TryParseSizeString(parts[i + 3], out totalBytes))
                        {
                            return true;
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return false;
    }

    /// <summary>
    /// Tries to parse size strings like "12.3 MB" to bytes
    /// </summary>
    private static bool TryParseSizeString(string sizeStr, out long bytes)
    {
        bytes = 0;
        
        try
        {
            var parts = sizeStr.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2 && double.TryParse(parts[0], out var size))
            {
                var unit = parts[1].ToUpperInvariant();
                var multiplier = unit switch
                {
                    "B" => 1L,
                    "KB" => 1024L,
                    "MB" => 1024L * 1024L,
                    "GB" => 1024L * 1024L * 1024L,
                    "TB" => 1024L * 1024L * 1024L * 1024L,
                    _ => 0L
                };

                if (multiplier > 0)
                {
                    bytes = (long)(size * multiplier);
                    return true;
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return false;
    }

    /// <summary>
    /// Gets the API endpoint URL
    /// </summary>
    public string GetApiUrl() => $"http://127.0.0.1:{_config.Port}";

    /// <summary>
    /// Gets the configured model name
    /// </summary>
    public string GetModelName() => _config.DefaultModel;

    /// <summary>
    /// Gets the Ollama API options
    /// </summary>
    public OllamaApiOptions GetApiOptions() => _config.ApiOptions;

    /// <summary>
    /// Checks if Ollama process is running
    /// </summary>
    public bool IsRunning => _ollamaProcess != null && !_ollamaProcess.HasExited;

    /// <summary>
    /// Stops the Ollama server gracefully
    /// </summary>
    public async Task StopAsync()
    {
        if (_ollamaProcess == null || _ollamaProcess.HasExited)
        {
            Logger.Debug("OLLAMA", "종료할 실행 중인 프로세스가 없습니다");
            return;
        }

        Logger.Info("🛑 Ollama 서버를 종료하고 있습니다...");
        Logger.Debug("OLLAMA", $"Ollama 프로세스를 종료합니다 (PID: {_ollamaProcess.Id})");

        try
        {
            _cancellationTokenSource.Cancel();
            
            var stopwatch = Stopwatch.StartNew();
            
            // Try graceful shutdown first
            _ollamaProcess.CloseMainWindow();
            Logger.Debug("OLLAMA", "창 닫기 신호를 보냈습니다");
            
            // Wait for configured shutdown timeout
            var shutdownTimeout = _config.ServerShutdownTimeoutSeconds * 1000;
            if (!_ollamaProcess.WaitForExit(shutdownTimeout))
            {
                Logger.Debug("OLLAMA", $"정상 종료 시간 초과 ({shutdownTimeout}ms), 강제 종료합니다");
                // Force kill if graceful shutdown failed
                _ollamaProcess.Kill();
                await _ollamaProcess.WaitForExitAsync();
                Logger.Debug("OLLAMA", "프로세스가 강제로 종료되었습니다");
            }
            else
            {
                Logger.Debug("OLLAMA", "프로세스가 정상적으로 종료되었습니다");
            }

            stopwatch.Stop();
            Logger.Info("✅ Ollama 서버가 종료되었습니다.");
            Logger.DebugPerformance("Ollama 종료", stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            Logger.Error("Ollama 종료 중 오류", ex);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;

        _disposed = true;
        Logger.Debug("OLLAMA", "OllamaProcessService를 정리합니다");
        
        try
        {
            var timeout = ConfigurationService.SecondsToTimeSpan(_config.ServerShutdownTimeoutSeconds);
            StopAsync().Wait(timeout);
        }
        catch (Exception ex)
        {
            Logger.Debug("OLLAMA", $"정리 중 오류 발생: {ex.Message}");
            // Ignore errors during disposal
        }

        _ollamaProcess?.Dispose();
        _cancellationTokenSource?.Dispose();
        Logger.Debug("OLLAMA", "OllamaProcessService가 정리되었습니다");
    }
}