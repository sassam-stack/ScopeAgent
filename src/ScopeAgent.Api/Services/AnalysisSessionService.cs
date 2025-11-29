using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// In-memory implementation of analysis session service
/// Note: For production, consider using a database or distributed cache
/// </summary>
public class AnalysisSessionService : IAnalysisSessionService
{
    private readonly ILogger<AnalysisSessionService> _logger;
    private readonly ConcurrentDictionary<string, AnalysisSession> _sessions = new();
    private readonly ConcurrentDictionary<string, byte[]> _images = new();
    private readonly ConcurrentDictionary<string, OCRResult> _ocrResults = new();
    private readonly ConcurrentDictionary<string, List<DetectedSymbol>> _detectedSymbols = new();
    private readonly ConcurrentDictionary<string, AnalysisResult> _analysisResults = new();
    private readonly ConcurrentDictionary<string, OuterportResult> _outerportResults = new();

    public AnalysisSessionService(ILogger<AnalysisSessionService> logger)
    {
        _logger = logger;
    }

    public Task<string> CreateSessionAsync(UploadRequest request, byte[] pdfBytes)
    {
        var analysisId = Guid.NewGuid().ToString();
        
        var session = new AnalysisSession
        {
            AnalysisId = analysisId,
            Request = request,
            Status = AnalysisStatus.Processing,
            CurrentStage = ProcessingStage.Uploaded.ToString(),
            Progress = 0
        };

        _sessions.TryAdd(analysisId, session);
        
        // Store PDF bytes as "pdf" image key
        _images.TryAdd($"{analysisId}:pdf", pdfBytes);
        
        _logger.LogInformation("Created analysis session {AnalysisId}", analysisId);
        return Task.FromResult(analysisId);
    }

    public Task<AnalysisSession?> GetSessionAsync(string analysisId)
    {
        _sessions.TryGetValue(analysisId, out var session);
        return Task.FromResult(session);
    }

    public Task UpdateSessionStatusAsync(string analysisId, AnalysisStatus status, string? message = null, string? currentStage = null)
    {
        if (_sessions.TryGetValue(analysisId, out var session))
        {
            session.Status = status;
            session.Message = message;
            session.CurrentStage = currentStage;
            session.UpdatedAt = DateTime.UtcNow;
            
            // Update progress based on stage
            session.Progress = currentStage switch
            {
                nameof(ProcessingStage.Uploaded) => 10,
                nameof(ProcessingStage.OcrExtracting) => 30,
                nameof(ProcessingStage.SymbolDetecting) => 50,
                nameof(ProcessingStage.AwaitingValidation) => 70,
                nameof(ProcessingStage.AwaitingModuleVerification) => 75,
                nameof(ProcessingStage.Analyzing) => 85,
                nameof(ProcessingStage.Completed) => 100,
                _ => session.Progress
            };
            
            _logger.LogInformation("Updated session {AnalysisId} status to {Status}", analysisId, status);
        }
        
        return Task.CompletedTask;
    }

    public Task StoreImageAsync(string analysisId, string imageKey, byte[] imageBytes)
    {
        var key = $"{analysisId}:{imageKey}";
        _images.AddOrUpdate(key, imageBytes, (k, v) => imageBytes);
        _logger.LogInformation("Stored image {ImageKey} for session {AnalysisId}", imageKey, analysisId);
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetImageAsync(string analysisId, string imageKey)
    {
        var key = $"{analysisId}:{imageKey}";
        _images.TryGetValue(key, out var imageBytes);
        return Task.FromResult<byte[]?>(imageBytes);
    }

    public Task StoreOCRResultsAsync(string analysisId, OCRResult ocrResult)
    {
        _ocrResults.AddOrUpdate(analysisId, ocrResult, (k, v) => ocrResult);
        _logger.LogInformation("Stored OCR results for session {AnalysisId}", analysisId);
        return Task.CompletedTask;
    }

    public Task<OCRResult?> GetOCRResultsAsync(string analysisId)
    {
        _ocrResults.TryGetValue(analysisId, out var ocrResult);
        return Task.FromResult(ocrResult);
    }

    public Task StoreDetectedSymbolsAsync(string analysisId, List<DetectedSymbol> symbols)
    {
        _detectedSymbols.AddOrUpdate(analysisId, symbols, (k, v) => symbols);
        _logger.LogInformation("Stored {Count} detected symbols for session {AnalysisId}", symbols.Count, analysisId);
        return Task.CompletedTask;
    }

    public Task<List<DetectedSymbol>> GetDetectedSymbolsAsync(string analysisId)
    {
        _detectedSymbols.TryGetValue(analysisId, out var symbols);
        return Task.FromResult(symbols ?? new List<DetectedSymbol>());
    }

    public Task StoreAnalysisResultAsync(string analysisId, AnalysisResult result)
    {
        _analysisResults.AddOrUpdate(analysisId, result, (k, v) => result);
        _logger.LogInformation("Stored analysis result for session {AnalysisId}", analysisId);
        return Task.CompletedTask;
    }

    public Task<AnalysisResult?> GetAnalysisResultAsync(string analysisId)
    {
        _analysisResults.TryGetValue(analysisId, out var result);
        return Task.FromResult(result);
    }

    public Task CleanupOldSessionsAsync(int hoursOld = 24)
    {
        var cutoffTime = DateTime.UtcNow.AddHours(-hoursOld);
        var sessionsToRemove = _sessions.Values
            .Where(s => s.UpdatedAt < cutoffTime)
            .Select(s => s.AnalysisId)
            .ToList();

        foreach (var analysisId in sessionsToRemove)
        {
            // Remove session
            _sessions.TryRemove(analysisId, out _);
            
            // Remove associated data
            var keysToRemove = _images.Keys.Where(k => k.StartsWith($"{analysisId}:")).ToList();
            foreach (var key in keysToRemove)
            {
                _images.TryRemove(key, out _);
            }
            
            _ocrResults.TryRemove(analysisId, out _);
            _detectedSymbols.TryRemove(analysisId, out _);
            _analysisResults.TryRemove(analysisId, out _);
            _outerportResults.TryRemove(analysisId, out _);
            
            _logger.LogInformation("Cleaned up session {AnalysisId}", analysisId);
        }

        return Task.CompletedTask;
    }

    public Task StoreOuterportResultsAsync(string analysisId, OuterportResult result)
    {
        _outerportResults.AddOrUpdate(analysisId, result, (k, v) => result);
        _logger.LogInformation("Stored Outerport results for session {AnalysisId}", analysisId);
        return Task.CompletedTask;
    }

    public Task<OuterportResult?> GetOuterportResultsAsync(string analysisId)
    {
        _outerportResults.TryGetValue(analysisId, out var result);
        return Task.FromResult(result);
    }
}

