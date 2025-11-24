namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Complete analysis result for a drainage plan
/// </summary>
public class AnalysisResult
{
    public string PlanId { get; set; } = string.Empty;
    public int PlanPageNumber { get; set; }
    public ScaleInfo Scale { get; set; } = new();
    public List<Module> Modules { get; set; } = new();
    public List<Pipe> Pipes { get; set; } = new();
    public ConnectionGraph Graph { get; set; } = new();
    public OverallConfidence Confidence { get; set; } = new();
    public List<Ambiguity> Ambiguities { get; set; } = new();
    public ProcessingMetadata ProcessingMetadata { get; set; } = new();
}

