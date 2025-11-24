namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a module in the drainage system
/// </summary>
public class Module
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Module label (e.g., "S-1", "S-2")
    /// </summary>
    public string Label { get; set; } = string.Empty;
    
    public DetectedSymbol Symbol { get; set; } = new();
    public Point Location { get; set; } = new();
    public BoundingBox BoundingBox { get; set; } = new();
    public ElevationData Elevations { get; set; } = new();
    
    /// <summary>
    /// Connections to pipes
    /// </summary>
    public List<ModuleConnection> Connections { get; set; } = new();
    
    public ModuleMetadata Metadata { get; set; } = new();
}

