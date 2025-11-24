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
}
