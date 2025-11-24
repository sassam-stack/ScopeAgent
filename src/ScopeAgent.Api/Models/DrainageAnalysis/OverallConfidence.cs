namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents overall confidence scores for the analysis
/// </summary>
public class OverallConfidence
{
    /// <summary>
    /// Module detection confidence (0-1)
    /// </summary>
    public double Modules { get; set; }
    
    /// <summary>
    /// Pipe detection confidence (0-1)
    /// </summary>
    public double Pipes { get; set; }
    
    /// <summary>
    /// Connection association confidence (0-1)
    /// </summary>
    public double Connections { get; set; }
    
    /// <summary>
    /// Specification extraction confidence (0-1)
    /// </summary>
    public double Specifications { get; set; }
    
    /// <summary>
    /// Elevation extraction confidence (0-1)
    /// </summary>
    public double Elevations { get; set; }
    
    /// <summary>
    /// Average confidence across all categories
    /// </summary>
    public double Average => (Modules + Pipes + Connections + Specifications + Elevations) / 5.0;
}

