using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using SumTube.Models;
using SumTube.Configuration;
using SumTube.Utils;

namespace SumTube.Services;

/// <summary>
/// Manages portable runtime environment setup and updates
/// </summary>
public class RuntimeSetupService
{
    private static readonly HttpClient _httpClient = new();
    private readonly string _runtimePath;
    private readonly string _pythonPath;
    private readonly string _ollamaPath;
    private readonly string _versionsFile;
    private readonly DownloadsConfig _downloadsConfig;
    private readonly UpdatesConfig _updatesConfig;
    private readonly RuntimeConfig _runtimeConfig;

    public RuntimeSetupService()
    {
        var config = ConfigurationService.Instance;
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        
        _runtimePath = Path.Combine(basePath, "runtime");
        _pythonPath = Path.Combine(_runtimePath, "python");
        _ollamaPath = Path.Combine(_runtimePath, "ollama");
        _versionsFile = Path.Combine(_runtimePath, "versions.json");
        
        _downloadsConfig = config.Downloads;
        _updatesConfig = config.Updates;
        _runtimeConfig = config.Runtime;

        Directory.CreateDirectory(_runtimePath);
        Directory.CreateDirectory(_pythonPath);
        Directory.CreateDirectory(_ollamaPath);
    }

    /// <summary>
    /// Initializes the runtime environment with all required components
    /// </summary>
    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        Console.WriteLine("🔧 런타임 환경을 초기화하고 있습니다...");

        var versionInfo = await LoadVersionInfoAsync();
        var needsUpdate = await CheckForUpdatesAsync(versionInfo);

        if (needsUpdate.Any(x => x.WasUpdated || !x.IsSuccess))
        {
            Console.WriteLine("📦 필요한 구성 요소를 업데이트하고 있습니다...");
            
            await SetupPythonAsync(cancellationToken);
            await SetupYtDlpAsync(cancellationToken);
            await SetupOllamaAsync(cancellationToken);
            
            await SaveVersionInfoAsync(versionInfo);
        }

