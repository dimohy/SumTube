namespace SumTube.Models;

/// <summary>
/// Represents the result of model validation
/// </summary>
public class ModelValidationResult
{
    /// <summary>
    /// Gets or sets whether the validation was successful
    /// </summary>
    public bool IsValid { get; set; }

    /// <summary>
    /// Gets or sets the model name that was validated
    /// </summary>
    public string ModelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the model exists in the system
    /// </summary>
    public bool ModelExists { get; set; }

    /// <summary>
    /// Gets or sets whether the model information could be retrieved
    /// </summary>
    public bool InfoRetrieved { get; set; }

    /// <summary>
    /// Gets or sets whether the functional test passed
    /// </summary>
    public bool FunctionalTestPassed { get; set; }

    /// <summary>
    /// Gets or sets the validation error message if any
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the time taken for validation
    /// </summary>
    public TimeSpan ValidationTime { get; set; }

    /// <summary>
    /// Gets or sets whether the model was re-downloaded
    /// </summary>
    public bool WasRedownloaded { get; set; }

    /// <summary>
    /// Gets or sets the test response from the model
    /// </summary>
    public string? TestResponse { get; set; }

    /// <summary>
    /// Gets or sets model information details
    /// </summary>
    public ModelInfo? ModelInfo { get; set; }
}

/// <summary>
/// Represents detailed information about a model
/// </summary>
public class ModelInfo
{
    /// <summary>
    /// Gets or sets the model name
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the model size in bytes
    /// </summary>
    public long Size { get; set; }

    /// <summary>
    /// Gets or sets the model's parameter count
    /// </summary>
    public string? Parameters { get; set; }

    /// <summary>
    /// Gets or sets the model family
    /// </summary>
    public string? Family { get; set; }

    /// <summary>
    /// Gets or sets when the model was created
    /// </summary>
    public DateTime? CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the model's digest/hash for integrity checking
    /// </summary>
    public string? Digest { get; set; }
}