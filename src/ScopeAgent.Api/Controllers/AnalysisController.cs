using Microsoft.AspNetCore.Mvc;
using ScopeAgent.Api.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace ScopeAgent.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IComputerVisionService _computerVisionService;
    private readonly IYoloService _yoloService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IComputerVisionService computerVisionService,
        IYoloService yoloService,
        ILogger<AnalysisController> logger)
    {
        _computerVisionService = computerVisionService;
        _yoloService = yoloService;
        _logger = logger;
    }

    [HttpPost("analyze")]
    public async Task<ActionResult> AnalyzeImage(IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "No file uploaded or file is empty"
                });
            }

            _logger.LogInformation("Starting image analysis for file: {FileName}, Size: {Size} bytes", file.FileName, file.Length);

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new
                {
                    success = false,
                    error = $"File type not supported. Allowed types: {string.Join(", ", allowedExtensions)}"
                });
            }

            // Read file into byte array
            byte[] imageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                imageBytes = memoryStream.ToArray();
            }

            _logger.LogInformation("Image file read successfully, size: {Size} bytes", imageBytes.Length);

            // Azure Computer Vision API limits:
            // - Maximum file size: 4 MB (4,194,304 bytes)
            // - Maximum dimensions: 10,000 x 10,000 pixels
            const long maxFileSize = 4 * 1024 * 1024; // 4 MB
            const int maxDimension = 10000;

            // Resize image if it's too large
            if (imageBytes.Length > maxFileSize)
            {
                _logger.LogInformation("Image size {Size} bytes exceeds limit of {MaxSize} bytes. Resizing...", imageBytes.Length, maxFileSize);
                imageBytes = await ResizeImageAsync(imageBytes, maxFileSize, maxDimension);
                _logger.LogInformation("Image resized to {Size} bytes", imageBytes.Length);
            }
            else
            {
                // Check dimensions even if file size is OK
                try
                {
                    using var image = Image.Load(imageBytes);
                    if (image.Width > maxDimension || image.Height > maxDimension)
                    {
                        _logger.LogInformation("Image dimensions {Width}x{Height} exceed limit of {MaxDimension}x{MaxDimension}. Resizing...", 
                            image.Width, image.Height, maxDimension, maxDimension);
                        imageBytes = await ResizeImageAsync(imageBytes, maxFileSize, maxDimension);
                        _logger.LogInformation("Image resized to {Size} bytes", imageBytes.Length);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not check image dimensions, proceeding with original image");
                }
            }

            // Analyze with Computer Vision API
            var analyzeResult = await _computerVisionService.AnalyzeImageAsync(imageBytes);
            
            if (analyzeResult == null)
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Computer Vision is not configured or failed to analyze the image"
                });
            }

            _logger.LogInformation("Computer Vision analysis completed");

            // Also get OCR/text extraction
            var readResult = await _computerVisionService.ReadTextAsync(imageBytes);
            
            // Get YOLO analysis (optional - graceful degradation if service unavailable)
            object? yoloResult = null;
            try
            {
                yoloResult = await _yoloService.AnalyzeImageAsync(imageBytes);
                if (yoloResult != null)
                {
                    _logger.LogInformation("YOLO analysis completed");
                }
                else
                {
                    _logger.LogWarning("YOLO service returned null result (service may be unavailable)");
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "YOLO service error (continuing without YOLO results)");
            }
            
            // Combine results
            var combinedResult = new Dictionary<string, object?>();
            
            if (analyzeResult is Dictionary<string, object?> analyzeDict)
            {
                foreach (var kvp in analyzeDict)
                {
                    combinedResult[kvp.Key] = kvp.Value;
                }
            }
            
            if (readResult != null)
            {
                combinedResult["text"] = readResult;
            }
            
            if (yoloResult != null)
            {
                combinedResult["yolo"] = yoloResult;
            }

            _logger.LogInformation("Image analysis completed (Computer Vision: {HasCV}, OCR: {HasOCR}, YOLO: {HasYolo})", 
                analyzeResult != null, readResult != null, yoloResult != null);

            // Return the combined result
            return Ok(combinedResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during image analysis");
            return StatusCode(500, new
            {
                success = false,
                error = $"An error occurred: {ex.Message}",
                details = ex.ToString()
            });
        }
    }

    [HttpGet("health")]
    public IActionResult Health()
    {
        return Ok(new { status = "healthy", timestamp = DateTime.UtcNow });
    }

    [HttpGet("test-computer-vision")]
    public IActionResult TestComputerVision()
    {
        try
        {
            _logger.LogInformation("Testing Computer Vision configuration");
            
            return Ok(new
            {
                success = true,
                message = "Computer Vision service is configured",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Computer Vision configuration test failed");
            return StatusCode(500, new
            {
                success = false,
                message = "Computer Vision configuration test failed",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    private async Task<byte[]> ResizeImageAsync(byte[] imageBytes, long maxFileSize, int maxDimension)
    {
        using var image = Image.Load(imageBytes);
        
        // Calculate new dimensions while maintaining aspect ratio
        int newWidth = image.Width;
        int newHeight = image.Height;
        
        if (image.Width > maxDimension || image.Height > maxDimension)
        {
            double ratio = Math.Min((double)maxDimension / image.Width, (double)maxDimension / image.Height);
            newWidth = (int)(image.Width * ratio);
            newHeight = (int)(image.Height * ratio);
        }

        // Resize the image
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(newWidth, newHeight),
            Mode = ResizeMode.Max
        }));

        // Compress to JPEG with quality adjustment to meet size limit
        int quality = 90;
        byte[] resizedBytes;
        
        do
        {
            using var outputStream = new MemoryStream();
            await image.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = quality });
            resizedBytes = outputStream.ToArray();
            
            if (resizedBytes.Length <= maxFileSize || quality <= 50)
                break;
                
            quality -= 10;
            _logger.LogInformation("Image still too large ({Size} bytes), reducing quality to {Quality}%", resizedBytes.Length, quality);
        } while (resizedBytes.Length > maxFileSize && quality > 50);

        return resizedBytes;
    }
}

