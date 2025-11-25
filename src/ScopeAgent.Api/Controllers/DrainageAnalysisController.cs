using Microsoft.AspNetCore.Mvc;
using ScopeAgent.Api.Models.DrainageAnalysis;
using ScopeAgent.Api.Services;

namespace ScopeAgent.Api.Controllers;

/// <summary>
/// Controller for drainage plan analysis operations
/// </summary>
[ApiController]
[Route("api/drainage")]
public class DrainageAnalysisController : ControllerBase
{
    private readonly IAnalysisSessionService _sessionService;
    private readonly IPdfProcessingService _pdfProcessingService;
    private readonly IComputerVisionService _computerVisionService;
    private readonly DrainageAnalysisProcessor _processor;
    private readonly ILogger<DrainageAnalysisController> _logger;

    public DrainageAnalysisController(
        IAnalysisSessionService sessionService,
        IPdfProcessingService pdfProcessingService,
        IComputerVisionService computerVisionService,
        DrainageAnalysisProcessor processor,
        ILogger<DrainageAnalysisController> logger)
    {
        _sessionService = sessionService;
        _pdfProcessingService = pdfProcessingService;
        _computerVisionService = computerVisionService;
        _processor = processor;
        _logger = logger;
    }

    /// <summary>
    /// Upload a PDF file for drainage plan analysis
    /// </summary>
    [HttpPost("upload")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upload([FromForm] UploadRequest request)
    {
        try
        {
            // Validate request
            if (request.PdfFile == null || request.PdfFile.Length == 0)
            {
                return BadRequest(new { error = "PDF file is required" });
            }

            if (request.PlanPageNumber < 1)
            {
                return BadRequest(new { error = "Plan page number must be at least 1" });
            }

            // Validate file type
            var allowedExtensions = new[] { ".pdf" };
            var fileExtension = Path.GetExtension(request.PdfFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { error = "Only PDF files are allowed" });
            }

            // Validate file size (max 50MB)
            const long maxFileSize = 50 * 1024 * 1024; // 50MB
            if (request.PdfFile.Length > maxFileSize)
            {
                return BadRequest(new { error = "File size exceeds maximum allowed size of 50MB" });
            }

            // Read PDF bytes
            byte[] pdfBytes;
            using (var memoryStream = new MemoryStream())
            {
                await request.PdfFile.CopyToAsync(memoryStream);
                pdfBytes = memoryStream.ToArray();
            }

            // Validate page number is within PDF
            var pageCount = await _pdfProcessingService.GetPageCountAsync(pdfBytes);
            if (request.PlanPageNumber > pageCount)
            {
                return BadRequest(new { error = $"Plan page number {request.PlanPageNumber} exceeds PDF page count of {pageCount}" });
            }

            if (request.ContentTablePageNumber.HasValue && 
                (request.ContentTablePageNumber.Value < 1 || request.ContentTablePageNumber.Value > pageCount))
            {
                return BadRequest(new { error = $"Content table page number {request.ContentTablePageNumber.Value} is invalid" });
            }

            // Create analysis session
            var analysisId = await _sessionService.CreateSessionAsync(request, pdfBytes);

            // Update status to indicate processing has started
            await _sessionService.UpdateSessionStatusAsync(
                analysisId, 
                AnalysisStatus.Processing, 
                "PDF uploaded successfully. Processing started.",
                ProcessingStage.Uploaded.ToString());

            _logger.LogInformation("PDF uploaded for analysis. AnalysisId: {AnalysisId}, PlanPage: {PlanPage}", 
                analysisId, request.PlanPageNumber);

            // Start background processing (fire and forget)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _processor.ProcessAnalysisAsync(analysisId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in background processing for {AnalysisId}", analysisId);
                }
            });

            // Return response (processing will continue asynchronously)
            var response = new UploadResponse
            {
                AnalysisId = analysisId,
                Status = AnalysisStatus.Processing,
                Message = "PDF uploaded successfully. Analysis in progress."
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading PDF for analysis");
            return StatusCode(500, new { error = "An error occurred while processing the upload", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the status of an analysis session
    /// </summary>
    [HttpGet("{analysisId}/status")]
    [ProducesResponseType(typeof(AnalysisStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatus(string analysisId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(analysisId);
            if (session == null)
            {
                return NotFound(new { error = $"Analysis session {analysisId} not found" });
            }

            var response = new AnalysisStatusResponse
            {
                AnalysisId = analysisId,
                Status = session.Status,
                Progress = session.Progress,
                Message = session.Message,
                CurrentStage = session.CurrentStage
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis status for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving status", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the analysis results
    /// </summary>
    [HttpGet("{analysisId}/results")]
    [ProducesResponseType(typeof(AnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetResults(string analysisId)
    {
        try
        {
            var result = await _sessionService.GetAnalysisResultAsync(analysisId);
            if (result == null)
            {
                return NotFound(new { error = $"Analysis results for {analysisId} not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting analysis results for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving results", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the plan page image
    /// </summary>
    [HttpGet("{analysisId}/image/plan")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPlanPageImage(string analysisId)
    {
        try
        {
            var imageBytes = await _sessionService.GetImageAsync(analysisId, "planPage");
            if (imageBytes == null)
            {
                return NotFound(new { error = $"Plan page image for {analysisId} not found" });
            }

            return File(imageBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plan page image for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving image", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the content table page image
    /// </summary>
    [HttpGet("{analysisId}/image/content-table")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContentTablePageImage(string analysisId)
    {
        try
        {
            var imageBytes = await _sessionService.GetImageAsync(analysisId, "contentTablePage");
            if (imageBytes == null)
            {
                return NotFound(new { error = $"Content table page image for {analysisId} not found" });
            }

            return File(imageBytes, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting content table page image for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving image", details = ex.Message });
        }
    }

    /// <summary>
    /// Get the OCR results
    /// </summary>
    [HttpGet("{analysisId}/ocr")]
    [ProducesResponseType(typeof(OCRResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetOCRResults(string analysisId)
    {
        try
        {
            var ocrResult = await _sessionService.GetOCRResultsAsync(analysisId);
            if (ocrResult == null)
            {
                return NotFound(new { error = $"OCR results for {analysisId} not found" });
            }

            return Ok(ocrResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting OCR results for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving OCR results", details = ex.Message });
        }
    }

    /// <summary>
    /// Get detected symbols for validation
    /// </summary>
    [HttpGet("{analysisId}/symbols")]
    [ProducesResponseType(typeof(List<DetectedSymbol>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetectedSymbols(string analysisId)
    {
        try
        {
            var symbols = await _sessionService.GetDetectedSymbolsAsync(analysisId);
            return Ok(symbols);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detected symbols for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving symbols", details = ex.Message });
        }
    }

    /// <summary>
    /// Validate detected symbols (mark which are modules)
    /// </summary>
    [HttpPost("{analysisId}/validate-symbols")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateSymbols(string analysisId, [FromBody] SymbolValidationRequest request)
    {
        try
        {
            // Get current symbols
            var symbols = await _sessionService.GetDetectedSymbolsAsync(analysisId);
            
            // Allow empty validations if no symbols were detected (user can proceed with text-only analysis)
            if (request.Validations == null || request.Validations.Count == 0)
            {
                if (symbols.Count == 0)
                {
                    _logger.LogInformation("No symbols detected and no validations provided - proceeding with text-only analysis");
                    // Continue with analysis even without symbol validation
                }
                else
                {
                    return BadRequest(new { error = "Validations are required when symbols are detected" });
                }
            }
            else
            {
                // Update symbols with validation results
                foreach (var validation in request.Validations)
                {
                    var symbol = symbols.FirstOrDefault(s => s.Id == validation.SymbolId);
                    if (symbol != null)
                    {
                        symbol.IsModule = validation.IsModule;
                    }
                }

                // Store updated symbols
                await _sessionService.StoreDetectedSymbolsAsync(analysisId, symbols);
            }

            // Update status to continue analysis
            var validationCount = request.Validations?.Count ?? 0;
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                validationCount > 0 
                    ? $"Validated {validationCount} symbols. Continuing analysis..."
                    : "No symbols to validate. Continuing with text-based analysis...",
                ProcessingStage.Analyzing.ToString());

            // Continue processing (trigger next phase)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _processor.ContinueAnalysisAfterValidationAsync(analysisId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error continuing analysis after validation for {AnalysisId}", analysisId);
                }
            });

            return Ok(new { message = "Symbols validated successfully", validatedCount = request.Validations?.Count ?? 0 });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating symbols for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while validating symbols", details = ex.Message });
        }
    }

    /// <summary>
    /// Get detected pipes for an analysis session
    /// </summary>
    [HttpGet("{analysisId}/pipes")]
    [ProducesResponseType(typeof(List<Pipe>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetectedPipes(string analysisId)
    {
        try
        {
            var result = await _sessionService.GetAnalysisResultAsync(analysisId);
            if (result == null)
            {
                return NotFound(new { error = $"Analysis results for {analysisId} not found" });
            }

            return Ok(result.Pipes ?? new List<Pipe>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting detected pipes for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving pipes", details = ex.Message });
        }
    }

    /// <summary>
    /// Get all cropped module images for verification
    /// </summary>
    [HttpGet("{analysisId}/modules/crops")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModuleCrops(string analysisId)
    {
        try
        {
            // Get validated symbols (modules)
            var symbols = await _sessionService.GetDetectedSymbolsAsync(analysisId);
            var validatedModules = symbols.Where(s => s.IsModule == true).ToList();

            if (validatedModules.Count == 0)
            {
                return Ok(new { modules = new List<object>() });
            }

            var moduleCrops = new List<object>();
            
            foreach (var symbol in validatedModules)
            {
                var imageKey = $"module_{symbol.Id}";
                var croppedImage = await _sessionService.GetImageAsync(analysisId, imageKey);
                
                if (croppedImage != null)
                {
                    // Convert to base64 for JSON response
                    var base64Image = Convert.ToBase64String(croppedImage);
                    var dataUrl = $"data:image/png;base64,{base64Image}";
                    
                    moduleCrops.Add(new
                    {
                        symbolId = symbol.Id,
                        moduleLabel = symbol.AssociatedModuleLabel ?? $"Module-{moduleCrops.Count + 1}",
                        confidence = symbol.Confidence,
                        boundingBox = symbol.BoundingBox,
                        croppedImage = dataUrl
                    });
                }
                else
                {
                    _logger.LogWarning("Cropped image not found for module {SymbolId} with key {ImageKey}", symbol.Id, imageKey);
                }
            }

            return Ok(new { modules = moduleCrops });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module crops for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while retrieving module crops", details = ex.Message });
        }
    }

    /// <summary>
    /// Get a specific cropped module image
    /// </summary>
    [HttpGet("{analysisId}/modules/{symbolId}/crop")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetModuleCrop(string analysisId, string symbolId)
    {
        try
        {
            var imageKey = $"module_{symbolId}";
            var croppedImage = await _sessionService.GetImageAsync(analysisId, imageKey);
            
            if (croppedImage == null)
            {
                return NotFound(new { error = $"Cropped module image for symbol {symbolId} not found" });
            }

            return File(croppedImage, "image/png");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting module crop for {AnalysisId}, symbol {SymbolId}", analysisId, symbolId);
            return StatusCode(500, new { error = "An error occurred while retrieving module crop", details = ex.Message });
        }
    }

    /// <summary>
    /// Confirm module verification and continue analysis
    /// </summary>
    [HttpPost("{analysisId}/modules/verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyModulesAndContinue(string analysisId)
    {
        try
        {
            var session = await _sessionService.GetSessionAsync(analysisId);
            if (session == null)
            {
                return NotFound(new { error = $"Analysis session {analysisId} not found" });
            }

            // Check if we're in the right stage
            if (session.CurrentStage != ProcessingStage.AwaitingModuleVerification.ToString())
            {
                return BadRequest(new { error = $"Cannot verify modules. Current stage is {session.CurrentStage}, expected {ProcessingStage.AwaitingModuleVerification}" });
            }

            // Update status
            await _sessionService.UpdateSessionStatusAsync(
                analysisId,
                AnalysisStatus.Processing,
                "Module verification confirmed. Continuing analysis...",
                ProcessingStage.Analyzing.ToString());

            // Continue processing (trigger next phase)
            _ = Task.Run(async () =>
            {
                try
                {
                    await _processor.ContinueAnalysisAfterModuleVerificationAsync(analysisId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error continuing analysis after module verification for {AnalysisId}", analysisId);
                }
            });

            return Ok(new { message = "Module verification confirmed. Analysis continuing..." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying modules for {AnalysisId}", analysisId);
            return StatusCode(500, new { error = "An error occurred while verifying modules", details = ex.Message });
        }
    }
}
