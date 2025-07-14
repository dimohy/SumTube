using System.Diagnostics;

namespace SumTube.Utils;

/// <summary>
/// Provides enhanced progress display for download operations
/// </summary>
public class DownloadProgressDisplay
{
    private readonly string _fileName;
    private readonly Stopwatch _stopwatch;
    private long _lastBytesRead;
    private DateTime _lastUpdate;
    private readonly object _lock = new();

    public DownloadProgressDisplay(string fileName)
    {
        _fileName = fileName;
        _stopwatch = new Stopwatch();
        _lastUpdate = DateTime.Now;
    }

    /// <summary>
    /// Starts the progress tracking
    /// </summary>
    public void Start()
    {
        _stopwatch.Start();
        _lastUpdate = DateTime.Now;
        Console.WriteLine($"📥 {_fileName} 다운로드를 시작합니다...");
    }

    /// <summary>
    /// Updates progress display with enhanced information
    /// </summary>
    public void UpdateProgress(long bytesRead, long totalBytes)
    {
        lock (_lock)
        {
            var now = DateTime.Now;
            
            // Update every 100ms to avoid console spam
            if ((now - _lastUpdate).TotalMilliseconds < 100)
                return;

            var elapsed = _stopwatch.Elapsed;
            var percentage = totalBytes > 0 ? (int)((bytesRead * 100) / totalBytes) : 0;
            
            // Calculate speed
            var timeDiff = (now - _lastUpdate).TotalSeconds;
            var bytesDiff = bytesRead - _lastBytesRead;
            var speedBps = timeDiff > 0 ? bytesDiff / timeDiff : 0;
            
            // Calculate ETA
            var remainingBytes = totalBytes - bytesRead;
            var eta = speedBps > 0 ? TimeSpan.FromSeconds(remainingBytes / speedBps) : TimeSpan.Zero;
            
            // Format display
            var progressBar = CreateProgressBar(percentage);
            var sizeDisplay = FormatBytes(bytesRead) + "/" + FormatBytes(totalBytes);
            var speedDisplay = FormatBytes((long)speedBps) + "/s";
            var etaDisplay = eta.TotalSeconds > 0 && eta.TotalSeconds < 3600 ? 
                $"{eta.Minutes:D2}:{eta.Seconds:D2}" : "--:--";

            // Clear current line and display progress
            Console.Write($"\r{progressBar} {percentage:D3}% | {sizeDisplay} | {speedDisplay} | ETA: {etaDisplay}");

            _lastBytesRead = bytesRead;
            _lastUpdate = now;
        }
    }

    /// <summary>
    /// Completes the progress display
    /// </summary>
    public void Complete()
    {
        _stopwatch.Stop();
        var totalTime = _stopwatch.Elapsed;
        var avgSpeed = _lastBytesRead > 0 ? _lastBytesRead / totalTime.TotalSeconds : 0;
        
        Console.WriteLine();
        Console.WriteLine($"✅ {_fileName} 다운로드 완료 ({FormatBytes((long)avgSpeed)}/s 평균 속도)");
    }

    /// <summary>
    /// Handles download failure
    /// </summary>
    public void Fail(string error)
    {
        Console.WriteLine();
        Console.WriteLine($"❌ {_fileName} 다운로드 실패: {error}");
    }

    /// <summary>
    /// Creates a visual progress bar
    /// </summary>
    private static string CreateProgressBar(int percentage)
    {
        const int barLength = 20;
        var filledLength = (int)((percentage / 100.0) * barLength);
        var bar = new string('█', filledLength) + new string('░', barLength - filledLength);
        return $"[{bar}]";
    }

    /// <summary>
    /// Formats bytes into human-readable format
    /// </summary>
    private static string FormatBytes(long bytes)
    {
        if (bytes < 0) return "0 B";
        
        string[] suffixes = ["B", "KB", "MB", "GB", "TB"];
        int suffixIndex = 0;
        double size = bytes;

        while (size >= 1024 && suffixIndex < suffixes.Length - 1)
        {
            size /= 1024;
            suffixIndex++;
        }

        return $"{size:F1} {suffixes[suffixIndex]}";
    }
}