        Console.WriteLine("✅ 런타임 환경 초기화가 완료되었습니다.");
    }

    /// <summary>
    /// Checks for updates to all runtime components
    /// </summary>
    private async Task<List<UpdateResult>> CheckForUpdatesAsync(VersionInfo versionInfo)
    {
        var results = new List<UpdateResult>();

        // Skip if checked within configured interval
        var checkInterval = ConfigurationService.HoursToTimeSpan(_updatesConfig.CheckIntervalHours);
        if (DateTime.Now - versionInfo.LastChecked < checkInterval)
        {
            return results;
        }

        Console.WriteLine("🔍 최신 버전을 확인하고 있습니다...");

        // Check yt-dlp version
        var ytDlpResult = await CheckYtDlpVersionAsync(versionInfo.YtDlpVersion);
        results.Add(ytDlpResult);

        // Check Ollama version
        var ollamaResult = await CheckOllamaVersionAsync(versionInfo.OllamaVersion);
        results.Add(ollamaResult);

        versionInfo.LastChecked = DateTime.Now;
        return results;
    }

    /// <summary>
    /// Sets up embedded Python environment
    /// </summary>
    private async Task SetupPythonAsync(CancellationToken cancellationToken)
    {
        var pythonExe = Path.Combine(_pythonPath, "python.exe");
        if (File.Exists(pythonExe)) return;

        Console.WriteLine("🐍 Python 환경을 설정하고 있습니다...");

        // Download Python embeddable package
        var pythonZip = Path.Combine(_runtimePath, "python.zip");

        await DownloadFileAsync(_downloadsConfig.PythonEmbeddedUrl, pythonZip, "Python 임베디드 패키지", cancellationToken);
        ZipFile.ExtractToDirectory(pythonZip, _pythonPath, true);
        File.Delete(pythonZip);

        // Configure Python path
        var pthFile = Path.Combine(_pythonPath, $"python{_runtimeConfig.PythonVersion.Replace(".", "")[..3]}._pth");
        if (File.Exists(pthFile))
        {
            var content = await File.ReadAllTextAsync(pthFile, cancellationToken);
            if (!content.Contains("Lib\\site-packages"))
            {
                content += "\nLib\\site-packages\n";
                await File.WriteAllTextAsync(pthFile, content, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Sets up yt-dlp in Python environment
    /// </summary>
    private async Task SetupYtDlpAsync(CancellationToken cancellationToken)
    {
        var ytDlpExe = Path.Combine(_pythonPath, "Scripts", "yt-dlp.exe");
        var pythonExe = Path.Combine(_pythonPath, "python.exe");

        if (!File.Exists(pythonExe))
        {
            throw new InvalidOperationException("Python is not installed");
        }

        Console.WriteLine("📺 yt-dlp를 설정하고 있습니다...");

        // Install pip first
        var getPipPath = Path.Combine(_runtimePath, "get-pip.py");
        
        await DownloadFileAsync(_downloadsConfig.GetPipUrl, getPipPath, "pip 설치 스크립트", cancellationToken);
        
        var pipInstallProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = pythonExe,
                Arguments = $"\"{getPipPath}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _pythonPath
            }
        };

        pipInstallProcess.Start();
        await pipInstallProcess.WaitForExitAsync(cancellationToken);
        File.Delete(getPipPath);

        // Install yt-dlp
        var ytDlpInstallProcess = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(_pythonPath, "Scripts", "pip.exe"),
                Arguments = "install yt-dlp --quiet --no-warn-script-location",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = _pythonPath
            }
        };

        ytDlpInstallProcess.Start();
        await ytDlpInstallProcess.WaitForExitAsync(cancellationToken);
    }

    /// <summary>
    /// Sets up portable Ollama
    /// </summary>
    private async Task SetupOllamaAsync(CancellationToken cancellationToken)
    {
        var ollamaExe = Path.Combine(_ollamaPath, "ollama.exe");
        if (File.Exists(ollamaExe)) return;

        Console.WriteLine("🤖 Ollama를 설정하고 있습니다...");

        // Download Ollama for Windows
        var ollamaZip = Path.Combine(_runtimePath, "ollama.zip");

        await DownloadFileAsync(_downloadsConfig.OllamaUrl, ollamaZip, "Ollama", cancellationToken);
        ZipFile.ExtractToDirectory(ollamaZip, _ollamaPath, true);
        File.Delete(ollamaZip);
    }

    /// <summary>
    /// Checks yt-dlp version against latest release
    /// </summary>
    private async Task<UpdateResult> CheckYtDlpVersionAsync(string currentVersion)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_downloadsConfig.YtDlpApiUrl);
            var release = JsonSerializer.Deserialize<GitHubRelease>(response);
            
            var latestVersion = release?.TagName ?? string.Empty;
            var needsUpdate = string.IsNullOrEmpty(currentVersion) || currentVersion != latestVersion;

            return new UpdateResult
            {
                IsSuccess = true,
                ComponentName = "yt-dlp",
                OldVersion = currentVersion,
                NewVersion = latestVersion,
                WasUpdated = needsUpdate
            };
        }
        catch (Exception ex)
        {
            return new UpdateResult
            {
                IsSuccess = false,
                ComponentName = "yt-dlp",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Checks Ollama version against latest release
    /// </summary>
    private async Task<UpdateResult> CheckOllamaVersionAsync(string currentVersion)
    {
        try
        {
            var response = await _httpClient.GetStringAsync(_downloadsConfig.OllamaApiUrl);
            var release = JsonSerializer.Deserialize<GitHubRelease>(response);
            
            var latestVersion = release?.TagName ?? string.Empty;
            var needsUpdate = string.IsNullOrEmpty(currentVersion) || currentVersion != latestVersion;

            return new UpdateResult
            {
                IsSuccess = true,
                ComponentName = "ollama",
                OldVersion = currentVersion,
                NewVersion = latestVersion,
                WasUpdated = needsUpdate
            };
        }
        catch (Exception ex)
        {
            return new UpdateResult
            {
                IsSuccess = false,
                ComponentName = "ollama",
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Downloads a file with enhanced progress indication
    /// </summary>
    private async Task DownloadFileAsync(string url, string filePath, string displayName, CancellationToken cancellationToken)
    {
        var progress = new DownloadProgressDisplay(displayName);
        
        try
        {
            progress.Start();

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, _runtimeConfig.BufferSize, true);

            var buffer = new byte[_runtimeConfig.BufferSize];
            var totalRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken);
                totalRead += bytesRead;

                progress.UpdateProgress(totalRead, totalBytes);
            }

            progress.Complete();
        }
        catch (Exception ex)
        {
            progress.Fail(ex.Message);
            throw;
        }
    }

    /// <summary>
    /// Loads version information from cache
    /// </summary>
    private async Task<VersionInfo> LoadVersionInfoAsync()
    {
        if (!File.Exists(_versionsFile))
        {
            return new VersionInfo();
        }

        try
        {
            var json = await File.ReadAllTextAsync(_versionsFile);
            return JsonSerializer.Deserialize<VersionInfo>(json) ?? new VersionInfo();
        }
        catch
        {
            return new VersionInfo();
        }
    }

    /// <summary>
    /// Saves version information to cache
    /// </summary>
    private async Task SaveVersionInfoAsync(VersionInfo versionInfo)
    {
        var json = JsonSerializer.Serialize(versionInfo, new JsonSerializerOptions
        {
            WriteIndented = true
        });
        await File.WriteAllTextAsync(_versionsFile, json);
    }

    /// <summary>
    /// Gets the path to Python executable
    /// </summary>
    public string GetPythonPath() => Path.Combine(_pythonPath, "python.exe");

    /// <summary>
    /// Gets the path to yt-dlp executable
    /// </summary>
    public string GetYtDlpPath() => Path.Combine(_pythonPath, "Scripts", "yt-dlp.exe");

    /// <summary>
    /// Gets the path to Ollama executable
    /// </summary>
    public string GetOllamaPath() => Path.Combine(_ollamaPath, "ollama.exe");

    /// <summary>
    /// Gets the Ollama models directory
    /// </summary>
    public string GetOllamaModelsPath() => Path.Combine(_ollamaPath, "models");
}