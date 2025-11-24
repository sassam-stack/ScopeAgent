namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a detected symbol on the drainage plan
/// </summary>
public class DetectedSymbol
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public SymbolType Type { get; set; } = SymbolType.Unknown;
    public BoundingBox BoundingBox { get; set; } = new();
    
    /// <summary>
    /// Cropped image as base64 string or URL
    /// </summary>
    public string? CroppedImage { get; set; }
    
    /// <summary>
    /// null = not validated yet, true/false = user validation result
    /// </summary>
    public bool? IsModule { get; set; }
    
    /// <summary>
    /// Associated module label (e.g., "S-1", "S-2")
    /// </summary>
    public string? AssociatedModuleLabel { get; set; }
    
    public double Confidence { get; set; }
}

