namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Metadata associated with a module
/// </summary>
public class ModuleMetadata
{
    /// <summary>
    /// Scale factor from plan
    /// </summary>
    public double? Scale { get; set; }
    
    /// <summary>
    /// North direction angle in degrees (0 = north)
    /// </summary>
    public double? NorthDirection { get; set; }
    
    public double Confidence { get; set; }
}

