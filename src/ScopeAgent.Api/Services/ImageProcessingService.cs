using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Configuration for Image Processing Service
/// </summary>
public class ImageProcessingConfig
{
    public string ServiceUrl { get; set; } = "http://localhost:8001";
    public int TimeoutSeconds { get; set; } = 60;
}

/// <summary>
/// Service for image processing operations (delegates to Python service)
/// </summary>
public class ImageProcessingService : IImageProcessingService
{
    private readonly ILogger<ImageProcessingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ImageProcessingConfig _config;

    public ImageProcessingService(
        IOptions<ImageProcessingConfig> config,
        ILogger<ImageProcessingService> logger,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = null;
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<List<LineSegment>> DetectLinesAsync(byte[] imageBytes)
    {
        try
        {
            _logger.LogInformation("Detecting lines in image");
            
            var url = $"{_config.ServiceUrl}/detect-lines";
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "image.png");

            var response = await _httpClient.PostAsync(new Uri(url), content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var lines = new List<LineSegment>();
            if (result.TryGetProperty("lines", out var linesArray))
            {
                foreach (var line in linesArray.EnumerateArray())
                {
                    var lineSegment = new LineSegment();
                    
                    if (line.TryGetProperty("startPoint", out var start))
                    {
                        lineSegment.StartPoint = new Point
                        {
                            X = start.TryGetProperty("x", out var sx) ? sx.GetDouble() : 0,
                            Y = start.TryGetProperty("y", out var sy) ? sy.GetDouble() : 0
                        };
                    }
                    
                    if (line.TryGetProperty("endPoint", out var end))
                    {
                        lineSegment.EndPoint = new Point
                        {
                            X = end.TryGetProperty("x", out var ex) ? ex.GetDouble() : 0,
                            Y = end.TryGetProperty("y", out var ey) ? ey.GetDouble() : 0
                        };
                    }
                    
                    lines.Add(lineSegment);
                }
            }

            _logger.LogInformation("Detected {Count} lines", lines.Count);
            return lines;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting lines");
            throw;
        }
    }

    public async Task<List<Circle>> DetectCirclesAsync(byte[] imageBytes)
    {
        try
        {
            _logger.LogInformation("Detecting circles in image");
            
            var url = $"{_config.ServiceUrl}/detect-circles";
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "image.png");

            var response = await _httpClient.PostAsync(new Uri(url), content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var circles = new List<Circle>();
            if (result.TryGetProperty("circles", out var circlesArray))
            {
                foreach (var circle in circlesArray.EnumerateArray())
                {
                    var circleObj = new Circle();
                    
                    if (circle.TryGetProperty("center", out var center))
                    {
                        circleObj.Center = new Point
                        {
                            X = center.TryGetProperty("x", out var cx) ? cx.GetDouble() : 0,
                            Y = center.TryGetProperty("y", out var cy) ? cy.GetDouble() : 0
                        };
                    }
                    
                    if (circle.TryGetProperty("radius", out var radius))
                    {
                        circleObj.Radius = radius.GetDouble();
                    }
                    
                    circles.Add(circleObj);
                }
            }

            _logger.LogInformation("Detected {Count} circles", circles.Count);
            return circles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting circles");
            throw;
        }
    }

    public async Task<List<Rectangle>> DetectRectanglesAsync(byte[] imageBytes)
    {
        try
        {
            _logger.LogInformation("Detecting rectangles in image");
            
            var url = $"{_config.ServiceUrl}/detect-rectangles";
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "image.png");

            var response = await _httpClient.PostAsync(new Uri(url), content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var rectangles = new List<Rectangle>();
            if (result.TryGetProperty("rectangles", out var rectanglesArray))
            {
                foreach (var rect in rectanglesArray.EnumerateArray())
                {
                    var rectangle = new Rectangle
                    {
                        X = rect.TryGetProperty("x", out var x) ? x.GetDouble() : 0,
                        Y = rect.TryGetProperty("y", out var y) ? y.GetDouble() : 0,
                        Width = rect.TryGetProperty("width", out var w) ? w.GetDouble() : 0,
                        Height = rect.TryGetProperty("height", out var h) ? h.GetDouble() : 0
                    };
                    
                    if (rect.TryGetProperty("points", out var points))
                    {
                        foreach (var point in points.EnumerateArray())
                        {
                            rectangle.Points.Add(new Point
                            {
                                X = point[0].GetDouble(),
                                Y = point[1].GetDouble()
                            });
                        }
                    }
                    
                    rectangles.Add(rectangle);
                }
            }

            _logger.LogInformation("Detected {Count} rectangles", rectangles.Count);
            return rectangles;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting rectangles");
            throw;
        }
    }

    public async Task<byte[]> CropImageAsync(byte[] imageBytes, BoundingBox boundingBox)
    {
        try
        {
            _logger.LogInformation("Cropping image");
            
            if (!boundingBox.X.HasValue || !boundingBox.Y.HasValue || 
                !boundingBox.Width.HasValue || !boundingBox.Height.HasValue)
            {
                throw new ArgumentException("Bounding box must have x, y, width, and height");
            }
            
            var url = $"{_config.ServiceUrl}/crop-image?x={(int)boundingBox.X.Value}&y={(int)boundingBox.Y.Value}&width={(int)boundingBox.Width.Value}&height={(int)boundingBox.Height.Value}";
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "image.png");

            var response = await _httpClient.PostAsync(new Uri(url), content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            if (result.TryGetProperty("croppedImage", out var croppedImageBase64))
            {
                var base64String = croppedImageBase64.GetString();
                if (!string.IsNullOrEmpty(base64String))
                {
                    return Convert.FromBase64String(base64String);
                }
            }

            throw new InvalidOperationException("Failed to get cropped image from service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cropping image");
            throw;
        }
    }

    public async Task<List<DetectedSymbol>> DetectSymbolsAsync(byte[] imageBytes)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            _logger.LogInformation("Detecting symbols in image ({Size} bytes, ~{SizeMB:F2} MB) via {ServiceUrl}/detect-symbols", 
                imageBytes.Length, imageBytes.Length / (1024.0 * 1024.0), _config.ServiceUrl);
            
            // Quick health check - verify service is reachable (with short timeout)
            try
            {
                using var healthCheckCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var healthCheck = await _httpClient.GetAsync(new Uri($"{_config.ServiceUrl}/health"), healthCheckCts.Token);
                _logger.LogDebug("Image processing service is reachable (health check returned {StatusCode})", healthCheck.StatusCode);
            }
            catch (Exception healthEx)
            {
                _logger.LogWarning(healthEx, "Health check failed for image processing service at {ServiceUrl}. " +
                    "Service may be unavailable, but continuing with symbol detection request anyway.", _config.ServiceUrl);
            }
            
            var url = $"{_config.ServiceUrl}/detect-symbols";
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "image.png");

