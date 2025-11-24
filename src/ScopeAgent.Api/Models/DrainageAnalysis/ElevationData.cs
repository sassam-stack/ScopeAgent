namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents elevation data for a module
/// </summary>
public class ElevationData
{
    /// <summary>
    /// Ground Elevation
    /// </summary>
    public double? GroundElevation { get; set; }
    
    /// <summary>
    /// Invert Elevation (can be multiple, one per connection)
    /// </summary>
    public List<double> InvertElevations { get; set; } = new();
    
    /// <summary>
    /// Edge of Pavement elevation
    /// </summary>
    public double? EdgeOfPavement { get; set; }
}

