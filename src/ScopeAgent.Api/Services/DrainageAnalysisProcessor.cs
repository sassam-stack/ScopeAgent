using Microsoft.Extensions.Logging;
using ScopeAgent.Api.Models.DrainageAnalysis;
using System.Threading;

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
    private readonly IOuterportService _outerportService;

    public DrainageAnalysisProcessor(
        ILogger<DrainageAnalysisProcessor> logger,
        IAnalysisSessionService sessionService,
        IPdfProcessingService pdfProcessingService,
        IComputerVisionService computerVisionService,
        IImageProcessingService imageProcessingService,
        IOuterportService outerportService)
    {
        _logger = logger;
        _sessionService = sessionService;
        _pdfProcessingService = pdfProcessingService;
        _computerVisionService = computerVisionService;
        _imageProcessingService = imageProcessingService;
        _outerportService = outerportService;
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

            // Check if Outerport service should be used
            if (session.Request.UseOuterport)
            {
                _logger.LogInformation("Using Outerport service for analysis {AnalysisId}", analysisId);
                await ProcessOuterportAnalysisAsync(analysisId, session);
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
                    // Use 200 DPI for faster conversion (still good quality for OCR and symbol detection)
                    // 300 DPI can be very slow for large PDFs and creates huge images
                    var dpi = 200;
                    _logger.LogInformation("Converting PDF page {PageNumber} to image at {Dpi} DPI (reduced from 300 for faster processing)", 
                        session.Request.PlanPageNumber, dpi);
                    planPageImage = await _pdfProcessingService.ConvertPageToImageAsync(
                        pdfBytes, 
                        session.Request.PlanPageNumber, 
                        dpi: dpi
                    );
                    
                    _logger.LogInformation("PDF conversion successful! Image size: {Size} bytes", planPageImage.Length);
                    
                    // Store the converted image
                    _logger.LogInformation("Storing converted plan page image to session");
                    await _sessionService.StoreImageAsync(analysisId, "planPage", planPageImage);
                    
                    // Verify it was stored
                    var verifyImage = await _sessionService.GetImageAsync(analysisId, "planPage");
                    if (verifyImage != null)
                    {
                        _logger.LogInformation("Verified: Plan page image successfully stored in session ({Size} bytes)", verifyImage.Length);
                    }
                    else
                    {
                        _logger.LogError("ERROR: Plan page image was NOT stored in session despite successful conversion!");
                    }
                    
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.Processing,
                        "Plan page image converted. Starting OCR extraction...",
                        ProcessingStage.OcrExtracting.ToString());
                    
                    _logger.LogInformation("Plan page image conversion and storage completed successfully. planPageImage variable has {Size} bytes", planPageImage.Length);
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
                    _logger.LogError(ex, "Error converting PDF page to image: {Message}. Stack trace: {StackTrace}", ex.Message, ex.StackTrace);
                    
                    // Even if conversion failed, check if image was stored before the exception
                    var checkStored = await _sessionService.GetImageAsync(analysisId, "planPage");
                    if (checkStored != null)
                    {
                        _logger.LogInformation("Image was stored before exception occurred. Using stored image ({Size} bytes)", checkStored.Length);
                        planPageImage = checkStored; // Use the stored image even if there was an exception
                    }
                    else
                    {
                        planPageImage = null; // Ensure it's null so we don't try to use it
                    }
                    
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.Processing,
                        planPageImage != null 
                            ? $"PDF conversion had issues but image is available. Using stored image..."
                            : $"Error converting PDF to image: {ex.Message}. Using OCR on PDF directly...",
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
            
            // Always try to get the image from session (it might have been stored even if local variable is null)
            // This ensures we use the stored image even if there was an exception that set the variable to null
            _logger.LogInformation("Checking for plan page image in session for symbol detection");
            var imageForSymbolDetection = await _sessionService.GetImageAsync(analysisId, "planPage");
            
            if (imageForSymbolDetection != null)
            {
                _logger.LogInformation("Found plan page image in session ({Size} bytes) - will proceed with symbol detection", imageForSymbolDetection.Length);
                planPageImage = imageForSymbolDetection; // Update the variable to use the stored image
            }
            else if (planPageImage != null)
            {
                _logger.LogInformation("Using plan page image from local variable ({Size} bytes) - proceeding with symbol detection", planPageImage.Length);
                // Image is in local variable but not in session - store it now
                await _sessionService.StoreImageAsync(analysisId, "planPage", planPageImage);
            }
            else
            {
                _logger.LogWarning("Plan page image is not available in session or local variable - symbol detection will be skipped");
            }
            
            if (planPageImage != null)
            {
                _logger.LogInformation("Plan page image available ({Size} bytes), starting symbol detection", planPageImage.Length);
                
                await _sessionService.UpdateSessionStatusAsync(
                    analysisId,
                    AnalysisStatus.Processing,
                    "Detecting symbols in plan page...",
                    ProcessingStage.SymbolDetecting.ToString());
                
                try
                {
                    // Get OCR results first - we need module label bounding boxes to guide detection
                    if (ocrResult == null)
                    {
                        ocrResult = await _sessionService.GetOCRResultsAsync(analysisId);
                    }
                    
                    // Get ModuleList from request if provided by user
                    var userModuleList = session.Request.ModuleList;
                    if (userModuleList != null && userModuleList.Count > 0)
                    {
                        _logger.LogInformation("User provided ModuleList with {Count} modules: {Modules}", 
                            userModuleList.Count, string.Join(", ", userModuleList));
                    }
                    
                    // NEW APPROACH: Use OCR module labels to guide symbol detection
                    // If user provided ModuleList, prioritize those labels; otherwise use all OCR-detected labels
                    detectedSymbols = await DetectSymbolsNearModuleLabelsAsync(planPageImage, ocrResult, userModuleList);
                    _logger.LogInformation("Detected {Count} symbols near module labels", detectedSymbols.Count);
                    
                    // Apply strict filtering to remove false positives (text blocks, EOP, GE, etc.)
                    detectedSymbols = FilterRelevantSymbols(detectedSymbols, ocrResult, requireProximityToLabels: true, userModuleList: userModuleList);
                    _logger.LogInformation("After filtering, {Count} symbols remain", detectedSymbols.Count);
                    
                    // Fallback: If no symbols found with OCR-guided approach, try broader detection
                    if (detectedSymbols.Count == 0)
                    {
                        _logger.LogWarning("No symbols found with OCR-guided detection. Trying fallback: detect in whole image with relaxed filtering.");
                        var allSymbols = await _imageProcessingService.DetectSymbolsAsync(planPageImage);
                        _logger.LogInformation("Fallback detection found {Count} symbols in whole image", allSymbols.Count);
                        
                        if (allSymbols.Count == 0)
                        {
                            _logger.LogError("Python symbol detection returned 0 symbols even for whole image. " +
                                "This suggests: 1) Python detection constraints are too strict, 2) Symbols don't match patterns, " +
                                "3) Image quality issues, 4) Python service error. Check Python service logs.");
                        }
                        else
                        {
                            // Apply strict filtering even in fallback - use ModuleList if available
                            // userModuleList is already in scope from above
                            detectedSymbols = FilterRelevantSymbols(allSymbols, ocrResult, requireProximityToLabels: false, userModuleList: userModuleList);
                            _logger.LogInformation("After filtering, {Count} symbols remain", detectedSymbols.Count);
                        }
                    }
                    
                    // Crop images for all detected symbols (should be a reasonable number now with OCR-guided detection)
                    _logger.LogInformation("Cropping images for {Count} symbols", detectedSymbols.Count);
                    
                    // Crop images in parallel with a semaphore to limit concurrent requests
                    using var semaphore = new SemaphoreSlim(5, 5); // Max 5 concurrent crops
                    var cropTasks = detectedSymbols.Select(async symbol =>
                    {
                        await semaphore.WaitAsync();
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
                        finally
                        {
                            semaphore.Release();
                        }
                    });
                    
                    await Task.WhenAll(cropTasks);
                    _logger.LogInformation("Completed cropping images for {Count} symbols", detectedSymbols.Count);
                    
                    // Store detected symbols (even if 0, so user can see the validation UI)
                    await _sessionService.StoreDetectedSymbolsAsync(analysisId, detectedSymbols);
                    
                    _logger.LogInformation("Stored {Count} detected symbols. Setting status to ReadyForValidation", detectedSymbols.Count);
                    
                    // Always show validation UI, even if 0 symbols found
                    // This allows user to see that symbol detection ran and potentially add symbols manually
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.ReadyForValidation,
                        detectedSymbols.Count > 0 
                            ? $"Detected {detectedSymbols.Count} potential module symbols. Please validate which are modules."
                            : "Symbol detection completed but no symbols were found. You can still proceed with text-based analysis.",
                        ProcessingStage.AwaitingValidation.ToString());
                    
                    // Return early - wait for user validation (or user can proceed if 0 symbols)
                    _logger.LogInformation("Returning early to wait for user validation. Symbol detection complete.");
                    return;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting symbols: {ErrorMessage}. Stack trace: {StackTrace}", 
                        ex.Message, ex.StackTrace);
                    _logger.LogError(ex, "Continuing with text analysis despite symbol detection failure");
                }
            }
            else
            {
                _logger.LogWarning("Plan page image is not available - skipping symbol detection and proceeding with text-only analysis");
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
    /// Filter detected symbols to keep only relevant ones (near module labels, reasonable size, not text blocks)
    /// </summary>
    private List<DetectedSymbol> FilterRelevantSymbols(List<DetectedSymbol> symbols, OCRResult? ocrResult, byte[]? imageBytes = null, bool requireProximityToLabels = true, List<string>? userModuleList = null)
    {
        if (symbols == null || symbols.Count == 0)
            return symbols ?? new List<DetectedSymbol>();

        var filtered = new List<DetectedSymbol>();
        
        // Extract module labels from OCR for proximity filtering
        var moduleLabels = ExtractModuleLabelsFromOCR(ocrResult);
        
        // If user provided ModuleList, filter module labels to only those
        if (userModuleList != null && userModuleList.Count > 0)
        {
            var normalizedUserModules = userModuleList.Select(m => 
            {
                var normalized = m.Trim().ToUpperInvariant();
                if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[SM]\d+$"))
                {
                    normalized = normalized.Insert(1, "-");
                }
                return normalized;
            }).ToHashSet();
            
            moduleLabels = moduleLabels.Where(ml =>
            {
                var labelText = ml.Label.Trim().ToUpperInvariant();
                var normalizedLabel = labelText;
                if (System.Text.RegularExpressions.Regex.IsMatch(normalizedLabel, @"^[SM]\d+$"))
                {
                    normalizedLabel = normalizedLabel.Insert(1, "-");
                }
                return normalizedUserModules.Contains(normalizedLabel) || 
                       normalizedUserModules.Any(um => labelText.Contains(um) || um.Contains(labelText));
            }).ToList();
            
            _logger.LogInformation("Filtered to {LabelCount} module labels matching user's ModuleList (from {TotalCount} OCR labels)", 
                moduleLabels.Count, ExtractModuleLabelsFromOCR(ocrResult).Count);
        }
        else
        {
            _logger.LogInformation("Found {LabelCount} module labels in OCR for symbol filtering", moduleLabels.Count);
        }
        
        // Extract all OCR words to check for text overlap (filter out symbols that are part of text blocks)
        var allOCRWords = new List<(BoundingBox bbox, string text)>();
        // Also extract elevation labels (EOP, GE, IE) and numeric values to filter them out
        var elevationLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EOP", "E.O.P.", "GE", "G.E.", "IE", "I.E." };
        var numericPattern = new System.Text.RegularExpressions.Regex(@"^\d+\.?\d*$"); // Pure numbers
        
        if (ocrResult != null && ocrResult.Pages != null)
        {
            foreach (var page in ocrResult.Pages)
            {
                if (page.Lines != null)
                {
                    foreach (var line in page.Lines)
                    {
                        if (line.Words != null)
                        {
                            foreach (var word in line.Words)
                            {
                                if (word.BoundingBox != null && !string.IsNullOrWhiteSpace(word.Text))
                                {
                                    var wordText = word.Text.Trim();
                                    // Skip elevation labels and pure numbers (these are false positives)
                                    if (!elevationLabels.Contains(wordText) && !numericPattern.IsMatch(wordText))
                                    {
                                        allOCRWords.Add((word.BoundingBox, wordText));
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        _logger.LogInformation("Found {WordCount} OCR words for text overlap filtering (excluding elevation labels and pure numbers)", allOCRWords.Count);

        // MUCH STRICTER size constraints for symbols (in pixels)
        // Symbols should be small, clear shapes - not large tables or text blocks
        // If user provided ModuleList, be even stricter (they know what they're looking for)
        double minWidth, minHeight, maxWidth, maxHeight, minArea, maxArea, minAspectRatio, maxAspectRatio;
        
        if (userModuleList != null && userModuleList.Count > 0)
        {
            // Stricter when user provided specific modules
            minWidth = 35.0;   // Minimum 35px width
            minHeight = 35.0;  // Minimum 35px height
            maxWidth = 100.0;  // Maximum 100px width (stricter)
            maxHeight = 100.0; // Maximum 100px height (stricter)
            minArea = 1225.0;  // Minimum 35x35 = 1225 pixels²
            maxArea = 10000.0;  // Maximum 100x100 = 10000 pixels²
            minAspectRatio = 0.6;  // Stricter aspect ratio
            maxAspectRatio = 1.7;  // Stricter aspect ratio
        }
        else
        {
            // More lenient when searching all labels
            minWidth = 30.0;   // Minimum 30px width
            minHeight = 30.0;  // Minimum 30px height
            maxWidth = 120.0;  // Maximum 120px width
            maxHeight = 120.0; // Maximum 120px height
            minArea = 900.0;   // Minimum 30x30 = 900 pixels²
            maxArea = 14400.0; // Maximum 120x120 = 14400 pixels²
            minAspectRatio = 0.5;  // Width/Height minimum (allows tall rectangles, but not extreme)
            maxAspectRatio = 2.0;   // Width/Height maximum (allows wide rectangles, but not text lines)
        }
        
        // Proximity threshold for module labels (in pixels)
        // Should match or exceed the expansion amount used in DetectSymbolsNearModuleLabelsAsync (300px)
        // This ensures symbols detected in expanded regions aren't filtered out
        const double proximityThreshold = 350.0; // 350 pixels from a module label (increased to match expansion)
        
        // Filter out "Unknown" symbol types - these are likely false positives
        // Only keep symbols with recognized types (DoubleRectangle, CircleWithGrid, Oval)
        var validSymbolTypes = new[] { SymbolType.DoubleRectangle, SymbolType.CircleWithGrid, SymbolType.Oval };
        
        // Minimum confidence threshold - ALWAYS require 80% confidence minimum
        // Python service assigns 0.65 for single rectangles and 0.85 for double rectangles
        // We're being strict: only keep high-confidence detections (80%+)
        const double absoluteMinConfidence = 0.80; // 80% minimum confidence required for all symbols

        // Track symbol positions to filter out clusters (repeating patterns like dashes)
        var symbolPositions = new List<Point>();
        
        // Track filtering statistics for debugging
        int filteredByConfidence = 0;
        int filteredByType = 0;
        int filteredBySize = 0;
        int filteredByAspectRatio = 0;
        int filteredByCompactness = 0;
        int filteredByTextOverlap = 0;
        int filteredByProximity = 0;
        int filteredByTooClose = 0;

        foreach (var symbol in symbols)
        {
            // Check if bounding box has valid dimensions
            if (!symbol.BoundingBox.Width.HasValue || !symbol.BoundingBox.Height.HasValue ||
                !symbol.BoundingBox.X.HasValue || !symbol.BoundingBox.Y.HasValue)
            {
                _logger.LogDebug("Skipping symbol {SymbolId}: invalid bounding box", symbol.Id);
                continue;
            }

            var width = symbol.BoundingBox.Width.Value;
            var height = symbol.BoundingBox.Height.Value;
            var area = width * height;

            // Filter by confidence - require minimum 80% confidence for all symbols
            if (symbol.Confidence < absoluteMinConfidence)
            {
                filteredByConfidence++;
                _logger.LogDebug("Skipping symbol {SymbolId}: confidence {Confidence:F2} too low (min: {MinConfidence})", 
                    symbol.Id, symbol.Confidence, absoluteMinConfidence);
                continue;
            }

            // Filter out "Unknown" symbol types - these are likely false positives
            // Only keep recognized symbol types: DoubleRectangle, CircleWithGrid, Oval
            if (symbol.Type == SymbolType.Unknown || !validSymbolTypes.Contains(symbol.Type))
            {
                filteredByType++;
                _logger.LogDebug("Skipping symbol {SymbolId}: type is {Type} (not a recognized symbol type - only DoubleRectangle, CircleWithGrid, and Oval are allowed)", 
                    symbol.Id, symbol.Type);
                continue;
            }

            // Filter by size - symbols should be small, clear shapes
            if (width < minWidth || height < minHeight)
            {
                filteredBySize++;
                _logger.LogDebug("Skipping symbol {SymbolId}: too small ({Width:F1}x{Height:F1})", 
                    symbol.Id, width, height);
                continue;
            }

            if (width > maxWidth || height > maxHeight)
            {
                filteredBySize++;
                _logger.LogDebug("Skipping symbol {SymbolId}: too large ({Width:F1}x{Height:F1}) - likely table or text block", 
                    symbol.Id, width, height);
                continue;
            }

            // Filter by area - symbols should have reasonable area (not tiny dashes)
            if (area < minArea || area > maxArea)
            {
                filteredBySize++;
                _logger.LogDebug("Skipping symbol {SymbolId}: area {Area:F1}px² out of range ({MinArea}-{MaxArea})", 
                    symbol.Id, area, minArea, maxArea);
                continue;
            }

            // Filter by aspect ratio - symbols should be roughly square/rectangular, not long text lines
            var aspectRatio = width / height;
            if (aspectRatio < minAspectRatio || aspectRatio > maxAspectRatio)
            {
                filteredByAspectRatio++;
                _logger.LogDebug("Skipping symbol {SymbolId}: extreme aspect ratio {AspectRatio:F2} ({Width:F1}x{Height:F1}) - likely text line, dash, or percentage", 
                    symbol.Id, aspectRatio, width, height);
                continue;
            }
            
            // Additional filter: Very rectangular shapes with high area are likely text blocks or tables
            // Symbols should be more compact (closer to square)
            var compactnessRatio = Math.Min(width, height) / Math.Max(width, height); // 1.0 = square, < 0.5 = very rectangular
            if (compactnessRatio < 0.4 && area > 5000) // Very rectangular AND large = likely text block
            {
                filteredByCompactness++;
                _logger.LogDebug("Skipping symbol {SymbolId}: too rectangular and large (compactness: {Compactness:F2}, area: {Area:F1}) - likely text block or table", 
                    symbol.Id, compactnessRatio, area);
                continue;
            }
            
            // Calculate symbol center once for use in multiple filters
            var symbolCenter = symbol.BoundingBox.GetCenter();
            
            // Filter out symbols that overlap significantly with OCR text (likely part of text blocks)
            // This helps filter out "dead screens", elevation labels (EOP, GE), numeric values, and text that was incorrectly detected as symbols
            // BUT: We exclude module labels from this check since symbols should be near module labels
            if (allOCRWords.Count > 0)
            {
                var symbolBbox = symbol.BoundingBox;
                // Stricter threshold - filter if symbol is close to text
                const double textOverlapThreshold = 20.0; // 20px - symbol center too close to text
                const double textOverlapAreaRatio = 0.3; // 30% overlap with text bbox is enough to filter
                
                // Get module label texts to exclude from text overlap check
                var moduleLabelTexts = moduleLabels.Select(ml => ml.Label).ToHashSet(StringComparer.OrdinalIgnoreCase);
                
                // Common false positive patterns to filter out
                var falsePositivePatterns = new[]
                {
                    new System.Text.RegularExpressions.Regex(@"^E\.?O\.?P\.?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    new System.Text.RegularExpressions.Regex(@"^G\.?E\.?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    new System.Text.RegularExpressions.Regex(@"^I\.?E\.?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase),
                    new System.Text.RegularExpressions.Regex(@"^\d+\.?\d*$"), // Pure numbers
                    new System.Text.RegularExpressions.Regex(@"^\d+\.?\d*%$"), // Percentages
                    new System.Text.RegularExpressions.Regex(@"^\d+\.?\d*[""']$"), // Measurements like "18\""
                };
                
                bool overlapsWithText = false;
                foreach (var (wordBbox, wordText) in allOCRWords)
                {
                    // Skip very short words (likely false positives or noise)
                    if (wordText.Length < 2)
                        continue;
                    
                    // Check if word matches false positive patterns
                    bool isFalsePositive = falsePositivePatterns.Any(pattern => pattern.IsMatch(wordText));
                    if (isFalsePositive)
                    {
                        // Check if symbol is near this false positive text
                        var wordCenter = wordBbox.GetCenter();
                        var distanceToText = symbolCenter.DistanceTo(wordCenter);
                        if (distanceToText < textOverlapThreshold * 1.5) // More lenient for false positives
                        {
                            overlapsWithText = true;
                            _logger.LogDebug("Skipping symbol {SymbolId}: too close to false positive text '{Text}' ({Distance:F1}px)", 
                                symbol.Id, wordText, distanceToText);
                            break;
                        }
                    }
                    
                    // Skip module labels - symbols should be near module labels, so don't filter those
                    if (moduleLabelTexts.Contains(wordText) || moduleLabelTexts.Any(ml => wordText.Contains(ml) || ml.Contains(wordText)))
                    {
                        continue; // This is a module label, skip text overlap check
                    }
                    
                    // Check if symbol center is very close to text
                    var wordCenter2 = wordBbox.GetCenter();
                    var distanceToText2 = symbolCenter.DistanceTo(wordCenter2);
                    if (distanceToText2 < textOverlapThreshold)
                    {
                        overlapsWithText = true;
                        _logger.LogDebug("Skipping symbol {SymbolId}: center too close to OCR text '{Text}' ({Distance:F1}px < {Threshold}px)", 
                            symbol.Id, wordText, distanceToText2, textOverlapThreshold);
                        break;
                    }
                    
                    // Check if symbol bounding box overlaps significantly with text bounding box
                    if (symbolBbox.X.HasValue && symbolBbox.Y.HasValue && symbolBbox.Width.HasValue && symbolBbox.Height.HasValue &&
                        wordBbox.X.HasValue && wordBbox.Y.HasValue && wordBbox.Width.HasValue && wordBbox.Height.HasValue)
                    {
                        var overlapArea = CalculateOverlapArea(symbolBbox, wordBbox);
                        var symbolArea = symbolBbox.Width.Value * symbolBbox.Height.Value;
                        if (symbolArea > 0 && overlapArea / symbolArea > textOverlapAreaRatio)
                        {
                            overlapsWithText = true;
                            _logger.LogDebug("Skipping symbol {SymbolId}: overlaps {OverlapRatio:P0} with OCR text '{Text}' - likely part of text block", 
                                symbol.Id, overlapArea / symbolArea, wordText);
                            break;
                        }
                    }
                }
                
                if (overlapsWithText)
                {
                    filteredByTextOverlap++;
                    continue;
                }
            }
            
            // Filter out symbols that are too close together (likely repeating patterns like dashes)
            // symbolCenter already declared above
            const double minSymbolDistance = 50.0; // Minimum 50px between symbols
            bool tooClose = false;
            foreach (var existingPosition in symbolPositions)
            {
                var distance = symbolCenter.DistanceTo(existingPosition);
                if (distance < minSymbolDistance)
                {
                    _logger.LogDebug("Skipping symbol {SymbolId}: too close to another symbol ({Distance:F1}px) - likely repeating pattern", 
                        symbol.Id, distance);
                    tooClose = true;
                    break;
                }
            }
            if (tooClose)
            {
                filteredByTooClose++;
                continue;
            }

            // If we have module labels and proximity is required, only keep symbols near them
            // If user provided ModuleList, be even stricter about proximity
            if (moduleLabels.Count > 0 && requireProximityToLabels)
            {
                var isNearModuleLabel = false;
                double minDistance = double.MaxValue;
                string? nearestLabel = null;

                foreach (var (label, labelPosition) in moduleLabels)
                {
                    var distance = symbolCenter.DistanceTo(labelPosition);
                    if (distance < proximityThreshold)
                    {
                        isNearModuleLabel = true;
                        if (distance < minDistance)
                        {
                            minDistance = distance;
                            nearestLabel = label;
                        }
                        // Don't break - find the nearest label
                    }
                    minDistance = Math.Min(minDistance, distance);
                }

                if (!isNearModuleLabel)
                {
                    filteredByProximity++;
                    _logger.LogDebug("Skipping symbol {SymbolId}: not near any module label (min distance: {Distance:F1}px, threshold: {Threshold}px)", 
                        symbol.Id, minDistance, proximityThreshold);
                    continue;
                }

                // If user provided ModuleList, be stricter - require closer proximity
                if (userModuleList != null && userModuleList.Count > 0)
                {
                    const double strictProximityThreshold = 250.0; // Stricter when user provided list
                    if (minDistance > strictProximityThreshold)
                    {
                        filteredByProximity++;
                        _logger.LogDebug("Skipping symbol {SymbolId}: too far from module label '{Label}' ({Distance:F1}px > {Threshold}px) - user provided ModuleList requires stricter proximity", 
                            symbol.Id, nearestLabel, minDistance, strictProximityThreshold);
                        continue;
                    }
                }

                // Store the nearest module label
                if (nearestLabel != null && string.IsNullOrEmpty(symbol.AssociatedModuleLabel))
                {
                    symbol.AssociatedModuleLabel = nearestLabel;
                }

                _logger.LogDebug("Keeping symbol {SymbolId}: near module label '{Label}' (distance: {Distance:F1}px, size: {Width:F1}x{Height:F1}, type: {Type})", 
                    symbol.Id, nearestLabel ?? "unknown", minDistance, width, height, symbol.Type);
            }
            else if (moduleLabels.Count > 0 && !requireProximityToLabels)
            {
                // In fallback mode: prefer symbols near labels but don't require it
                double minDistance = double.MaxValue;
                foreach (var (label, labelPosition) in moduleLabels)
                {
                    var distance = symbolCenter.DistanceTo(labelPosition);
                    minDistance = Math.Min(minDistance, distance);
                }
                // Boost confidence if near a label, but don't filter out if far
                if (minDistance < proximityThreshold * 2) // 2x threshold for preference
                {
                    symbol.Confidence = Math.Min(0.95, symbol.Confidence + 0.1);
                    _logger.LogDebug("Keeping symbol {SymbolId}: in fallback mode, near module label (distance: {Distance:F1}px, boosted confidence: {Confidence:F2})", 
                        symbol.Id, minDistance, symbol.Confidence);
                }
                else
                {
                    _logger.LogDebug("Keeping symbol {SymbolId}: in fallback mode, not near module label (distance: {Distance:F1}px, size: {Width:F1}x{Height:F1}, type: {Type})", 
                        symbol.Id, minDistance, width, height, symbol.Type);
                }
            }
            else
            {
                // No module labels found - be VERY strict with size/aspect ratio filters
                // Also require higher confidence for symbols without nearby labels
                const double minConfidenceWithoutLabel = 0.8; // 80% confidence minimum
                if (symbol.Confidence < minConfidenceWithoutLabel)
                {
                    _logger.LogDebug("Skipping symbol {SymbolId}: confidence {Confidence:F2} too low without module label (min: {MinConfidence})", 
                        symbol.Id, symbol.Confidence, minConfidenceWithoutLabel);
                    continue;
                }
                _logger.LogDebug("No module labels found, keeping symbol {SymbolId} with high confidence {Confidence:F2} (size: {Width:F1}x{Height:F1}, type: {Type})", 
                    symbol.Id, symbol.Confidence, width, height, symbol.Type);
            }

            // Symbol passed all filters - add to filtered list and track position
            filtered.Add(symbol);
            symbolPositions.Add(symbolCenter);
        }

        // Final safety check: if we still have too many symbols, apply even stricter filtering
        // This handles cases where the initial filters weren't strict enough
        const int maxReasonableSymbols = 200; // Maximum reasonable number of symbols on a page
        if (filtered.Count > maxReasonableSymbols)
        {
            _logger.LogWarning("After initial filtering, still have {Count} symbols (max reasonable: {Max}). Applying stricter secondary filter.", 
                filtered.Count, maxReasonableSymbols);
            
            // Secondary filter: only keep symbols with highest confidence and closest to module labels
            if (moduleLabels.Count > 0)
            {
                // Calculate distance to nearest module label for each symbol
                var symbolsWithDistance = filtered.Select(s => new
                {
                    Symbol = s,
                    MinDistance = moduleLabels.Min(ml => s.BoundingBox.GetCenter().DistanceTo(ml.Position))
                }).OrderBy(x => x.MinDistance) // Closest to module labels first
                  .ThenByDescending(x => x.Symbol.Confidence) // Then by confidence
                  .Take(maxReasonableSymbols)
                  .Select(x => x.Symbol)
                  .ToList();
                
                _logger.LogInformation("Secondary filter reduced symbols from {OriginalCount} to {FilteredCount}", 
                    filtered.Count, symbolsWithDistance.Count);
                return symbolsWithDistance;
            }
            else
            {
                // No module labels - only keep highest confidence symbols
                var topSymbols = filtered.OrderByDescending(s => s.Confidence)
                                          .Take(maxReasonableSymbols)
                                          .ToList();
                
                _logger.LogInformation("Secondary filter (no module labels) reduced symbols from {OriginalCount} to {FilteredCount}", 
                    filtered.Count, topSymbols.Count);
                return topSymbols;
            }
        }

        // Log filtering statistics
        _logger.LogInformation("Symbol filtering complete: {Total} input, {Filtered} filtered, {Remaining} remaining. " +
            "Filtered by: confidence={Confidence}, type={Type}, size={Size}, aspectRatio={AspectRatio}, " +
            "compactness={Compactness}, textOverlap={TextOverlap}, proximity={Proximity}, tooClose={TooClose}",
            symbols.Count, symbols.Count - filtered.Count, filtered.Count,
            filteredByConfidence, filteredByType, filteredBySize, filteredByAspectRatio,
            filteredByCompactness, filteredByTextOverlap, filteredByProximity, filteredByTooClose);
        
        return filtered;
    }

    /// <summary>
    /// Detect symbols only near module labels from OCR (OCR-guided detection)
    /// For each module label, expand its bounding box and detect symbols in that region
    /// If userModuleList is provided, only search near those specific labels
    /// </summary>
    private async Task<List<DetectedSymbol>> DetectSymbolsNearModuleLabelsAsync(byte[] planPageImage, OCRResult? ocrResult, List<string>? userModuleList = null)
    {
        var allDetectedSymbols = new List<DetectedSymbol>();
        
        if (ocrResult == null)
        {
            _logger.LogWarning("No OCR results available - cannot use OCR-guided symbol detection");
            return allDetectedSymbols;
        }
        
        // Get module labels with their bounding boxes from OCR
        var allModuleLabelWords = OCRHelper.ExtractModuleLabels(ocrResult);
        _logger.LogInformation("Found {Count} module labels in OCR", allModuleLabelWords.Count);
        
        // Filter to only user-provided module labels if ModuleList is provided
        List<OCRWord> moduleLabelWords;
        if (userModuleList != null && userModuleList.Count > 0)
        {
            // Normalize user module list (handle both "S-1" and "S1" formats)
            var normalizedUserModules = userModuleList.Select(m => 
            {
                // Normalize to format like "S-1" or "S1"
                var normalized = m.Trim().ToUpperInvariant();
                // If it's "S1", convert to "S-1" for matching
                if (System.Text.RegularExpressions.Regex.IsMatch(normalized, @"^[SM]\d+$"))
                {
                    normalized = normalized.Insert(1, "-");
                }
                return normalized;
            }).ToHashSet();
            
            // Filter OCR labels to only those in user's ModuleList
            moduleLabelWords = allModuleLabelWords.Where(ml =>
            {
                var labelText = ml.Text.Trim().ToUpperInvariant();
                // Normalize OCR label for comparison
                var normalizedLabel = labelText;
                if (System.Text.RegularExpressions.Regex.IsMatch(normalizedLabel, @"^[SM]\d+$"))
                {
                    normalizedLabel = normalizedLabel.Insert(1, "-");
                }
                
                // Check if this label matches any in user's list
                return normalizedUserModules.Contains(normalizedLabel) || 
                       normalizedUserModules.Any(um => labelText.Contains(um) || um.Contains(labelText));
            }).ToList();
            
            _logger.LogInformation("Filtered to {Count} module labels matching user's ModuleList ({UserCount} provided): {Labels}", 
                moduleLabelWords.Count, userModuleList.Count, string.Join(", ", moduleLabelWords.Select(ml => ml.Text)));
            
            if (moduleLabelWords.Count == 0)
            {
                _logger.LogWarning("None of the user-provided module labels ({UserModules}) were found in OCR results. " +
                    "This might mean: 1) Labels are spelled differently, 2) OCR didn't detect them, 3) Labels are on a different page. " +
                    "Falling back to all OCR-detected labels.",
                    string.Join(", ", userModuleList));
                // Fallback to all OCR labels if user's labels aren't found
                moduleLabelWords = allModuleLabelWords;
            }
        }
        else
        {
            // No user ModuleList provided - use all OCR-detected labels
            moduleLabelWords = allModuleLabelWords;
            _logger.LogInformation("No user ModuleList provided - using all {Count} OCR-detected module labels", moduleLabelWords.Count);
        }
        
        if (moduleLabelWords.Count == 0)
        {
            _logger.LogWarning("No module labels found in OCR - cannot perform guided symbol detection. " +
                "This might mean: 1) OCR didn't find module labels, 2) Module label pattern doesn't match, " +
                "3) OCR failed. Check OCR results.");
            return allDetectedSymbols;
        }
        
        // Log first few module labels for debugging
        foreach (var label in moduleLabelWords.Take(5))
        {
            var bbox = label.BoundingBox;
            _logger.LogDebug("Module label '{Text}' at ({X:F0}, {Y:F0}, {W:F0}x{H:F0})", 
                label.Text, bbox.X ?? 0, bbox.Y ?? 0, bbox.Width ?? 0, bbox.Height ?? 0);
        }
        
        // Expansion amount (pixels) - expand bounding box to search for nearby symbols
        // If user provided ModuleList, use tighter expansion (more focused search)
        // Otherwise use broader expansion to catch all possible symbols
        double expansionPixels = userModuleList != null && userModuleList.Count > 0 
            ? 200.0  // Tighter when user provided specific modules
            : 300.0; // Broader when searching all OCR labels
        
        _logger.LogDebug("Using expansion of {Expansion}px for symbol detection (user provided {ModuleCount} modules)", 
            expansionPixels, userModuleList?.Count ?? 0);
        
        // Process each module label
        foreach (var moduleLabelWord in moduleLabelWords)
        {
            try
            {
                var labelBbox = moduleLabelWord.BoundingBox;
                
                // Check if bounding box has valid dimensions
                if (!labelBbox.X.HasValue || !labelBbox.Y.HasValue || 
                    !labelBbox.Width.HasValue || !labelBbox.Height.HasValue)
                {
                    _logger.LogDebug("Skipping module label '{Text}': invalid bounding box", moduleLabelWord.Text);
                    continue;
                }
                
                // Expand the bounding box to search for nearby symbols
                // Clamp to image boundaries to avoid cropping issues
                var expandedX = Math.Max(0, labelBbox.X.Value - expansionPixels);
                var expandedY = Math.Max(0, labelBbox.Y.Value - expansionPixels);
                var expandedWidth = labelBbox.Width.Value + (expansionPixels * 2);
                var expandedHeight = labelBbox.Height.Value + (expansionPixels * 2);
                
                // Note: Width/Height clamping will be handled by CropImageAsync if needed
                
                // Create expanded bounding box
                var expandedBbox = new BoundingBox
                {
                    X = expandedX,
                    Y = expandedY,
                    Width = expandedWidth,
                    Height = expandedHeight
                };
                
                _logger.LogDebug("Searching for symbols near module label '{Text}' in expanded region ({X:F0}, {Y:F0}, {W:F0}x{H:F0})", 
                    moduleLabelWord.Text, expandedX, expandedY, expandedWidth, expandedHeight);
                
                // Crop image to expanded region
                var croppedRegion = await _imageProcessingService.CropImageAsync(planPageImage, expandedBbox);
                _logger.LogDebug("Cropped region for label '{Text}': {Size} bytes", moduleLabelWord.Text, croppedRegion.Length);
                
                // Detect symbols in the cropped region
                var symbolsInRegion = await _imageProcessingService.DetectSymbolsAsync(croppedRegion);
                _logger.LogInformation("Detected {Count} symbols in cropped region for module label '{Text}' (region size: {W:F0}x{H:F0})", 
                    symbolsInRegion.Count, moduleLabelWord.Text, expandedWidth, expandedHeight);
                
                if (symbolsInRegion.Count == 0)
                {
                    _logger.LogWarning("No symbols detected in cropped region for module label '{Text}'. " +
                        "This might mean: 1) Symbols are outside the expanded region, 2) Python detection is too strict, " +
                        "3) Symbols don't match the detection patterns (double rectangle, circle with grid, oval)", 
                        moduleLabelWord.Text);
                }
                
                // Adjust symbol positions to account for cropping offset
                foreach (var symbol in symbolsInRegion)
                {
                    if (symbol.BoundingBox.X.HasValue && symbol.BoundingBox.Y.HasValue)
                    {
                        // Adjust coordinates to be relative to full image
                        symbol.BoundingBox.X = symbol.BoundingBox.X.Value + expandedX;
                        symbol.BoundingBox.Y = symbol.BoundingBox.Y.Value + expandedY;
                        
                        // Store the associated module label
                        symbol.AssociatedModuleLabel = moduleLabelWord.Text;
                        
                        _logger.LogDebug("Symbol {SymbolId} (type: {Type}, confidence: {Confidence:F2}) adjusted to ({X:F0}, {Y:F0})", 
                            symbol.Id, symbol.Type, symbol.Confidence, symbol.BoundingBox.X.Value, symbol.BoundingBox.Y.Value);
                    }
                    else
                    {
                        _logger.LogWarning("Symbol {SymbolId} has invalid coordinates after detection", symbol.Id);
                    }
                }
                
                allDetectedSymbols.AddRange(symbolsInRegion);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error detecting symbols near module label '{Text}'", moduleLabelWord.Text);
            }
        }
        
        // Deduplicate similar symbols (if two symbols are too similar, only keep one)
        var deduplicatedSymbols = DeduplicateSimilarSymbols(allDetectedSymbols);
        _logger.LogInformation("Deduplicated symbols: {OriginalCount} -> {DeduplicatedCount}", 
            allDetectedSymbols.Count, deduplicatedSymbols.Count);
        
        return deduplicatedSymbols;
    }
    
    /// <summary>
    /// Remove duplicate/similar symbols - if two symbols are too close and similar size, keep only one
    /// Improved to handle overlapping detections from multiple module label regions
    /// </summary>
    private List<DetectedSymbol> DeduplicateSimilarSymbols(List<DetectedSymbol> symbols)
    {
        if (symbols == null || symbols.Count == 0)
            return symbols ?? new List<DetectedSymbol>();
        
        var uniqueSymbols = new List<DetectedSymbol>();
        // Stricter threshold - symbols closer than 25px are likely duplicates
        // This helps when the same symbol is detected in overlapping regions near different module labels
        const double similarityThreshold = 25.0; // Pixels - symbols closer than this are considered duplicates
        const double sizeSimilarityThreshold = 0.25; // 25% size difference allowed (stricter)
        
        // Sort by confidence (highest first) so we keep the best detections
        var sortedSymbols = symbols.OrderByDescending(s => s.Confidence).ToList();
        
        foreach (var symbol in sortedSymbols)
        {
            var isDuplicate = false;
            var symbolCenter = symbol.BoundingBox.GetCenter();
            var symbolWidth = symbol.BoundingBox.Width ?? 0;
            var symbolHeight = symbol.BoundingBox.Height ?? 0;
            var symbolArea = symbolWidth * symbolHeight;
            
            foreach (var existingSymbol in uniqueSymbols)
            {
                var existingCenter = existingSymbol.BoundingBox.GetCenter();
                var existingWidth = existingSymbol.BoundingBox.Width ?? 0;
                var existingHeight = existingSymbol.BoundingBox.Height ?? 0;
                var existingArea = existingWidth * existingHeight;
                
                // Check distance
                var distance = symbolCenter.DistanceTo(existingCenter);
                if (distance > similarityThreshold)
                    continue;
                
                // Check size similarity (stricter)
                var areaRatio = Math.Min(symbolArea, existingArea) / Math.Max(symbolArea, existingArea);
                if (areaRatio < (1.0 - sizeSimilarityThreshold))
                    continue;
                
                // Check type similarity
                if (symbol.Type != existingSymbol.Type)
                    continue;
                
                // Check bounding box overlap - if they overlap significantly, they're duplicates
                var overlapArea = CalculateOverlapArea(symbol.BoundingBox, existingSymbol.BoundingBox);
                var overlapRatio = Math.Max(overlapArea / symbolArea, overlapArea / existingArea);
                if (overlapRatio > 0.5) // More than 50% overlap = duplicate
                {
                    // Keep the one with higher confidence (or better associated label)
                    if (symbol.Confidence <= existingSymbol.Confidence)
                    {
                        isDuplicate = true;
                        break;
                    }
                    else
                    {
                        // Replace existing with better one
                        uniqueSymbols.Remove(existingSymbol);
                        break;
                    }
                }
                
                // Symbols are very close and similar - keep the one with higher confidence
                if (symbol.Confidence <= existingSymbol.Confidence)
                {
                    isDuplicate = true;
                    break;
                }
                else
                {
                    // Replace existing with better one
                    uniqueSymbols.Remove(existingSymbol);
                    break;
                }
            }
            
            if (!isDuplicate)
            {
                uniqueSymbols.Add(symbol);
            }
        }
        
        _logger.LogInformation("Deduplication: {OriginalCount} symbols -> {UniqueCount} unique symbols", 
            symbols.Count, uniqueSymbols.Count);
        
        return uniqueSymbols;
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
    /// Extract ST labels from OCR results with their positions
    /// </summary>
    private List<STLabel> ExtractSTLabelsFromOCR(OCRResult? ocrResult)
    {
        var stLabels = new List<STLabel>();
        
        if (ocrResult == null || ocrResult.Pages == null)
        {
            return stLabels;
        }

        // Use OCRHelper to get OCRWord objects
        var ocrWords = OCRHelper.ExtractSTLabels(ocrResult);
        
        foreach (var word in ocrWords)
        {
            var stLabel = new STLabel
            {
                Text = "ST",
                BoundingBox = word.BoundingBox,
                Position = word.BoundingBox.GetCenter(),
                Confidence = word.Confidence
            };
            stLabels.Add(stLabel);
        }

        _logger.LogInformation("Extracted {Count} ST labels from OCR results", stLabels.Count);
        return stLabels;
    }

    /// <summary>
    /// Calculate the overlap area between two bounding boxes
    /// </summary>
    private double CalculateOverlapArea(BoundingBox bbox1, BoundingBox bbox2)
    {
        if (!bbox1.X.HasValue || !bbox1.Y.HasValue || !bbox1.Width.HasValue || !bbox1.Height.HasValue ||
            !bbox2.X.HasValue || !bbox2.Y.HasValue || !bbox2.Width.HasValue || !bbox2.Height.HasValue)
        {
            return 0.0;
        }
        
        var x1 = Math.Max(bbox1.X.Value, bbox2.X.Value);
        var y1 = Math.Max(bbox1.Y.Value, bbox2.Y.Value);
        var x2 = Math.Min(bbox1.X.Value + bbox1.Width.Value, bbox2.X.Value + bbox2.Width.Value);
        var y2 = Math.Min(bbox1.Y.Value + bbox1.Height.Value, bbox2.Y.Value + bbox2.Height.Value);
        
        if (x2 <= x1 || y2 <= y1)
            return 0.0;
        
        return (x2 - x1) * (y2 - y1);
    }

    /// <summary>
    /// Calculate perpendicular distance from a point to a line segment
    /// </summary>
    private double CalculateDistanceToLine(Point point, Point lineStart, Point lineEnd)
    {
        var lineSegment = new LineSegment
        {
            StartPoint = lineStart,
            EndPoint = lineEnd
        };
        return lineSegment.DistanceToPoint(point);
    }

    /// <summary>
    /// Group ST labels that are collinear (form a line pattern)
    /// </summary>
    private List<List<STLabel>> GroupCollinearSTLabels(List<STLabel> stLabels, double threshold = 50.0)
    {
        var groups = new List<List<STLabel>>();
        
        if (stLabels.Count < 2)
        {
            return groups;
        }

        var used = new HashSet<int>();
        
        // For each pair of ST labels, find other labels that are collinear
        for (int i = 0; i < stLabels.Count; i++)
        {
            if (used.Contains(i))
                continue;

            for (int j = i + 1; j < stLabels.Count; j++)
            {
                if (used.Contains(j))
                    continue;

                // Create a line from these two points
                var lineStart = stLabels[i].Position;
                var lineEnd = stLabels[j].Position;
                
                // Find all other labels that are close to this line
                var group = new List<STLabel> { stLabels[i], stLabels[j] };
                var groupIndices = new HashSet<int> { i, j };
                
                for (int k = 0; k < stLabels.Count; k++)
                {
                    if (k == i || k == j || used.Contains(k))
                        continue;

                    var distance = CalculateDistanceToLine(stLabels[k].Position, lineStart, lineEnd);
                    if (distance < threshold)
                    {
                        group.Add(stLabels[k]);
                        groupIndices.Add(k);
                    }
                }

                // Only create group if we have at least 2 labels
                if (group.Count >= 2)
                {
                    groups.Add(group);
                    foreach (var idx in groupIndices)
                    {
                        used.Add(idx);
                    }
                    break; // Move to next starting point
                }
            }
        }

        // Handle remaining ungrouped labels (if any)
        // These might be single ST labels or part of very short pipes
        for (int i = 0; i < stLabels.Count; i++)
        {
            if (!used.Contains(i))
            {
                // Create a single-label group (will be handled in line fitting)
                groups.Add(new List<STLabel> { stLabels[i] });
            }
        }

        _logger.LogInformation("Grouped {TotalLabels} ST labels into {GroupCount} collinear groups", 
            stLabels.Count, groups.Count);
        
        return groups;
    }

    /// <summary>
    /// Fit a line through a group of collinear ST labels and find endpoints
    /// </summary>
    private LineSegment FitLineThroughSTLabels(List<STLabel> stLabels)
    {
        if (stLabels == null || stLabels.Count == 0)
        {
            throw new ArgumentException("ST labels list cannot be empty");
        }

        // If only one label, create a short line segment centered on it
        if (stLabels.Count == 1)
        {
            var center = stLabels[0].Position;
            var defaultLength = 100.0; // Default length for single-label pipes
            
            return new LineSegment
            {
                StartPoint = new Point { X = center.X - defaultLength / 2, Y = center.Y },
                EndPoint = new Point { X = center.X + defaultLength / 2, Y = center.Y }
            };
        }

        // Extract all positions
        var positions = stLabels.Select(sl => sl.Position).ToList();
        
        // Calculate line using least squares
        var n = positions.Count;
        var sumX = positions.Sum(p => p.X);
        var sumY = positions.Sum(p => p.Y);
        var sumXY = positions.Sum(p => p.X * p.Y);
        var sumXX = positions.Sum(p => p.X * p.X);
        
        // Check for vertical line (infinite slope)
        var xVariance = positions.Select(p => p.X).Distinct().Count();
        if (xVariance == 1 || Math.Abs(sumXX - sumX * sumX / n) < 0.001)
        {
            // Vertical line: x = constant
            var x = positions.Average(p => p.X);
            var minY = positions.Min(p => p.Y);
            var maxY = positions.Max(p => p.Y);
            var verticalExtension = 50.0; // Extend by 50 pixels
            
            return new LineSegment
            {
                StartPoint = new Point { X = x, Y = minY - verticalExtension },
                EndPoint = new Point { X = x, Y = maxY + verticalExtension }
            };
        }
        
        // Calculate slope and intercept: y = mx + b
        var m = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
        var b = (sumY - m * sumX) / n;
        
        // Project all points onto the line and find min/max
        var projectedPoints = new List<Point>();
        foreach (var pos in positions)
        {
            // Project point onto line
            var projX = (pos.X + m * (pos.Y - b)) / (1 + m * m);
            var projY = m * projX + b;
            projectedPoints.Add(new Point { X = projX, Y = projY });
        }
        
        // Find the two points that are furthest apart
        double maxDistance = 0;
        Point? startPoint = null;
        Point? endPoint = null;
        
        for (int i = 0; i < projectedPoints.Count; i++)
        {
            for (int j = i + 1; j < projectedPoints.Count; j++)
            {
                var dist = projectedPoints[i].DistanceTo(projectedPoints[j]);
                if (dist > maxDistance)
                {
                    maxDistance = dist;
                    startPoint = projectedPoints[i];
                    endPoint = projectedPoints[j];
                }
            }
        }
        
        // If we couldn't find two distinct points, use min/max
        if (startPoint == null || endPoint == null)
        {
            var minX = projectedPoints.Min(p => p.X);
            var maxX = projectedPoints.Max(p => p.X);
            startPoint = new Point { X = minX, Y = m * minX + b };
            endPoint = new Point { X = maxX, Y = m * maxX + b };
        }
        
        // Extend the line beyond the endpoints
        var extension = 50.0;
        var dx = endPoint.X - startPoint.X;
        var dy = endPoint.Y - startPoint.Y;
        var length = Math.Sqrt(dx * dx + dy * dy);
        
        if (length > 0)
        {
            var extendX = (dx / length) * extension;
            var extendY = (dy / length) * extension;
            
            return new LineSegment
            {
                StartPoint = new Point 
                { 
                    X = startPoint.X - extendX, 
                    Y = startPoint.Y - extendY 
                },
                EndPoint = new Point 
                { 
                    X = endPoint.X + extendX, 
                    Y = endPoint.Y + extendY 
                }
            };
        }
        
        // Fallback: return line through first and last positions
        return new LineSegment
        {
            StartPoint = positions.First(),
            EndPoint = positions.Last()
        };
    }

    /// <summary>
    /// Verify that a detected line exists in the image using Hough Transform
    /// </summary>
    private async Task<bool> VerifyLineWithHoughTransform(byte[] imageBytes, LineSegment fittedLine, double tolerance = 20.0)
    {
        try
        {
            // Get all detected lines from image
            var detectedLines = await _imageProcessingService.DetectLinesAsync(imageBytes);
            
            if (detectedLines == null || detectedLines.Count == 0)
            {
                // No lines detected, but ST pattern is still valid
                return false;
            }
            
            // Check if any detected line is close to our fitted line
            foreach (var detectedLine in detectedLines)
            {
                // Check if endpoints are close
                var startDist1 = fittedLine.StartPoint.DistanceTo(detectedLine.StartPoint);
                var startDist2 = fittedLine.StartPoint.DistanceTo(detectedLine.EndPoint);
                var endDist1 = fittedLine.EndPoint.DistanceTo(detectedLine.StartPoint);
                var endDist2 = fittedLine.EndPoint.DistanceTo(detectedLine.EndPoint);
                
                var minDist = Math.Min(Math.Min(startDist1, startDist2), Math.Min(endDist1, endDist2));
                
                // Also check if the lines are parallel and close
                var line1Angle = Math.Atan2(
                    fittedLine.EndPoint.Y - fittedLine.StartPoint.Y,
                    fittedLine.EndPoint.X - fittedLine.StartPoint.X);
                var line2Angle = Math.Atan2(
                    detectedLine.EndPoint.Y - detectedLine.StartPoint.Y,
                    detectedLine.EndPoint.X - detectedLine.StartPoint.X);
                
                var angleDiff = Math.Abs(line1Angle - line2Angle);
                if (angleDiff > Math.PI) angleDiff = 2 * Math.PI - angleDiff;
                
                // Check if lines are parallel (angle difference < 10 degrees) and close
                if (angleDiff < Math.PI / 18 && minDist < tolerance * 2)
                {
                    return true;
                }
                
                // Check if endpoints are close enough
                if (minDist < tolerance)
                {
                    return true;
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error verifying line with Hough Transform");
            return false; // Don't fail pipe detection if verification fails
        }
    }

    /// <summary>
    /// Connect pipe segments that share endpoints
    /// </summary>
    private List<Pipe> ConnectPipeSegments(List<Pipe> pipes, double connectionThreshold = 30.0)
    {
        if (pipes == null || pipes.Count <= 1)
        {
            return pipes ?? new List<Pipe>();
        }

        var connectedPipes = new List<Pipe>();
        var merged = new HashSet<int>();
        
        for (int i = 0; i < pipes.Count; i++)
        {
            if (merged.Contains(i))
                continue;

            var currentPipe = pipes[i];
            var mergedPipe = new Pipe
            {
                Id = currentPipe.Id,
                Line = currentPipe.Line,
                STLabels = new List<STLabel>(currentPipe.STLabels),
                Specification = currentPipe.Specification,
                FlowDirection = currentPipe.FlowDirection,
                Connections = new List<PipeConnection>(currentPipe.Connections),
                Confidence = currentPipe.Confidence
            };

            // Try to merge with other pipes
            bool foundMerge = true;
            while (foundMerge)
            {
                foundMerge = false;
                
                for (int j = i + 1; j < pipes.Count; j++)
                {
                    if (merged.Contains(j))
                        continue;

                    var otherPipe = pipes[j];
                    
                    // Check all endpoint combinations
                    var dist1 = mergedPipe.Line.EndPoint.DistanceTo(otherPipe.Line.StartPoint);
                    var dist2 = mergedPipe.Line.EndPoint.DistanceTo(otherPipe.Line.EndPoint);
                    var dist3 = mergedPipe.Line.StartPoint.DistanceTo(otherPipe.Line.StartPoint);
                    var dist4 = mergedPipe.Line.StartPoint.DistanceTo(otherPipe.Line.EndPoint);
                    
                    var minDist = Math.Min(Math.Min(dist1, dist2), Math.Min(dist3, dist4));
                    
                    if (minDist < connectionThreshold)
                    {
                        // Merge pipes
                        mergedPipe.STLabels.AddRange(otherPipe.STLabels);
                        
                        // Determine new endpoints (furthest apart)
                        var allPoints = new[]
                        {
                            mergedPipe.Line.StartPoint,
                            mergedPipe.Line.EndPoint,
                            otherPipe.Line.StartPoint,
                            otherPipe.Line.EndPoint
                        };
                        
                        double maxDist = 0;
                        Point? newStart = null;
                        Point? newEnd = null;
                        
                        for (int p1 = 0; p1 < allPoints.Length; p1++)
                        {
                            for (int p2 = p1 + 1; p2 < allPoints.Length; p2++)
                            {
                                var dist = allPoints[p1].DistanceTo(allPoints[p2]);
                                if (dist > maxDist)
                                {
                                    maxDist = dist;
                                    newStart = allPoints[p1];
                                    newEnd = allPoints[p2];
                                }
                            }
                        }
                        
                        if (newStart != null && newEnd != null)
                        {
                            mergedPipe.Line = new LineSegment
                            {
                                StartPoint = newStart,
                                EndPoint = newEnd
                            };
                        }
                        
                        // Merge specifications (prefer non-null)
                        if (mergedPipe.Specification == null && otherPipe.Specification != null)
                        {
                            mergedPipe.Specification = otherPipe.Specification;
                        }
                        
                        // Merge flow directions (prefer non-null)
                        if (mergedPipe.FlowDirection == null && otherPipe.FlowDirection != null)
                        {
                            mergedPipe.FlowDirection = otherPipe.FlowDirection;
                        }
                        
                        // Update confidence (average)
                        mergedPipe.Confidence = (mergedPipe.Confidence + otherPipe.Confidence) / 2.0;
                        
                        merged.Add(j);
                        foundMerge = true;
                        break;
                    }
                }
            }
            
            connectedPipes.Add(mergedPipe);
        }
        
        // Filter pipes: remove pipes with < 2 ST labels (unless they're very short and have at least 1)
        var filteredPipes = connectedPipes.Where(p => 
            p.STLabels.Count >= 2 || 
            (p.STLabels.Count == 1 && p.Line.Length() < 100)).ToList();
        
        _logger.LogInformation("Connected {OriginalCount} pipes into {ConnectedCount} pipes, filtered to {FilteredCount}",
            pipes.Count, connectedPipes.Count, filteredPipes.Count);
        
        return filteredPipes;
    }

    /// <summary>
    /// Detect pipes from ST labels in OCR results
    /// </summary>
    private async Task<List<Pipe>> DetectPipesAsync(string analysisId)
    {
        var pipes = new List<Pipe>();
        
        try
        {
            // Get OCR results
            var ocrResult = await _sessionService.GetOCRResultsAsync(analysisId);
            if (ocrResult == null)
            {
                _logger.LogWarning("No OCR results available for pipe detection");
                return pipes;
            }

            // Extract ST labels from OCR
            var stLabels = ExtractSTLabelsFromOCR(ocrResult);
            _logger.LogInformation("Extracted {Count} ST labels from OCR for pipe detection", stLabels.Count);
            
            if (stLabels.Count == 0)
            {
                _logger.LogWarning("No ST labels found in OCR results - cannot detect pipes");
                return pipes;
            }

            // Log first few ST label positions for debugging
            foreach (var stLabel in stLabels.Take(3))
            {
                _logger.LogDebug("ST Label at position ({X}, {Y}) with confidence {Confidence}", 
                    stLabel.Position.X, stLabel.Position.Y, stLabel.Confidence);
            }

            // Group collinear ST labels
            var collinearGroups = GroupCollinearSTLabels(stLabels, threshold: 50.0);
            _logger.LogInformation("Grouped ST labels into {GroupCount} collinear groups", collinearGroups.Count);
            
            // Get plan page image for verification (optional)
            byte[]? planPageImage = await _sessionService.GetImageAsync(analysisId, "planPage");

            // For each group, fit a line and create a pipe
            foreach (var group in collinearGroups)
            {
                try
                {
                    // Fit line through ST labels
                    var fittedLine = FitLineThroughSTLabels(group);
                    
                    // Verify line with Hough Transform (if image available)
                    bool verified = false;
                    if (planPageImage != null)
                    {
                        verified = await VerifyLineWithHoughTransform(planPageImage, fittedLine, tolerance: 20.0);
                        if (!verified && group.Count >= 2)
                        {
                            // Still valid if we have 2+ ST labels
                            _logger.LogDebug("Pipe with {Count} ST labels not verified by Hough Transform, but keeping it", group.Count);
                        }
                    }
                    
                    // Create pipe object
                    var pipe = new Pipe
                    {
                        Id = Guid.NewGuid().ToString(),
                        Line = fittedLine,
                        STLabels = group,
                        Confidence = group.Average(sl => sl.Confidence)
                    };
                    
                    pipes.Add(pipe);
                    
                    _logger.LogDebug("Created pipe {PipeId} with {LabelCount} ST labels, length {Length:F1}px, verified: {Verified}",
                        pipe.Id, group.Count, fittedLine.Length(), verified);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error creating pipe from ST label group");
                }
            }
            
            // Connect pipe segments
            if (pipes.Count > 1)
            {
                pipes = ConnectPipeSegments(pipes, connectionThreshold: 30.0);
            }
            
            _logger.LogInformation("Detected {Count} pipes from {STCount} ST labels", pipes.Count, stLabels.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting pipes");
        }
        
        return pipes;
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

            // Detect pipes from ST labels (STEP-3)
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "Detecting pipes from ST labels...",
                ProcessingStage.Analyzing.ToString());

            var detectedPipes = await DetectPipesAsync(analysisId);
            _logger.LogInformation("Detected {Count} pipes", detectedPipes.Count);

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
                Pipes = detectedPipes,
                Graph = new ConnectionGraph(),
                Confidence = new OverallConfidence
                {
                    Modules = validatedModules.Count > 0 ? validatedModules.Average(s => s.Confidence) : 0.5,
                    Pipes = detectedPipes.Count > 0 ? detectedPipes.Average(p => p.Confidence) : 0.0,
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

            // Extract module labels with positions from OCR for location data
            // Handle duplicates by taking the first occurrence of each label
            var moduleLabelsWithPositions = ExtractModuleLabelsFromOCR(ocrResult);
            var moduleLabelMap = moduleLabelsWithPositions
                .GroupBy(ml => ml.Label, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First().Position);
            
            if (moduleLabelsWithPositions.Count != moduleLabelMap.Count)
            {
                var duplicates = moduleLabelsWithPositions
                    .GroupBy(ml => ml.Label, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => $"{g.Key} ({g.Count()} occurrences)");
                _logger.LogWarning("Found duplicate module labels in OCR: {Duplicates}. Using first occurrence for each.", 
                    string.Join(", ", duplicates));
            }

            // Add modules from validated symbols
            foreach (var symbol in validatedModules)
            {
                var moduleLabel = symbol.AssociatedModuleLabel ?? $"Module-{result.Modules.Count + 1}";
                
                // Try to get location from OCR first (more accurate), fallback to symbol bounding box
                Point moduleLocation;
                if (moduleLabelMap.TryGetValue(moduleLabel, out var ocrLocation))
                {
                    moduleLocation = ocrLocation;
                    _logger.LogInformation("Using OCR location for module {Label}: ({X}, {Y})", moduleLabel, ocrLocation.X, ocrLocation.Y);
                }
                else
                {
                    moduleLocation = symbol.BoundingBox.GetCenter();
                    _logger.LogInformation("Using symbol bounding box center for module {Label}: ({X}, {Y})", moduleLabel, moduleLocation.X, moduleLocation.Y);
                }
                
                result.Modules.Add(new Module
                {
                    Label = moduleLabel,
                    Symbol = symbol,
                    Location = moduleLocation,
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
                $"Analysis completed. Found {validatedModules.Count} validated modules and {detectedPipes.Count} pipes from {stLabels.Count} ST labels.",
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

    /// <summary>
    /// Continue analysis after user has verified modules
    /// </summary>
    public async Task ContinueAnalysisAfterModuleVerificationAsync(string analysisId)
    {
        try
        {
            _logger.LogInformation("Continuing analysis after module verification for {AnalysisId}", analysisId);

            var session = await _sessionService.GetSessionAsync(analysisId);
            if (session == null)
            {
                _logger.LogWarning("Session {AnalysisId} not found", analysisId);
                return;
            }

            // Continue with the same flow as after validation
            // Module verification is essentially a confirmation step, so we can proceed with analysis
            await ContinueAnalysisAfterValidationAsync(analysisId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error continuing analysis after module verification {AnalysisId}", analysisId);
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Error,
                $"Error during processing: {ex.Message}",
                ProcessingStage.Error.ToString());
        }
    }

    private async Task ProcessOuterportAnalysisAsync(string analysisId, AnalysisSession session)
    {
        try
        {
            _logger.LogInformation("Starting Outerport analysis processing for {AnalysisId}", analysisId);

            byte[]? pdfBytes = await _sessionService.GetImageAsync(analysisId, "pdf");
            if (pdfBytes == null)
            {
                throw new InvalidOperationException("PDF file not found in session");
            }

            // Step 1: Convert PDF page to image
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "Converting PDF page to image...",
                ProcessingStage.OcrExtracting.ToString());

            byte[]? planPageImage = await _sessionService.GetImageAsync(analysisId, "planPage");
            
            if (planPageImage == null)
            {
                try
                {
                    var dpi = 200;
                    _logger.LogInformation("Converting PDF page {PageNumber} to image at {Dpi} DPI for Outerport processing", 
                        session.Request.PlanPageNumber, dpi);
                    planPageImage = await _pdfProcessingService.ConvertPageToImageAsync(
                        pdfBytes, 
                        session.Request.PlanPageNumber, 
                        dpi: dpi
                    );
                    
                    _logger.LogInformation("PDF conversion successful! Image size: {Size} bytes", planPageImage.Length);
                    await _sessionService.StoreImageAsync(analysisId, "planPage", planPageImage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error converting PDF page to image: {Message}", ex.Message);
                    await _sessionService.UpdateSessionStatusAsync(
                        analysisId,
                        AnalysisStatus.Error,
                        $"Error converting PDF to image: {ex.Message}",
                        null);
                    return;
                }
            }

            // Step 2: Process with Outerport service
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "Processing drainage plan with Outerport service...",
                ProcessingStage.Analyzing.ToString());

            _logger.LogInformation("Calling Outerport service for analysis {AnalysisId}", analysisId);
            var outerportResult = await _outerportService.ProcessDrainagePlanAsync(planPageImage);
            
            if (outerportResult == null)
            {
                throw new InvalidOperationException("Outerport service returned null result");
            }

            _logger.LogInformation("Outerport processing completed. Found {JunctionCount} junctions and {MaterialCount} materials",
                outerportResult.Junctions.Count, outerportResult.Materials.Count);

            // Store Outerport results
            await _sessionService.StoreOuterportResultsAsync(analysisId, outerportResult);

            // Mark as completed
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Completed,
                $"Outerport analysis completed. Found {outerportResult.Junctions.Count} junctions and {outerportResult.Materials.Count} materials.",
                ProcessingStage.Completed.ToString());

            _logger.LogInformation("Outerport analysis completed for {AnalysisId}", analysisId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Outerport analysis {AnalysisId}", analysisId);
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Error,
                $"Error during Outerport processing: {ex.Message}",
                null);
        }
    }
}

