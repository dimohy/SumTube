using System.Text.Json.Serialization;

namespace SumTube.Configuration;

/// <summary>
/// Root configuration model for SumTube application
/// </summary>
public class SumTubeConfig
{
    [JsonPropertyName("Ollama")]
    public OllamaConfig Ollama { get; set; } = new();

    [JsonPropertyName("Downloads")]
    public DownloadsConfig Downloads { get; set; } = new();

    [JsonPropertyName("Updates")]
    public UpdatesConfig Updates { get; set; } = new();

    [JsonPropertyName("YouTube")]
    public YouTubeConfig YouTube { get; set; } = new();

    [JsonPropertyName("Runtime")]
    public RuntimeConfig Runtime { get; set; } = new();
}

/// <summary>
/// Configuration for Ollama AI model settings
/// </summary>
public class OllamaConfig
{
    [JsonPropertyName("Port")]
    public int Port { get; set; } = 11435;

    [JsonPropertyName("DefaultModel")]
    public string DefaultModel { get; set; } = "exaone3.5:7.8b";

    [JsonPropertyName("ServerStartupTimeoutSeconds")]
    public int ServerStartupTimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("ServerReadyTimeoutSeconds")]
    public int ServerReadyTimeoutSeconds { get; set; } = 5;

    [JsonPropertyName("ServerShutdownTimeoutSeconds")]
    public int ServerShutdownTimeoutSeconds { get; set; } = 10;

    [JsonPropertyName("ModelDownloadTimeoutMinutes")]
    public int ModelDownloadTimeoutMinutes { get; set; } = 30;

    [JsonPropertyName("ConnectionTimeoutMinutes")]
    public int ConnectionTimeoutMinutes { get; set; } = 10;

    [JsonPropertyName("ModelValidation")]
    public ModelValidationConfig ModelValidation { get; set; } = new();

    [JsonPropertyName("ApiOptions")]
    public OllamaApiOptions ApiOptions { get; set; } = new();
}

/// <summary>
/// Configuration for model validation settings
/// </summary>
public class ModelValidationConfig
{
    [JsonPropertyName("EnableIntegrityCheck")]
    public bool EnableIntegrityCheck { get; set; } = true;

    [JsonPropertyName("EnableFunctionalTest")]
    public bool EnableFunctionalTest { get; set; } = true;

    [JsonPropertyName("TestPrompt")]
    public string TestPrompt { get; set; } = "æ»≥Á«œººø‰";

    [JsonPropertyName("ExpectedResponseLength")]
    public int ExpectedResponseLength { get; set; } = 5;

    [JsonPropertyName("ValidationTimeoutSeconds")]
    public int ValidationTimeoutSeconds { get; set; } = 30;

    [JsonPropertyName("RetryAttempts")]
    public int RetryAttempts { get; set; } = 2;
}

/// <summary>
/// Configuration for Ollama API options
/// </summary>
public class OllamaApiOptions
{
    [JsonPropertyName("Temperature")]
    public float Temperature { get; set; } = 0.3f;

    [JsonPropertyName("TopP")]
    public float TopP { get; set; } = 0.9f;

    [JsonPropertyName("MaxTokens")]
    public int MaxTokens { get; set; } = 4096;
}

/// <summary>
/// Configuration for download URLs and endpoints
/// </summary>
public class DownloadsConfig
{
    [JsonPropertyName("PythonEmbeddedUrl")]
    public string PythonEmbeddedUrl { get; set; } = "https://www.python.org/ftp/python/3.12.0/python-3.12.0-embed-amd64.zip";

    [JsonPropertyName("GetPipUrl")]
    public string GetPipUrl { get; set; } = "https://bootstrap.pypa.io/get-pip.py";

    [JsonPropertyName("OllamaUrl")]
    public string OllamaUrl { get; set; } = "https://github.com/ollama/ollama/releases/latest/download/ollama-windows-amd64.zip";

    [JsonPropertyName("YtDlpApiUrl")]
    public string YtDlpApiUrl { get; set; } = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";

    [JsonPropertyName("OllamaApiUrl")]
    public string OllamaApiUrl { get; set; } = "https://api.github.com/repos/ollama/ollama/releases/latest";
}

/// <summary>
/// Configuration for update checking and caching
/// </summary>
public class UpdatesConfig
{
    [JsonPropertyName("CheckIntervalHours")]
    public int CheckIntervalHours { get; set; } = 24;

    [JsonPropertyName("RetryAttempts")]
    public int RetryAttempts { get; set; } = 3;

    [JsonPropertyName("RetryDelaySeconds")]
    public int RetryDelaySeconds { get; set; } = 5;
}

/// <summary>
/// Configuration for YouTube processing
/// </summary>
public class YouTubeConfig
{
    [JsonPropertyName("SubtitleLanguagePriority")]
    public string[] SubtitleLanguagePriority { get; set; } = ["ko", "en", "en.*"];

    [JsonPropertyName("MaxTranscriptLength")]
    public int MaxTranscriptLength { get; set; } = 150000;

    [JsonPropertyName("TempDirectoryPrefix")]
    public string TempDirectoryPrefix { get; set; } = "sumtube_";
}

/// <summary>
/// Configuration for runtime environment
/// </summary>
public class RuntimeConfig
{
    [JsonPropertyName("PythonVersion")]
    public string PythonVersion { get; set; } = "3.12.0";

    [JsonPropertyName("BufferSize")]
    public int BufferSize { get; set; } = 8192;

    [JsonPropertyName("MaxDownloadRetries")]
    public int MaxDownloadRetries { get; set; } = 3;
}