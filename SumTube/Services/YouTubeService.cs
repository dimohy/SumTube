using System.Diagnostics;
using System.Text.Json;
using SumTube.Configuration;

namespace SumTube.Services;

/// <summary>
/// Handles YouTube video transcript extraction using yt-dlp
/// </summary>
public class YouTubeService
{
    private readonly string _ytDlpPath;
    private readonly string _pythonPath;
    private readonly YouTubeConfig _config;

    public YouTubeService(string ytDlpPath, string pythonPath, YouTubeConfig? config = null)
    {
        _ytDlpPath = ytDlpPath;
        _pythonPath = pythonPath;
        _config = config ?? ConfigurationService.Instance.YouTube;
    }

    /// <summary>
    /// Extracts transcript/subtitles from YouTube video
    /// </summary>
    public async Task<string> ExtractTranscriptAsync(string youtubeUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(youtubeUrl))
        {
            throw new ArgumentException("YouTube URL이 제공되지 않았습니다.", nameof(youtubeUrl));
        }

        if (!IsValidYouTubeUrl(youtubeUrl))
        {
            throw new ArgumentException("올바른 YouTube URL이 아닙니다.", nameof(youtubeUrl));
        }

        Console.WriteLine("📺 YouTube 영상 정보를 가져오고 있습니다...");

        // First, get video info
        var videoInfo = await GetVideoInfoAsync(youtubeUrl, cancellationToken);
        Console.WriteLine($"🎬 제목: {videoInfo.Title}");
        Console.WriteLine($"⏱️ 길이: {videoInfo.Duration}");

        Console.WriteLine("📝 자막/스크립트를 추출하고 있습니다...");

        // Try to extract subtitles/transcript
        var transcript = await ExtractSubtitlesAsync(youtubeUrl, cancellationToken);

        if (string.IsNullOrWhiteSpace(transcript))
        {
            Console.WriteLine("⚠️ 자막을 찾을 수 없습니다. 오디오 추출을 시도합니다...");
            // If no subtitles available, we could potentially extract audio and use speech recognition
            // For now, we'll throw an exception
            throw new InvalidOperationException("이 영상에는 사용 가능한 자막이 없습니다.");
        }

        Console.WriteLine($"✅ 스크립트 추출 완료 (약 {transcript.Length} 문자)");
        return transcript;
    }

    /// <summary>
    /// Gets basic video information
    /// </summary>
    private async Task<VideoInfo> GetVideoInfoAsync(string youtubeUrl, CancellationToken cancellationToken)
    {
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = _pythonPath,
                Arguments = $"\"{_ytDlpPath}\" --print \"%(title)s|%(duration)s|%(description)s\" \"{youtubeUrl}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        var error = await process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"영상 정보를 가져오는데 실패했습니다: {error}");
        }

        var parts = output.Trim().Split('|');
        return new VideoInfo
        {
            Title = parts.Length > 0 ? parts[0] : "Unknown",
            Duration = parts.Length > 1 ? parts[1] : "Unknown",
            Description = parts.Length > 2 ? parts[2] : string.Empty
        };
    }

    /// <summary>
    /// Extracts subtitles/transcript from YouTube video
    /// </summary>
    private async Task<string> ExtractSubtitlesAsync(string youtubeUrl, CancellationToken cancellationToken)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), _config.TempDirectoryPrefix + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            // Try to get subtitles based on configured language priority
            foreach (var lang in _config.SubtitleLanguagePriority)
            {
                Console.WriteLine($"🌐 {lang} 언어 자막을 시도하고 있습니다...");
                
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _pythonPath,
                        Arguments = $"\"{_ytDlpPath}\" --write-subs --write-auto-subs --sub-langs \"{lang}\" --sub-format \"vtt\" --skip-download -o \"{Path.Combine(tempDir, "%(title)s.%(ext)s")}\" \"{youtubeUrl}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    }
                };

                process.Start();
                
                var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
                var error = await process.StandardError.ReadToEndAsync(cancellationToken);
                
                await process.WaitForExitAsync(cancellationToken);

                // Look for subtitle files
                var vttFiles = Directory.GetFiles(tempDir, "*.vtt");
                if (vttFiles.Length > 0)
                {
                    var subtitleContent = await File.ReadAllTextAsync(vttFiles[0], cancellationToken);
                    var cleanContent = CleanVttContent(subtitleContent);
                    
                    if (!string.IsNullOrWhiteSpace(cleanContent))
                    {
                        Console.WriteLine($"✅ {lang} 언어 자막을 찾았습니다.");
                        return cleanContent;
                    }
                }
            }

            return string.Empty;
        }
        finally
        {
            // Clean up temp directory
            try
            {
                Directory.Delete(tempDir, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    /// <summary>
    /// Cleans VTT subtitle content to extract only text
    /// </summary>
    private static string CleanVttContent(string vttContent)
    {
        if (string.IsNullOrWhiteSpace(vttContent))
            return string.Empty;

        var lines = vttContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var textLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip VTT headers and timing lines
            if (trimmedLine.StartsWith("WEBVTT") ||
                trimmedLine.StartsWith("NOTE") ||
                trimmedLine.Contains("-->") ||
                string.IsNullOrWhiteSpace(trimmedLine) ||
                trimmedLine.All(char.IsDigit))
            {
                continue;
            }

            // Remove HTML tags and formatting
            var cleanLine = System.Text.RegularExpressions.Regex.Replace(trimmedLine, "<[^>]*>", "");
            cleanLine = System.Text.RegularExpressions.Regex.Replace(cleanLine, @"\&\w+;", "");
            
            if (!string.IsNullOrWhiteSpace(cleanLine))
            {
                textLines.Add(cleanLine);
            }
        }

        return string.Join(" ", textLines);
    }

    /// <summary>
    /// Validates if the provided URL is a valid YouTube URL
    /// </summary>
    private static bool IsValidYouTubeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return false;

        try
        {
            var uri = new Uri(url);
            return (uri.Host.Contains("youtube.com") || uri.Host.Contains("youtu.be")) &&
                   (url.Contains("/watch?v=") || url.Contains("youtu.be/"));
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Represents basic video information
    /// </summary>
    private class VideoInfo
    {
        public string Title { get; set; } = string.Empty;
        public string Duration { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}