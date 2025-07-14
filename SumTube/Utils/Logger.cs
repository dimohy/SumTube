namespace SumTube.Utils;

/// <summary>
/// Centralized logging utility with debug mode support
/// </summary>
public static class Logger
{
    private static bool _debugMode = false;
    private static readonly object _lock = new object();

    /// <summary>
    /// Gets or sets the current debug mode state
    /// </summary>
    public static bool IsDebugMode 
    { 
        get 
        { 
            lock (_lock) 
            { 
                return _debugMode; 
            } 
        } 
        set 
        { 
            lock (_lock) 
            { 
                _debugMode = value; 
            } 
        } 
    }

    /// <summary>
    /// Logs an informational message
    /// </summary>
    /// <param name="message">The message to log</param>
    public static void Info(string message)
    {
        Console.WriteLine(message);
    }

    /// <summary>
    /// Logs a debug message only when debug mode is enabled
    /// </summary>
    /// <param name="message">The debug message to log</param>
    public static void Debug(string message)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] DEBUG: {message}");
        }
    }

    /// <summary>
    /// Logs a debug message with category only when debug mode is enabled
    /// </summary>
    /// <param name="category">The category or component name</param>
    /// <param name="message">The debug message to log</param>
    public static void Debug(string category, string message)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] [{category}] {message}");
        }
    }

    /// <summary>
    /// Logs a warning message
    /// </summary>
    /// <param name="message">The warning message to log</param>
    public static void Warning(string message)
    {
        Console.WriteLine($"⚠️ {message}");
    }

    /// <summary>
    /// Logs an error message
    /// </summary>
    /// <param name="message">The error message to log</param>
    public static void Error(string message)
    {
        Console.WriteLine($"❌ {message}");
    }

    /// <summary>
    /// Logs an error with exception details
    /// </summary>
    /// <param name="message">The error message</param>
    /// <param name="exception">The exception to log</param>
    public static void Error(string message, Exception exception)
    {
        Console.WriteLine($"❌ {message}: {exception.Message}");
        if (IsDebugMode && exception.InnerException != null)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] Inner Exception: {exception.InnerException.Message}");
            Console.WriteLine($"🐛 [{timestamp}] Stack Trace: {exception.StackTrace}");
        }
    }

    /// <summary>
    /// Logs HTTP request details in debug mode
    /// </summary>
    /// <param name="method">HTTP method</param>
    /// <param name="url">Request URL</param>
    /// <param name="headers">Optional headers</param>
    public static void DebugHttpRequest(string method, string url, Dictionary<string, string>? headers = null)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] [HTTP] {method} {url}");
            
            if (headers != null && headers.Count > 0)
            {
                foreach (var header in headers)
                {
                    Console.WriteLine($"🐛 [{timestamp}] [HTTP] Header: {header.Key}: {header.Value}");
                }
            }
        }
    }

    /// <summary>
    /// Logs HTTP response details in debug mode
    /// </summary>
    /// <param name="statusCode">HTTP status code</param>
    /// <param name="contentLength">Response content length</param>
    /// <param name="elapsedMs">Request elapsed time in milliseconds</param>
    public static void DebugHttpResponse(int statusCode, long? contentLength, long elapsedMs)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var lengthInfo = contentLength.HasValue ? $" ({contentLength.Value:N0} bytes)" : "";
            Console.WriteLine($"🐛 [{timestamp}] [HTTP] Response: {statusCode}{lengthInfo} in {elapsedMs}ms");
        }
    }

    /// <summary>
    /// Logs process execution details in debug mode
    /// </summary>
    /// <param name="fileName">Process executable name</param>
    /// <param name="arguments">Process arguments</param>
    /// <param name="workingDirectory">Working directory</param>
    public static void DebugProcessStart(string fileName, string arguments, string? workingDirectory = null)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] [PROCESS] Starting: {fileName} {arguments}");
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                Console.WriteLine($"🐛 [{timestamp}] [PROCESS] Working Directory: {workingDirectory}");
            }
        }
    }

    /// <summary>
    /// Logs process exit details in debug mode
    /// </summary>
    /// <param name="fileName">Process executable name</param>
    /// <param name="exitCode">Process exit code</param>
    /// <param name="elapsedMs">Process execution time in milliseconds</param>
    public static void DebugProcessExit(string fileName, int exitCode, long elapsedMs)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var statusEmoji = exitCode == 0 ? "✅" : "❌";
            Console.WriteLine($"🐛 [{timestamp}] [PROCESS] {statusEmoji} {fileName} exited with code {exitCode} after {elapsedMs}ms");
        }
    }

    /// <summary>
    /// Logs file I/O operations in debug mode
    /// </summary>
    /// <param name="operation">Operation type (read, write, delete, etc.)</param>
    /// <param name="filePath">File path</param>
    /// <param name="size">Optional file size</param>
    public static void DebugFileOperation(string operation, string filePath, long? size = null)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var sizeInfo = size.HasValue ? $" ({size.Value:N0} bytes)" : "";
            Console.WriteLine($"🐛 [{timestamp}] [FILE] {operation}: {filePath}{sizeInfo}");
        }
    }

    /// <summary>
    /// Logs configuration details in debug mode
    /// </summary>
    /// <param name="configSection">Configuration section name</param>
    /// <param name="settings">Settings dictionary</param>
    public static void DebugConfiguration(string configSection, Dictionary<string, object> settings)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] [CONFIG] Section '{configSection}':");
            
            foreach (var setting in settings)
            {
                // Mask sensitive values
                var value = IsSensitiveKey(setting.Key) ? "***MASKED***" : setting.Value?.ToString();
                Console.WriteLine($"🐛 [{timestamp}] [CONFIG]   {setting.Key} = {value}");
            }
        }
    }

    /// <summary>
    /// Determines if a configuration key contains sensitive information
    /// </summary>
    /// <param name="key">Configuration key</param>
    /// <returns>True if the key is sensitive</returns>
    private static bool IsSensitiveKey(string key)
    {
        var lowerKey = key.ToLowerInvariant();
        return lowerKey.Contains("password") || 
               lowerKey.Contains("token") || 
               lowerKey.Contains("secret") || 
               lowerKey.Contains("key") && !lowerKey.Contains("apikey");
    }

    /// <summary>
    /// Logs performance metrics in debug mode
    /// </summary>
    /// <param name="operation">Operation name</param>
    /// <param name="elapsedMs">Elapsed time in milliseconds</param>
    /// <param name="additionalMetrics">Additional metrics</param>
    public static void DebugPerformance(string operation, long elapsedMs, Dictionary<string, object>? additionalMetrics = null)
    {
        if (IsDebugMode)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"🐛 [{timestamp}] [PERF] {operation}: {elapsedMs}ms");
            
            if (additionalMetrics != null)
            {
                foreach (var metric in additionalMetrics)
                {
                    Console.WriteLine($"🐛 [{timestamp}] [PERF]   {metric.Key}: {metric.Value}");
                }
            }
        }
    }
}