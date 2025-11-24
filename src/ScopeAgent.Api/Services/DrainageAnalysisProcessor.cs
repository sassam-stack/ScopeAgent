using Microsoft.Extensions.Logging;
using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Processes drainage plan analysis in the background
/// </summary>
public class DrainageAnalysisProcessor
{
    private readonly ILogger<DrainageAnalysisProcessor> _logger;
    private readonly IAnalysisSessionService _sessionService;
    private readonly IPdfProcessingService _pdfProcessingService;
    private readonly IComputerVisionService _computerVisionService;
    private readonly IImageProcessingService _imageProcessingService;

    public DrainageAnalysisProcessor(
        ILogger<DrainageAnalysisProcessor> logger,
        IAnalysisSessionService sessionService,
        IPdfProcessingService pdfProcessingService,
        IComputerVisionService computerVisionService,
        IImageProcessingService imageProcessingService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _pdfProcessingService = pdfProcessingService;
        _computerVisionService = computerVisionService;
        _imageProcessingService = imageProcessingService;
    }

    public async Task ProcessAnalysisAsync(string analysisId)
    {
        try
        {
            _logger.LogInformation("Starting analysis processing for {AnalysisId}", analysisId);

            var session = await _sessionService.GetSessionAsync(analysisId);
            if (session == null)
            {
                _logger.LogWarning("Session {AnalysisId} not found", analysisId);
                return;
            }

            // Step 1: Convert PDF page to image
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "Converting PDF page to image...",
                ProcessingStage.OcrExtracting.ToString());

            byte[]? pdfBytes = await _sessionService.GetImageAsync(analysisId, "pdf");
            if (pdfBytes == null)
            {
                throw new InvalidOperationException("PDF file not found in session");
            }

            // TODO: Implement PDF to image conversion via Python service
            // For now, we'll skip this step and note it in the status
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "PDF to image conversion not yet implemented. Skipping to OCR...",
                ProcessingStage.OcrExtracting.ToString());

            // Step 2: Perform OCR (if we had the image)
            // For now, we'll extract text from PDF directly
            _logger.LogInformation("Extracting text from PDF page {PageNumber}", session.Request.PlanPageNumber);
            
            var pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
            
            // Step 3: Try to extract structured data from text
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "Analyzing extracted text...",
                ProcessingStage.Analyzing.ToString());

            // Extract basic information from text
            var moduleLabels = ExtractModuleLabelsFromText(pdfText);
            var stLabels = ExtractSTLabelsFromText(pdfText);
            var scaleInfo = ExtractScaleFromText(pdfText);

            _logger.LogInformation("Found {ModuleCount} module labels, {STCount} ST labels", 
                moduleLabels.Count, stLabels.Count);

            // Step 4: Create a basic analysis result
            var result = new AnalysisResult
            {
                PlanId = analysisId,
                PlanPageNumber = session.Request.PlanPageNumber,
                Scale = new ScaleInfo
                {
                    Text = scaleInfo.text ?? "Not found",
                    Ratio = scaleInfo.ratio,
                    Unit = scaleInfo.unit
                },
                Modules = new List<Module>(),
                Pipes = new List<Pipe>(),
                Graph = new ConnectionGraph(),
                Confidence = new OverallConfidence
                {
                    Modules = 0.5,
                    Pipes = 0.3,
                    Connections = 0.0,
                    Specifications = 0.0,
                    Elevations = 0.0
                },
                Ambiguities = new List<Ambiguity>(),
                ProcessingMetadata = new ProcessingMetadata
                {
                    ProcessingTime = 0,
                    OcrConfidence = 0.7,
                    SymbolDetectionConfidence = 0.0,
                    UserValidations = 0,
                    Timestamp = DateTime.UtcNow
                }
            };

            // Add modules found in text
            foreach (var label in moduleLabels)
            {
                result.Modules.Add(new Module
                {
                    Label = label,
                    Metadata = new ModuleMetadata { Confidence = 0.5 }
                });
            }

            // Store result
            await _sessionService.StoreAnalysisResultAsync(analysisId, result);

            // Mark as completed
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Completed,
                $"Analysis completed. Found {moduleLabels.Count} module labels and {stLabels.Count} ST labels in text.",
                ProcessingStage.Completed.ToString());

            _logger.LogInformation("Analysis completed for {AnalysisId}", analysisId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing analysis {AnalysisId}", analysisId);
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Error,
                $"Error during processing: {ex.Message}",
                ProcessingStage.Error.ToString());
        }
    }

    private List<string> ExtractModuleLabelsFromText(string text)
    {
        var labels = new List<string>();
        var pattern = new System.Text.RegularExpressions.Regex(@"\b([SM]-\d+|[SM]\d+)\b", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var matches = pattern.Matches(text);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (!labels.Contains(match.Value, StringComparer.OrdinalIgnoreCase))
            {
                labels.Add(match.Value);
            }
        }
        return labels;
    }

    private List<string> ExtractSTLabelsFromText(string text)
    {
        var labels = new List<string>();
        var pattern = new System.Text.RegularExpressions.Regex(@"\bST\b", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var matches = pattern.Matches(text);
        labels.AddRange(Enumerable.Repeat("ST", matches.Count));
        return labels;
    }

    private (string? text, double? ratio, string? unit) ExtractScaleFromText(string text)
    {
        var scalePatterns = new[]
        {
            new System.Text.RegularExpressions.Regex(@"(\d+)\s*[""']\s*=\s*(\d+)\s*['']", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase),
            new System.Text.RegularExpressions.Regex(@"SCALE\s*:?\s*(\d+)\s*:\s*(\d+)", 
                System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        };

        foreach (var pattern in scalePatterns)
        {
            var match = pattern.Match(text);
            if (match.Success)
            {
                if (double.TryParse(match.Groups[1].Value, out var numerator) &&
                    double.TryParse(match.Groups[2].Value, out var denominator))
                {
                    var ratio = denominator / numerator;
                    var unit = text.Contains("FEET") || text.Contains("'") ? "feet" : "unknown";
                    return (match.Value, ratio, unit);
                }
            }
        }

        return (null, null, null);
    }
}

