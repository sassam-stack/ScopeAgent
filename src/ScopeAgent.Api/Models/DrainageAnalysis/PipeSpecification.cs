namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents pipe specification information
/// </summary>
public class PipeSpecification
{
    /// <summary>
    /// Diameter in inches
    /// </summary>
    public double? Diameter { get; set; }
    
    /// <summary>
    /// Material type (e.g., "RCP", "HDPE")
    /// </summary>
    public string? Material { get; set; }
    
    /// <summary>
    /// Original text from OCR (e.g., "18\" RCP")
    /// </summary>
    public string Text { get; set; } = string.Empty;
    
    public BoundingBox BoundingBox { get; set; } = new();
    
    /// <summary>
    /// Arrow pointing to the pipe (if present)
    /// </summary>
    public Arrow? Arrow { get; set; }
}

