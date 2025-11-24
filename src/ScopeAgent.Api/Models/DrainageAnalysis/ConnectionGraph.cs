namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents the connection graph of modules and pipes
/// </summary>
public class ConnectionGraph
{
    public List<GraphNode> Nodes { get; set; } = new();
    public List<GraphEdge> Edges { get; set; } = new();
}

