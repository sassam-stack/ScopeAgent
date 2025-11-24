namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Response after uploading a PDF for analysis
/// </summary>
public class UploadResponse
{
    public string AnalysisId { get; set; } = string.Empty;
    public AnalysisStatus Status { get; set; }
    public List<DetectedSymbol>? DetectedSymbols { get; set; }
    public string? Message { get; set; }
}

/// <summary>
/// Status of the analysis
/// </summary>
public enum AnalysisStatus
{
    Processing,
    ReadyForValidation,
    Completed,
    Error
}

