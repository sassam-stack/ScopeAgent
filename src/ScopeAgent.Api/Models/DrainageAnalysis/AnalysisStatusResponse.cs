namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Response for analysis status check
/// </summary>
public class AnalysisStatusResponse
{
    public string AnalysisId { get; set; } = string.Empty;
    public AnalysisStatus Status { get; set; }
    public int Progress { get; set; } // 0-100
    public string? Message { get; set; }
    public string? CurrentStage { get; set; }
}

/// <summary>
/// Processing stages
/// </summary>
public enum ProcessingStage
{
    Uploaded,
    OcrExtracting,
    SymbolDetecting,
    AwaitingValidation,
    AwaitingModuleVerification,
    Analyzing,
    Completed,
    Error
}

