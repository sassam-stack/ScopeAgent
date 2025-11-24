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
        try
        {
            _logger.LogInformation("Detecting symbols in image");
            
            var url = $"{_config.ServiceUrl}/detect-symbols";
            using var content = new MultipartFormDataContent();
            var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(imageContent, "file", "image.png");

            var response = await _httpClient.PostAsync(new Uri(url), content);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            var symbols = new List<DetectedSymbol>();
            if (result.TryGetProperty("symbols", out var symbolsArray))
            {
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

            _logger.LogInformation("Detected {Count} symbols", symbols.Count);
            return symbols;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error detecting symbols");
            throw;
        }
    }
}

