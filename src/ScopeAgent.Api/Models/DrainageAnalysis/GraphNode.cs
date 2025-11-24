namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a node in the connection graph
/// </summary>
public class GraphNode
{
    public string Id { get; set; } = string.Empty;
    public NodeType Type { get; set; }
    public string? Label { get; set; }
}

/// <summary>
/// Type of graph node
/// </summary>
public enum NodeType
{
    Module,
    External
}

