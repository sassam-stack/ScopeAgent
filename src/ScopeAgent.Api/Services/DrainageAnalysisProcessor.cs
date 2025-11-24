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

            // Step 1: Convert PDF page to image
            byte[]? planPageImage = await _sessionService.GetImageAsync(analysisId, "planPage");
            
            if (planPageImage == null)
            {
                // Try to convert PDF page to image
                try
                {
                    _logger.LogInformation("Converting PDF page {PageNumber} to image", session.Request.PlanPageNumber);
                    planPageImage = await _pdfProcessingService.ConvertPageToImageAsync(
                        pdfBytes, 
                        session.Request.PlanPageNumber, 
                        dpi: 300
                    );
                    
                    // Store the converted image
                    await _sessionService.StoreImageAsync(analysisId, "planPage", planPageImage);
                    
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.Processing,
                        "Plan page image converted. Starting OCR extraction...",
                        ProcessingStage.OcrExtracting.ToString());
                }
                catch (NotImplementedException ex)
                {
                    // PDF to image conversion not yet implemented
                    // Note: Image will not be available for preview, but OCR will run on PDF directly
                    _logger.LogWarning(ex, "PDF to image conversion not available. Will use OCR on PDF directly. Plan page image will not be available for preview.");
                    planPageImage = null; // Ensure it's null so we don't try to use it
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.Processing,
                        "PDF to image conversion not available. Running OCR on PDF directly...",
                        ProcessingStage.OcrExtracting.ToString());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting PDF page to image. Plan page image will not be available for preview.");
                    planPageImage = null; // Ensure it's null so we don't try to use it
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.Processing,
                        $"Error converting PDF to image: {ex.Message}. Using OCR on PDF directly...",
                        ProcessingStage.OcrExtracting.ToString());
                }
            }
            else
            {
                await _sessionService.UpdateSessionStatusAsync(
                    analysisId,
                    AnalysisStatus.Processing,
                    "Plan page image loaded. Starting OCR extraction...",
                    ProcessingStage.OcrExtracting.ToString());
            }

            // Step 2: Perform OCR
            _logger.LogInformation("Extracting text from PDF page {PageNumber}", session.Request.PlanPageNumber);
            
            OCRResult? ocrResult = null;
            string pdfText;
            
            // Try OCR on image if available, otherwise try OCR on PDF directly
            if (planPageImage != null)
            {
                try
                {
                    _logger.LogInformation("Performing OCR on plan page image");
                    ocrResult = await _computerVisionService.ReadTextStructuredAsync(planPageImage);
                    
                    if (ocrResult != null && ocrResult.Pages != null && ocrResult.Pages.Count > 0)
                    {
                        // Convert OCR result to text for text-based extraction
                        pdfText = ExtractTextFromOCRResult(ocrResult);
                        
                        _logger.LogInformation("OCR completed. Found {LineCount} lines", 
                            ocrResult.Pages.FirstOrDefault()?.Lines.Count ?? 0);
                    }
                    else
                    {
                        _logger.LogWarning("OCR returned null or empty, falling back to PDF text extraction");
                        pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OCR on image failed, trying OCR on PDF directly");
                    // Try OCR on PDF directly
                    try
                    {
                        ocrResult = await _computerVisionService.ReadTextFromPdfAsync(pdfBytes);
                        if (ocrResult != null && ocrResult.Pages != null && ocrResult.Pages.Count > 0)
                        {
                            // Filter to only the requested page
                            var requestedPage = ocrResult.Pages.FirstOrDefault(p => p.PageNumber == session.Request.PlanPageNumber);
                            if (requestedPage != null)
                            {
                                ocrResult = new OCRResult
                                {
                                    Pages = new List<OCRPage> { requestedPage }
                                };
                            }
                            else
                            {
                                // Use first page if exact match not found
                                ocrResult = new OCRResult
                                {
                                    Pages = ocrResult.Pages.Take(1).ToList()
                                };
                            }
                            
                            pdfText = ExtractTextFromOCRResult(ocrResult);
                            _logger.LogInformation("OCR on PDF completed. Found {LineCount} lines", 
                                ocrResult.Pages.FirstOrDefault()?.Lines.Count ?? 0);
                        }
                        else
                        {
                            _logger.LogWarning("OCR on PDF returned null or empty, falling back to text extraction");
                            pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
                        }
                    }
                    catch (Exception pdfOcrEx)
                    {
                        _logger.LogWarning(pdfOcrEx, "OCR on PDF also failed, falling back to text extraction");
                        pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
                    }
                }
            }
            else
            {
                // No image available, try OCR on PDF directly
                try
                {
                    _logger.LogInformation("Performing OCR on PDF directly (image conversion not available)");
                    ocrResult = await _computerVisionService.ReadTextFromPdfAsync(pdfBytes);
                    
                    if (ocrResult != null && ocrResult.Pages != null && ocrResult.Pages.Count > 0)
                    {
                        // Filter to only the requested page
                        var requestedPage = ocrResult.Pages.FirstOrDefault(p => p.PageNumber == session.Request.PlanPageNumber);
                        if (requestedPage != null)
                        {
                            ocrResult = new OCRResult
                            {
                                Pages = new List<OCRPage> { requestedPage }
                            };
                        }
                        else
                        {
                            // Use first page if exact match not found
                            ocrResult = new OCRResult
                            {
                                Pages = ocrResult.Pages.Take(1).ToList()
                            };
                        }
                        
                        pdfText = ExtractTextFromOCRResult(ocrResult);
                        _logger.LogInformation("OCR on PDF completed. Found {LineCount} lines", 
                            ocrResult.Pages.FirstOrDefault()?.Lines.Count ?? 0);
                    }
                    else
                    {
                        _logger.LogWarning("OCR on PDF returned null or empty, falling back to text extraction");
                        pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OCR on PDF failed, falling back to text extraction");
                    pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
                }
            }
            
            // Ensure OCR results are stored - create from text extraction if OCR failed
            if (ocrResult != null && ocrResult.Pages != null && ocrResult.Pages.Count > 0)
            {
                try
                {
                    await _sessionService.StoreOCRResultsAsync(analysisId, ocrResult);
                    _logger.LogInformation("OCR results stored successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to store OCR results, but continuing with analysis");
                }
            }
            else if (!string.IsNullOrEmpty(pdfText))
            {
                // OCR failed but we have text - create a basic OCR result from extracted text
                // This allows the UI to display the text even though OCR didn't work
                try
                {
                    _logger.LogInformation("Creating OCR result from extracted text (OCR was not available)");
                    ocrResult = CreateOCRResultFromText(pdfText, session.Request.PlanPageNumber);
                    await _sessionService.StoreOCRResultsAsync(analysisId, ocrResult);
                    _logger.LogInformation("OCR results created from text extraction and stored");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create/store OCR results from text extraction");
                }
            }
            
            // Step 3: Detect symbols in image
            List<DetectedSymbol> detectedSymbols = new();
            
            if (planPageImage != null)
            {
                await _sessionService.UpdateSessionStatusAsync(
                    analysisId,
                    AnalysisStatus.Processing,
                    "Detecting symbols in plan page...",
                    ProcessingStage.SymbolDetecting.ToString());
                
                try
                {
                    _logger.LogInformation("Detecting symbols in plan page image");
                    detectedSymbols = await _imageProcessingService.DetectSymbolsAsync(planPageImage);
                    
                    // Crop and store symbol images
                    foreach (var symbol in detectedSymbols)
                    {
                        try
                        {
                            var croppedImage = await _imageProcessingService.CropImageAsync(
                                planPageImage, 
                                symbol.BoundingBox
                            );
                            
                            // Convert to base64 for storage
                            symbol.CroppedImage = Convert.ToBase64String(croppedImage);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to crop symbol image for symbol {SymbolId}", symbol.Id);
                        }
                    }
                    
                    // Store detected symbols
                    await _sessionService.StoreDetectedSymbolsAsync(analysisId, detectedSymbols);
                    
                    _logger.LogInformation("Detected {Count} symbols", detectedSymbols.Count);
                    
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.ReadyForValidation,
                        $"Detected {detectedSymbols.Count} potential module symbols. Please validate which are modules.",
                        ProcessingStage.AwaitingValidation.ToString());
                    
                    // Return early - wait for user validation
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting symbols, continuing with text analysis");
                }
            }
            
            // Step 4: Extract structured data from text
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

            // Step 5: Create a basic analysis result
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
                    OcrConfidence = ocrResult != null ? 0.8 : 0.7,
                    SymbolDetectionConfidence = detectedSymbols.Count > 0 ? 
                        detectedSymbols.Average(s => s.Confidence) : 0.0,
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
            var hasImage = planPageImage != null;
            var hasOCR = ocrResult != null && ocrResult.Pages != null && ocrResult.Pages.Count > 0;
            
            var completionMessage = $"Analysis completed. Found {moduleLabels.Count} module labels and {stLabels.Count} ST labels in text.";
            if (!hasImage)
            {
                completionMessage += " Note: Plan page image not available (PDF to image conversion failed or not configured).";
            }
            if (!hasOCR)
            {
                completionMessage += " Note: OCR results created from text extraction (OCR service may not be configured).";
            }
            
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Completed,
                completionMessage,
                ProcessingStage.Completed.ToString());

            _logger.LogInformation("Analysis completed for {AnalysisId}. HasImage: {HasImage}, HasOCR: {HasOCR}", 
                analysisId, hasImage, hasOCR);
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

    /// <summary>
    /// Extract module labels from OCR results with their positions
    /// </summary>
    private List<(string Label, Point Position)> ExtractModuleLabelsFromOCR(OCRResult? ocrResult)
    {
        var labels = new List<(string Label, Point Position)>();
        
        if (ocrResult == null || ocrResult.Pages == null)
        {
            return labels;
        }

        var pattern = new System.Text.RegularExpressions.Regex(@"\b([SM]-\d+|[SM]\d+)\b", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (var page in ocrResult.Pages)
        {
            if (page.Lines == null) continue;

            foreach (var line in page.Lines)
            {
                if (line.Words == null) continue;

                foreach (var word in line.Words)
                {
                    var match = pattern.Match(word.Text);
                    if (match.Success)
                    {
                        // Get center of word bounding box
                        var center = word.BoundingBox.GetCenter();
                        labels.Add((match.Value, center));
                    }
                }
            }
        }

        return labels;
    }

    /// <summary>
    /// Associate module labels from OCR with validated symbols based on spatial proximity
    /// </summary>
    private async Task AssociateModuleLabelsAsync(List<DetectedSymbol> validatedSymbols, OCRResult? ocrResult)
    {
        if (validatedSymbols == null || validatedSymbols.Count == 0)
        {
            _logger.LogInformation("No validated symbols to associate with labels");
            return;
        }

        if (ocrResult == null)
        {
            _logger.LogWarning("No OCR results available for label association");
            return;
        }

        // Extract module labels with positions from OCR
        var moduleLabels = ExtractModuleLabelsFromOCR(ocrResult);
        
        if (moduleLabels.Count == 0)
        {
            _logger.LogInformation("No module labels found in OCR results");
            return;
        }

        _logger.LogInformation("Found {LabelCount} module labels in OCR results", moduleLabels.Count);

        // Calculate association threshold (200 pixels or scale-based if available)
        // For now, use fixed threshold - could be made scale-aware later
        const double associationThreshold = 200.0;

        // Track which labels have been used
        var usedLabels = new HashSet<int>();

        // For each validated symbol, find the nearest module label
        foreach (var symbol in validatedSymbols)
        {
            var symbolCenter = symbol.BoundingBox.GetCenter();
            double minDistance = double.MaxValue;
            int nearestLabelIndex = -1;

            // Find nearest label
            for (int i = 0; i < moduleLabels.Count; i++)
            {
                if (usedLabels.Contains(i))
                    continue;

                var distance = symbolCenter.DistanceTo(moduleLabels[i].Position);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestLabelIndex = i;
                }
            }

            // Associate if within threshold
            if (nearestLabelIndex >= 0 && minDistance < associationThreshold)
            {
                symbol.AssociatedModuleLabel = moduleLabels[nearestLabelIndex].Label;
                usedLabels.Add(nearestLabelIndex);
                
                // Calculate association confidence
                var associationConfidence = 1.0 - (minDistance / associationThreshold);
                // Update symbol confidence to reflect association
                symbol.Confidence = Math.Min(0.95, symbol.Confidence + (associationConfidence * 0.1));

                _logger.LogInformation(
                    "Associated symbol {SymbolId} with label {Label} (distance: {Distance:F1}px, confidence: {Confidence:F2})",
                    symbol.Id, symbol.AssociatedModuleLabel, minDistance, symbol.Confidence);
            }
            else if (nearestLabelIndex >= 0)
            {
                _logger.LogInformation(
                    "Symbol {SymbolId} nearest to label {Label} but distance {Distance:F1}px exceeds threshold {Threshold}px",
                    symbol.Id, moduleLabels[nearestLabelIndex].Label, minDistance, associationThreshold);
            }
        }

        // Log unmatched symbols and labels
        var unmatchedSymbols = validatedSymbols.Count(s => string.IsNullOrEmpty(s.AssociatedModuleLabel));
        var unmatchedLabels = moduleLabels.Count - usedLabels.Count;

        if (unmatchedSymbols > 0)
        {
            _logger.LogWarning("Found {Count} validated symbols without associated labels", unmatchedSymbols);
        }

        if (unmatchedLabels > 0)
        {
            _logger.LogInformation("Found {Count} module labels without associated symbols", unmatchedLabels);
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

    private string ExtractTextFromOCRResult(OCRResult ocrResult)
    {
        var textBuilder = new System.Text.StringBuilder();
        
        foreach (var page in ocrResult.Pages)
        {
            foreach (var line in page.Lines)
            {
                textBuilder.AppendLine(line.Text);
            }
        }
        
        return textBuilder.ToString();
    }

    /// <summary>
    /// Create a basic OCRResult from plain text when OCR is not available
    /// This allows the UI to display text even when OCR fails
    /// </summary>
    private OCRResult CreateOCRResultFromText(string text, int pageNumber)
    {
        var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        var ocrLines = new List<OCRLine>();
        
        foreach (var lineText in lines)
        {
            if (!string.IsNullOrWhiteSpace(lineText))
            {
                var words = lineText.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(word => new OCRWord
                    {
                        Text = word,
                        Confidence = 0.5, // Lower confidence since this is from text extraction, not OCR
                        BoundingBox = new BoundingBox()
                    })
                    .ToList();
                
                ocrLines.Add(new OCRLine
                {
                    Text = lineText,
                    Confidence = 0.5,
                    BoundingBox = new BoundingBox(),
                    Words = words
                });
            }
        }
        
        return new OCRResult
        {
            Pages = new List<OCRPage>
            {
                new OCRPage
                {
                    PageNumber = pageNumber,
                    Width = 0, // Unknown dimensions from text extraction
                    Height = 0,
                    Lines = ocrLines
                }
            }
        };
    }

    /// <summary>
    /// Continue analysis after user has validated symbols
    /// </summary>
    public async Task ContinueAnalysisAfterValidationAsync(string analysisId)
    {
        try
        {
            _logger.LogInformation("Continuing analysis after validation for {AnalysisId}", analysisId);

            var session = await _sessionService.GetSessionAsync(analysisId);
            if (session == null)
            {
                _logger.LogWarning("Session {AnalysisId} not found", analysisId);
                return;
            }

            // Get validated symbols
            var symbols = await _sessionService.GetDetectedSymbolsAsync(analysisId);
            var validatedModules = symbols.Where(s => s.IsModule == true).ToList();

            _logger.LogInformation("Found {Count} validated modules", validatedModules.Count);

            // Get OCR results
            var ocrResult = await _sessionService.GetOCRResultsAsync(analysisId);
            string pdfText;
            if (ocrResult != null)
            {
                pdfText = ExtractTextFromOCRResult(ocrResult);
            }
            else
            {
                // Fallback to PDF text extraction
                byte[]? pdfBytes = await _sessionService.GetImageAsync(analysisId, "pdf");
                if (pdfBytes == null)
                {
                    throw new InvalidOperationException("PDF file not found in session");
                }
                pdfText = await _pdfProcessingService.ExtractTextAsync(pdfBytes, session.Request.PlanPageNumber);
            }

            // Extract basic information from text
            var moduleLabels = ExtractModuleLabelsFromText(pdfText);
            var stLabels = ExtractSTLabelsFromText(pdfText);
            var scaleInfo = ExtractScaleFromText(pdfText);

            // Associate module labels with validated symbols (STEP-2.6)
            await AssociateModuleLabelsAsync(validatedModules, ocrResult);

            // Create analysis result
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
                    Modules = validatedModules.Count > 0 ? validatedModules.Average(s => s.Confidence) : 0.5,
                    Pipes = 0.3,
                    Connections = 0.0,
                    Specifications = 0.0,
                    Elevations = 0.0
                },
                Ambiguities = new List<Ambiguity>(),
                ProcessingMetadata = new ProcessingMetadata
                {
                    ProcessingTime = 0,
                    OcrConfidence = ocrResult != null ? 0.8 : 0.7,
                    SymbolDetectionConfidence = symbols.Count > 0 ? symbols.Average(s => s.Confidence) : 0.0,
                    UserValidations = validatedModules.Count,
                    Timestamp = DateTime.UtcNow
                }
            };

            // Add modules from validated symbols
            foreach (var symbol in validatedModules)
            {
                result.Modules.Add(new Module
                {
                    Label = symbol.AssociatedModuleLabel ?? $"Module-{result.Modules.Count + 1}",
                    Symbol = symbol,
                    Location = symbol.BoundingBox.GetCenter(),
                    BoundingBox = symbol.BoundingBox,
                    Metadata = new ModuleMetadata 
                    { 
                        Confidence = symbol.Confidence 
                    }
                });
            }

            // Store result
            await _sessionService.StoreAnalysisResultAsync(analysisId, result);

            // Mark as completed
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Completed,
                $"Analysis completed. Found {validatedModules.Count} validated modules and {stLabels.Count} ST labels.",
                ProcessingStage.Completed.ToString());

            _logger.LogInformation("Analysis completed for {AnalysisId}", analysisId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing analysis after validation {AnalysisId}", analysisId);
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Error,
                $"Error during processing: {ex.Message}",
                ProcessingStage.Error.ToString());
        }
    }
}

