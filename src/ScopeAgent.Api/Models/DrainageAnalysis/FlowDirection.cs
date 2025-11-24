namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Flow direction types
/// </summary>
public enum FlowDirectionType
{
    Incoming,
    Outgoing,
    Bidirectional,
    Unknown
}

/// <summary>
/// Represents flow direction information for a pipe
/// </summary>
public class FlowDirection
{
    public FlowDirectionType Direction { get; set; } = FlowDirectionType.Unknown;
    
    /// <summary>
    /// Arrows indicating flow direction
    /// </summary>
    public List<Arrow> Arrows { get; set; } = new();
    
    /// <summary>
    /// Source module ID if known
    /// </summary>
    public string? FromModule { get; set; }
    
    /// <summary>
    /// Target module ID if known
    /// </summary>
    public string? ToModule { get; set; }
}

