namespace SumTube.Models;

/// <summary>
/// Represents the result of an update operation
/// </summary>
public class UpdateResult
{
    public bool IsSuccess { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string OldVersion { get; set; } = string.Empty;
    public string NewVersion { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public bool WasUpdated { get; set; }
}