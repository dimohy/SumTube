using System.Text.Json.Serialization;

namespace SumTube.Models;

/// <summary>
/// Represents version information for runtime components
/// </summary>
public class VersionInfo
{
    [JsonPropertyName("python_version")]
    public string PythonVersion { get; set; } = string.Empty;

    [JsonPropertyName("yt_dlp_version")]
    public string YtDlpVersion { get; set; } = string.Empty;

    [JsonPropertyName("ollama_version")]
    public string OllamaVersion { get; set; } = string.Empty;

    [JsonPropertyName("last_checked")]
    public DateTime LastChecked { get; set; } = DateTime.MinValue;

    [JsonPropertyName("model_version")]
    public string ModelVersion { get; set; } = "exaone3.5:7.8b";
}