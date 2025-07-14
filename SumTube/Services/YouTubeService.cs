using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
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
                    var cleanContent = CleanVttContent(subtitleContent, lang);
                    
                    // Enhanced validation for meaningful content
                    if (IsValidSubtitleContent(cleanContent, lang))
                    {
                        Console.WriteLine($"✅ {lang} 언어 자막을 찾았습니다. (유효한 내용: {cleanContent.Length} 문자)");
                        return cleanContent;
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ {lang} 언어 자막 파일이 있지만 유효한 내용이 부족합니다.");
                    }
                }
                else
                {
                    Console.WriteLine($"❌ {lang} 언어 자막 파일이 생성되지 않았습니다.");
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
    private static string CleanVttContent(string vttContent, string language = "")
    {
        if (string.IsNullOrWhiteSpace(vttContent))
            return string.Empty;

        // Normalize line endings and split
        var lines = Regex.Split(vttContent.Trim(), @"\r?\n|\r", RegexOptions.None);
        var textLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            
            // Skip VTT headers and timing lines
            if (trimmedLine.StartsWith("WEBVTT") ||
                trimmedLine.StartsWith("NOTE") ||
                trimmedLine.StartsWith("Kind:") ||
                trimmedLine.StartsWith("Language:") ||
                trimmedLine.Contains("-->") ||
                string.IsNullOrWhiteSpace(trimmedLine) ||
                trimmedLine.All(char.IsDigit) ||
                IsTimestampLine(trimmedLine))
            {
                continue;
            }

            // Remove HTML tags and formatting
            var cleanLine = Regex.Replace(trimmedLine, "<[^>]*>", "");
            cleanLine = Regex.Replace(cleanLine, @"&\w+;", "");
            
            // Remove common VTT artifacts
            cleanLine = Regex.Replace(cleanLine, @"^\d+$", ""); // Pure number lines
            cleanLine = Regex.Replace(cleanLine, @"^-+$", ""); // Dash lines
            cleanLine = cleanLine.Trim();
            
            if (!string.IsNullOrWhiteSpace(cleanLine) && !IsCommonNoiseText(cleanLine))
            {
                textLines.Add(cleanLine);
            }
        }

        // Join with a space, but also consider language-specific punctuation
        var joinedText = string.Join(" ", textLines);

        // Further clean-up for Korean to handle special cases
        if (language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            // Remove standalone Korean punctuation marks
            joinedText = Regex.Replace(joinedText, @"\s*[.,!?]+\s*", " "); // Common sentence-end punctuation
            joinedText = Regex.Replace(joinedText, @"\s*[-–—]\s*", " "); // Dashes
            joinedText = Regex.Replace(joinedText, @"^\s+|\s+$", ""); // Trim spaces around
        }

        return joinedText;
    }

    /// <summary>
    /// Validates if subtitle content contains meaningful text for the specified language
    /// </summary>
    private static bool IsValidSubtitleContent(string content, string language)
    {
        if (string.IsNullOrWhiteSpace(content))
            return false;

        // Basic length check - subtitle should have minimum meaningful content
        if (content.Length < 20)
            return false;

        // Count meaningful words (not just whitespace and punctuation)
        var words = content.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                          .Where(word => !string.IsNullOrWhiteSpace(word) && 
                                       !Regex.IsMatch(word, @"^[^\w]+$")) // Not just punctuation
                          .ToArray();

        if (words.Length < 5) // Minimum word count
            return false;

        // Language-specific validation
        if (language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateKoreanContent(content, words);
        }
        else if (language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
        {
            return ValidateEnglishContent(content, words);
        }

        // General validation for other languages
        return ValidateGeneralContent(content, words);
    }

    /// <summary>
    /// Validates Korean subtitle content
    /// </summary>
    private static bool ValidateKoreanContent(string content, string[] words)
    {
        // Check for Korean characters (Hangul)
        var koreanCharCount = content.Count(c => c >= 0xAC00 && c <= 0xD7AF);
        var totalCharCount = content.Count(char.IsLetter);
        
        // Should have reasonable amount of Korean characters
        if (totalCharCount > 0 && koreanCharCount / (double)totalCharCount < 0.3)
        {
            Console.WriteLine($"⚠️ 한국어 자막으로 기대했지만 한글 비율이 낮습니다. ({koreanCharCount}/{totalCharCount})");
            return false;
        }

        // Check for common Korean subtitle artifacts or placeholders
        var lowerContent = content.ToLower();
        var commonNoisePatterns = new[]
        {
            "자막이 없습니다", "subtitle not available", "no captions", 
            "자동 생성", "auto-generated", "음성 인식", 
            "번역 불가", "translation unavailable"
        };

        if (commonNoisePatterns.Any(pattern => lowerContent.Contains(pattern)))
        {
            Console.WriteLine($"⚠️ 한국어 자막에 자막 없음을 나타내는 텍스트가 발견되었습니다.");
            return false;
        }

        return koreanCharCount > 10; // At least 10 Korean characters
    }

    /// <summary>
    /// Validates English subtitle content
    /// </summary>
    private static bool ValidateEnglishContent(string content, string[] words)
    {
        // Check for reasonable English word patterns
        var englishWordCount = words.Count(word => Regex.IsMatch(word, @"^[a-zA-Z]+$"));
        
        if (englishWordCount < words.Length * 0.5)
        {
            Console.WriteLine($"⚠️ 영어 자막으로 기대했지만 영어 단어 비율이 낮습니다. ({englishWordCount}/{words.Length})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// General content validation for other languages
    /// </summary>
    private static bool ValidateGeneralContent(string content, string[] words)
    {
        // Basic checks for any language
        var averageWordLength = words.Average(w => w.Length);
        
        // Words that are too short or too long might indicate noise
        if (averageWordLength < 2 || averageWordLength > 20)
        {
            Console.WriteLine($"⚠️ 자막 내용의 평균 단어 길이가 비정상적입니다. ({averageWordLength:F1})");
            return false;
        }

        return true;
    }

    /// <summary>
    /// Checks if a line contains timestamp information
    /// </summary>
    private static bool IsTimestampLine(string line)
    {
        // VTT timestamp format: 00:00:00.000 --> 00:00:03.000
        return Regex.IsMatch(line, @"\d{2}:\d{2}:\d{2}\.\d{3}\s*-->\s*\d{2}:\d{2}:\d{2}\.\d{3}") ||
               Regex.IsMatch(line, @"\d{2}:\d{2}\.\d{3}\s*-->\s*\d{2}:\d{2}\.\d{3}");
    }

    /// <summary>
    /// Checks if text is common noise/placeholder text
    /// </summary>
    private static bool IsCommonNoiseText(string text)
    {
        var lowerText = text.ToLower().Trim();
        
        var noisePatterns = new[]
        {
            "[음악]", "[music]", "[applause]", "[박수]",
            "[웃음]", "[laughter]", "[silence]", "[조용히]",
            "♪", "♫", "♬", "♩", "♭", "♯",
            "...", "…", "---", "***"
        };

        return noisePatterns.Any(pattern => lowerText.Contains(pattern.ToLower())) ||
               lowerText.Length <= 2 ||
               Regex.IsMatch(lowerText, @"^\[.*\]$"); // Text enclosed in brackets
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