namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents an "ST" label found on a pipe
/// </summary>
public class STLabel
{
    public string Text { get; set; } = "ST";
    public BoundingBox BoundingBox { get; set; } = new();
    
    /// <summary>
    /// Center position of the label
    /// </summary>
    public Point Position { get; set; } = new();
    
    public double Confidence { get; set; }
}

