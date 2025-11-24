namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents an arrow indicating direction
/// </summary>
public class Arrow
{
    public Point StartPoint { get; set; } = new();
    public Point EndPoint { get; set; } = new();
    
    /// <summary>
    /// Normalized direction vector
    /// </summary>
    public Point Direction { get; set; } = new();
}

