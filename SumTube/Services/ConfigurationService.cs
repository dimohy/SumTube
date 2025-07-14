using System.Text.Json;
using SumTube.Configuration;

namespace SumTube.Services;

/// <summary>
/// Manages application configuration loading and access
/// </summary>
public class ConfigurationService
{
    private static ConfigurationService? _instance;
    private static readonly object _lock = new();
    private SumTubeConfig? _config;
    private readonly string _configPath;

    private ConfigurationService()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        _configPath = Path.Combine(basePath, "appsettings.json");
    }

    /// <summary>
    /// Gets the singleton instance of ConfigurationService
    /// </summary>
    public static ConfigurationService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ConfigurationService();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Loads configuration from appsettings.json
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(_configPath))
            {
                Console.WriteLine("?? ���� ������ ã�� �� �����ϴ�. �⺻ ������ ����մϴ�.");
                _config = new SumTubeConfig();
                await SaveDefaultConfigAsync();
                return;
            }

            var json = await File.ReadAllTextAsync(_configPath);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                ReadCommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true
            };

            _config = JsonSerializer.Deserialize<SumTubeConfig>(json, options) ?? new SumTubeConfig();
            Console.WriteLine("? ���� ������ ���������� �ε��߽��ϴ�.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? ���� ���� �ε� �� ����: {ex.Message}");
            Console.WriteLine("�⺻ ������ ����մϴ�.");
            _config = new SumTubeConfig();
        }
    }

    /// <summary>
    /// Saves default configuration to file
    /// </summary>
    private async Task SaveDefaultConfigAsync()
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var json = JsonSerializer.Serialize(_config, options);
            await File.WriteAllTextAsync(_configPath, json);
            Console.WriteLine("?? �⺻ ���� ������ �����߽��ϴ�.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"?? ���� ���� ���� �� ����: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the current configuration
    /// </summary>
    public SumTubeConfig Config
    {
        get
        {
            if (_config == null)
            {
                LoadAsync().Wait();
            }
            return _config ?? new SumTubeConfig();
        }
    }

    /// <summary>
    /// Gets Ollama configuration
    /// </summary>
    public OllamaConfig Ollama => Config.Ollama;

    /// <summary>
    /// Gets Downloads configuration
    /// </summary>
    public DownloadsConfig Downloads => Config.Downloads;

    /// <summary>
    /// Gets Updates configuration
    /// </summary>
    public UpdatesConfig Updates => Config.Updates;

    /// <summary>
    /// Gets YouTube configuration
    /// </summary>
    public YouTubeConfig YouTube => Config.YouTube;

    /// <summary>
    /// Gets Runtime configuration
    /// </summary>
    public RuntimeConfig Runtime => Config.Runtime;

    /// <summary>
    /// Reloads configuration from file
    /// </summary>
    public async Task ReloadAsync()
    {
        await LoadAsync();
    }

    /// <summary>
    /// Gets a TimeSpan from seconds
    /// </summary>
    public static TimeSpan SecondsToTimeSpan(int seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>
    /// Gets a TimeSpan from minutes
    /// </summary>
    public static TimeSpan MinutesToTimeSpan(int minutes) => TimeSpan.FromMinutes(minutes);

    /// <summary>
    /// Gets a TimeSpan from hours
    /// </summary>
    public static TimeSpan HoursToTimeSpan(int hours) => TimeSpan.FromHours(hours);
}