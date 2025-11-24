namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Metadata about the processing operation
/// </summary>
public class ProcessingMetadata
{
    /// <summary>
    /// Processing time in milliseconds
    /// </summary>
    public long ProcessingTime { get; set; }
    
    /// <summary>
    /// OCR confidence score
    /// </summary>
    public double OcrConfidence { get; set; }
    
    /// <summary>
    /// Symbol detection confidence score
    /// </summary>
    public double SymbolDetectionConfidence { get; set; }
    
    /// <summary>
    /// Number of user validations performed
    /// </summary>
    public int UserValidations { get; set; }
    
    /// <summary>
    /// Timestamp of processing
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

