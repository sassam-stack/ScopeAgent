namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents scale information from the plan
/// </summary>
public class ScaleInfo
{
    /// <summary>
    /// Original scale text (e.g., "1\"=30'")
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    /// <summary>
    /// Calculated scale ratio
    /// </summary>
    public double? Ratio { get; set; }
    
    /// <summary>
    /// Unit (e.g., "feet", "meters")
    /// </summary>
    public string? Unit { get; set; }
}

