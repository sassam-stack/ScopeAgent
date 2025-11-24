namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a connection between a pipe and a module
/// </summary>
public class PipeConnection
{
    public string ModuleId { get; set; } = string.Empty;
    public Point ConnectionPoint { get; set; } = new();
    public CardinalDirection Direction { get; set; }
    public ConnectionType Type { get; set; }
}

/// <summary>
/// Type of connection
/// </summary>
public enum ConnectionType
{
    Incoming,
    Outgoing
}

