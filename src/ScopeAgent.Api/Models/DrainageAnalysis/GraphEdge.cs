namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents an edge in the connection graph
/// </summary>
public class GraphEdge
{
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public string PipeId { get; set; } = string.Empty;
    public PipeSpecification Metadata { get; set; } = new();
}

