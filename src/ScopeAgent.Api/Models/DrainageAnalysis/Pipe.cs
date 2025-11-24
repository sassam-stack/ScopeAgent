namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a pipe in the drainage system
/// </summary>
public class Pipe
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public LineSegment Line { get; set; } = new();
    
    /// <summary>
    /// All "ST" labels along this pipe
    /// </summary>
    public List<STLabel> STLabels { get; set; } = new();
    
    public PipeSpecification? Specification { get; set; }
    public FlowDirection? FlowDirection { get; set; }
    
    /// <summary>
    /// Connections to modules
    /// </summary>
    public List<PipeConnection> Connections { get; set; } = new();
    
    public double Confidence { get; set; }
}

