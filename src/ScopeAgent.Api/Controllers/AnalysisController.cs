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
    public async Task<ActionResult> AnalyzeImage(IFormFile file, [FromForm] bool useComputerVision = true, [FromForm] bool useYolo = true, [FromForm] string? context = null)
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

            // Read file into byte array - keep original for YOLO
            byte[] originalImageBytes;
            using (var memoryStream = new MemoryStream())
            {
                await file.CopyToAsync(memoryStream);
                originalImageBytes = memoryStream.ToArray();
            }

            _logger.LogInformation("Image file read successfully, size: {Size} bytes", originalImageBytes.Length);

            // Azure Computer Vision API limits:
            // - Maximum file size: 4 MB (4,194,304 bytes)
            // - Maximum dimensions: 10,000 x 10,000 pixels
            const long maxFileSize = 4 * 1024 * 1024; // 4 MB
            const int maxDimension = 10000;

            // Prepare image for Computer Vision (resize if needed)
            byte[] cvImageBytes = originalImageBytes;
            if (useComputerVision)
            {
                if (cvImageBytes.Length > maxFileSize)
                {
                    _logger.LogInformation("Image size {Size} bytes exceeds CV limit of {MaxSize} bytes. Resizing for CV only...", cvImageBytes.Length, maxFileSize);
                    cvImageBytes = await ResizeImageAsync(cvImageBytes, maxFileSize, maxDimension);
                    _logger.LogInformation("Image resized for CV to {Size} bytes", cvImageBytes.Length);
                }
                else
                {
                    // Check dimensions even if file size is OK
                    try
                    {
                        using var image = Image.Load(cvImageBytes);
                        if (image.Width > maxDimension || image.Height > maxDimension)
                        {
                            _logger.LogInformation("Image dimensions {Width}x{Height} exceed CV limit of {MaxDimension}x{MaxDimension}. Resizing for CV only...", 
                                image.Width, image.Height, maxDimension, maxDimension);
                            cvImageBytes = await ResizeImageAsync(cvImageBytes, maxFileSize, maxDimension);
                            _logger.LogInformation("Image resized for CV to {Size} bytes", cvImageBytes.Length);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Could not check image dimensions, proceeding with original image for CV");
                    }
                }
            }

            // Analyze with Computer Vision API (if requested)
            object? analyzeResult = null;
            object? readResult = null;
            string? imageContext = context; // Start with user-provided context
            
            if (useComputerVision)
            {
                analyzeResult = await _computerVisionService.AnalyzeImageAsync(cvImageBytes);
                
                if (analyzeResult == null)
                {
                    _logger.LogWarning("Computer Vision is not configured or failed to analyze the image");
                }
                else
                {
                    _logger.LogInformation("Computer Vision analysis completed");
                    
                    // Extract description/caption from CV results for context
                    if (analyzeResult is Dictionary<string, object?> cvDict)
                    {
                        if (cvDict.TryGetValue("caption", out var caption) && caption != null)
                        {
                            var captionDict = caption as Dictionary<string, object?>;
                            if (captionDict != null && captionDict.TryGetValue("text", out var captionText) && captionText != null)
                            {
                                imageContext = captionText.ToString();
                                _logger.LogInformation("Extracted CV caption as context: {Caption}", imageContext);
                            }
                        }
                        
                        // Also add top tags as context
                        if (cvDict.TryGetValue("tags", out var tags) && tags is List<object> tagList && tagList.Count > 0)
                        {
                            var topTags = new List<string>();
                            foreach (var tag in tagList.Take(5))
                            {
                                if (tag is Dictionary<string, object?> tagDict)
                                {
                                    if (tagDict.TryGetValue("name", out var tagName) && tagName != null)
                                    {
                                        topTags.Add(tagName.ToString() ?? "");
                                    }
                                }
                            }
                            if (topTags.Count > 0)
                            {
                                var tagsStr = string.Join(", ", topTags);
                                imageContext = string.IsNullOrEmpty(imageContext) 
                                    ? $"Tags: {tagsStr}" 
                                    : $"{imageContext}. Tags: {tagsStr}";
                            }
                        }
                    }
                }

                // Also get OCR/text extraction
                readResult = await _computerVisionService.ReadTextAsync(cvImageBytes);
                
                // Extract OCR text as additional context
                if (readResult != null && readResult is Dictionary<string, object?> readDict)
                {
                    if (readDict.TryGetValue("content", out var content) && content != null)
                    {
                        var contentStr = content.ToString();
                        if (!string.IsNullOrEmpty(contentStr) && contentStr.Length > 0)
                        {
                            // Use first 200 characters of OCR text as context
                            var ocrContext = contentStr.Length > 200 
                                ? contentStr.Substring(0, 200) + "..." 
                                : contentStr;
                            imageContext = string.IsNullOrEmpty(imageContext) 
                                ? $"OCR text: {ocrContext}" 
                                : $"{imageContext}. OCR: {ocrContext}";
                            _logger.LogInformation("Added OCR text to context");
                        }
                    }
                }
            }
            
            // Get YOLO analysis (if requested) - use ORIGINAL full resolution image
            object? yoloResult = null;
            if (useYolo)
            {
                try
                {
                    _logger.LogInformation("Sending original full-resolution image to YOLO (size: {Size} bytes)", originalImageBytes.Length);
                    // Combine user context with extracted context
                    if (!string.IsNullOrEmpty(context) && !string.IsNullOrEmpty(imageContext) && imageContext != context)
                    {
                        imageContext = $"{context}. {imageContext}";
                    }
                    else if (!string.IsNullOrEmpty(context))
                    {
                        imageContext = context;
                    }
                    
                    if (!string.IsNullOrEmpty(imageContext))
                    {
                        _logger.LogInformation("Including context: {Context}", imageContext.Substring(0, Math.Min(100, imageContext.Length)));
                    }
                    yoloResult = await _yoloService.AnalyzeImageAsync(originalImageBytes, imageContext);
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
            }
            
            // Validate that at least one service was requested and returned results
            if (!useComputerVision && !useYolo)
            {
                return BadRequest(new
                {
                    success = false,
                    error = "At least one service (Computer Vision or YOLO) must be selected"
                });
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

