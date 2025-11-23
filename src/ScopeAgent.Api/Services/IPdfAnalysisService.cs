using Azure.AI.DocumentIntelligence;

namespace ScopeAgent.Api.Services;

public interface IPdfAnalysisService
{
    Task<string> ExtractTextFromPdfAsync(byte[] pdfBytes);
    Task<List<int>> IdentifyDrainagePagesAsync(byte[] pdfBytes);
    Task<AnalyzeResult?> AnalyzeDocumentRawAsync(byte[] pdfBytes);
}