            // Use a longer timeout for symbol detection (3x the default, or minimum 180 seconds)
            var symbolDetectionTimeout = TimeSpan.FromSeconds(Math.Max(_config.TimeoutSeconds * 3, 180));
            _logger.LogInformation("Using timeout of {TimeoutSeconds} seconds for symbol detection (default timeout: {DefaultTimeoutSeconds}s)", 
                symbolDetectionTimeout.TotalSeconds, _config.TimeoutSeconds);
            
            // Create a separate HttpClient with extended timeout for symbol detection
            // This is necessary because symbol detection can take much longer than other operations
            using var symbolDetectionClient = new HttpClient
            {
                Timeout = symbolDetectionTimeout
            };
            
            _logger.LogDebug("Sending POST request to {Url}", url);
            var response = await symbolDetectionClient.PostAsync(new Uri(url), content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Image processing service returned error: {StatusCode} - {Error}", 
                    response.StatusCode, errorContent);
                throw new HttpRequestException($"Image processing service returned {response.StatusCode}: {errorContent}");
            }

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogDebug("Received response from image processing service: {Length} characters", responseContent.Length);
            
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var symbols = new List<DetectedSymbol>();
            if (result.TryGetProperty("symbols", out var symbolsArray))
            {
                _logger.LogDebug("Found 'symbols' array in response with {Count} items", 
                    symbolsArray.GetArrayLength());
                
                foreach (var symbol in symbolsArray.EnumerateArray())
                {
                    var detectedSymbol = new DetectedSymbol();
                    
                    // Parse symbol type
                    if (symbol.TryGetProperty("type", out var type))
                    {
                        var typeStr = type.GetString() ?? "Unknown";
                        detectedSymbol.Type = typeStr switch
                        {
                            "DoubleRectangle" => SymbolType.DoubleRectangle,
                            "CircleWithGrid" => SymbolType.CircleWithGrid,
                            "Oval" => SymbolType.Oval,
                            _ => SymbolType.Unknown
                        };
                    }
                    
                    // Parse bounding box
                    if (symbol.TryGetProperty("boundingBox", out var bbox))
                    {
                        detectedSymbol.BoundingBox = new BoundingBox();
                        
                        // Parse points if available (preferred method)
                        if (bbox.TryGetProperty("points", out var points))
                        {
                            var pointsList = new List<double>();
                            foreach (var point in points.EnumerateArray())
                            {
                                if (point.ValueKind == JsonValueKind.Array && point.GetArrayLength() >= 2)
                                {
                                    pointsList.Add(point[0].GetDouble());
                                    pointsList.Add(point[1].GetDouble());
                                }
                            }
                            if (pointsList.Count >= 8)
                            {
                                detectedSymbol.BoundingBox.FromPoints(pointsList);
                            }
                        }
                        else
                        {
                            // Fallback to rectangle format
                            detectedSymbol.BoundingBox.X = bbox.TryGetProperty("x", out var x) ? x.GetDouble() : null;
                            detectedSymbol.BoundingBox.Y = bbox.TryGetProperty("y", out var y) ? y.GetDouble() : null;
                            detectedSymbol.BoundingBox.Width = bbox.TryGetProperty("width", out var w) ? w.GetDouble() : null;
                            detectedSymbol.BoundingBox.Height = bbox.TryGetProperty("height", out var h) ? h.GetDouble() : null;
                        }
                    }
                    
                    // Parse confidence
                    if (symbol.TryGetProperty("confidence", out var confidence))
                    {
                        detectedSymbol.Confidence = confidence.GetDouble();
                    }
                    
                    symbols.Add(detectedSymbol);
                }
            }
            else
            {
                _logger.LogWarning("Response from image processing service does not contain 'symbols' property. Full response: {Response}", 
                    responseContent);
            }

            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogInformation("Successfully parsed {Count} symbols from image processing service response (took {ElapsedSeconds:F2} seconds)", 
                symbols.Count, elapsed.TotalSeconds);
            return symbols;
        }
        catch (HttpRequestException ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "HTTP error detecting symbols after {ElapsedSeconds:F2} seconds: {Message}. " +
                "Check if Python service is running at {ServiceUrl}", elapsed.TotalSeconds, ex.Message, _config.ServiceUrl);
            throw;
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Timeout detecting symbols after {ElapsedSeconds:F2} seconds (configured timeout: {TimeoutSeconds}s). " +
                "The Python service at {ServiceUrl} may be slow, unavailable, or the image is too large ({SizeMB:F2} MB). " +
                "Consider: 1) Checking if the Python service is running, 2) Reducing image size/DPI, 3) Increasing TimeoutSeconds in appsettings.json", 
                elapsed.TotalSeconds, _config.TimeoutSeconds, _config.ServiceUrl, imageBytes.Length / (1024.0 * 1024.0));
            throw;
        }
        catch (TaskCanceledException ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Request canceled after {ElapsedSeconds:F2} seconds (service may be unavailable or slow)", elapsed.TotalSeconds);
            throw;
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Unexpected error detecting symbols after {ElapsedSeconds:F2} seconds: {Message}. Stack trace: {StackTrace}", 
                elapsed.TotalSeconds, ex.Message, ex.StackTrace);
            throw;
        }
    }
}

