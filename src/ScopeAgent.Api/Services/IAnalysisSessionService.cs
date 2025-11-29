using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Service for managing analysis sessions and temporary storage
/// </summary>
public interface IAnalysisSessionService
{
    /// <summary>
    /// Create a new analysis session
    /// </summary>
    Task<string> CreateSessionAsync(UploadRequest request, byte[] pdfBytes);

    /// <summary>
    /// Get analysis session by ID
    /// </summary>
    Task<AnalysisSession?> GetSessionAsync(string analysisId);

    /// <summary>
    /// Update session status
    /// </summary>
    Task UpdateSessionStatusAsync(string analysisId, AnalysisStatus status, string? message = null, string? currentStage = null);

    /// <summary>
    /// Store image for a session
    /// </summary>
    Task StoreImageAsync(string analysisId, string imageKey, byte[] imageBytes);

    /// <summary>
    /// Get stored image
    /// </summary>
    Task<byte[]?> GetImageAsync(string analysisId, string imageKey);

    /// <summary>
    /// Store OCR results for a session
    /// </summary>
    Task StoreOCRResultsAsync(string analysisId, OCRResult ocrResult);

    /// <summary>
    /// Get stored OCR results
    /// </summary>
    Task<OCRResult?> GetOCRResultsAsync(string analysisId);

    /// <summary>
    /// Store detected symbols
    /// </summary>
    Task StoreDetectedSymbolsAsync(string analysisId, List<DetectedSymbol> symbols);

    /// <summary>
    /// Get stored detected symbols
    /// </summary>
    Task<List<DetectedSymbol>> GetDetectedSymbolsAsync(string analysisId);

    /// <summary>
    /// Store analysis result
    /// </summary>
    Task StoreAnalysisResultAsync(string analysisId, AnalysisResult result);

    /// <summary>
    /// Get analysis result
    /// </summary>
    Task<AnalysisResult?> GetAnalysisResultAsync(string analysisId);

    /// <summary>
    /// Store Outerport results
    /// </summary>
    Task StoreOuterportResultsAsync(string analysisId, OuterportResult result);

    /// <summary>
    /// Get stored Outerport results
    /// </summary>
    Task<OuterportResult?> GetOuterportResultsAsync(string analysisId);

    /// <summary>
    /// Clean up old sessions (older than specified hours)
    /// </summary>
    Task CleanupOldSessionsAsync(int hoursOld = 24);
}

/// <summary>
/// Represents an analysis session
/// </summary>
public class AnalysisSession
{
    public string AnalysisId { get; set; } = string.Empty;
    public UploadRequest Request { get; set; } = new();
    public AnalysisStatus Status { get; set; }
    public string? Message { get; set; }
    public string? CurrentStage { get; set; }
    public int Progress { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

