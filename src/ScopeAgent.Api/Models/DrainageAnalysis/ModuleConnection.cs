namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a connection from a module to a pipe
/// </summary>
public class ModuleConnection
{
    public string PipeId { get; set; } = string.Empty;
    public CardinalDirection Direction { get; set; }
    public ConnectionType Type { get; set; }
    public PipeSpecification PipeSpecification { get; set; } = new();
    
    /// <summary>
    /// Invert elevation at this connection
    /// </summary>
    public double? InvertElevation { get; set; }
}

