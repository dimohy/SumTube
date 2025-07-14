using System.Text.Json.Serialization;

namespace SumTube.Models;

/// <summary>
/// Represents GitHub release information
/// </summary>
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string TagName { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("published_at")]
    public DateTime PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public GitHubAsset[] Assets { get; set; } = [];

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }
}

/// <summary>
/// Represents GitHub release asset
/// </summary>
public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("browser_download_url")]
    public string BrowserDownloadUrl { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}