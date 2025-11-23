using ScopeAgent.Api.Models;

namespace ScopeAgent.Api.Services;

public interface IAiAnalysisService
{
    Task<AnalysisResponse> AnalyzeDrainagePlanAsync(string pdfText, List<int> drainagePages);
    Task<string> TestConnectionAsync();
}

