using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

public interface IComputerVisionService
{
    Task<object?> AnalyzeImageAsync(byte[] imageBytes);
    Task<object?> ReadTextAsync(byte[] imageBytes);
    
    /// <summary>
    /// Read text from image and return structured OCR result
    /// </summary>
    Task<OCRResult?> ReadTextStructuredAsync(byte[] imageBytes);
}

