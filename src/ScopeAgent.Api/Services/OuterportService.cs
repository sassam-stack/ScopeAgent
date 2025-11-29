using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace ScopeAgent.Api.Services;

public class OuterportService : IOuterportService
{
    private readonly ILogger<OuterportService> _logger;
    private readonly HttpClient _httpClient;
    private readonly OuterportConfig _config;

    public OuterportService(IOptions<OuterportConfig> config, ILogger<OuterportService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = null;
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        
        if (!string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _logger.LogInformation("Outerport service client initialized: {ServiceUrl}", _config.ServiceUrl);
        }
        else
        {
            _logger.LogWarning("Outerport service URL not configured.");
        }
    }

    public async Task<OuterportResult?> ProcessDrainagePlanAsync(byte[] imageBytes)
    {
        if (string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _logger.LogWarning("Outerport service not configured. Cannot process drainage plan.");
            return null;
        }

        if (string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogWarning("Outerport API key not configured. Returning mock data for testing.");
            return GenerateMockOuterportResult();
        }

        try
        {
            _logger.LogInformation("Processing drainage plan with Outerport service");
            
            var serviceUrl = _config.ServiceUrl.TrimEnd('/');
            var url = $"{serviceUrl}/process-drainage-plan";
            
            // Detect image format to set correct content type
            string contentType = "image/jpeg"; // default
            string fileName = "image.jpg";
            
            try
            {
                using var image = Image.Load(imageBytes);
                var format = image.Metadata.DecodedImageFormat;
                if (format != null)
                {
                    contentType = format.DefaultMimeType;
                    var extension = format.FileExtensions.FirstOrDefault() ?? "jpg";
                    fileName = $"image.{extension}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not detect image format, using default image/jpeg");
            }
            
            using var content = new MultipartFormDataContent();
            using var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            content.Add(imageContent, "file", fileName);
            
            // Add API key header
            _httpClient.DefaultRequestHeaders.Authorization = null; // Clear any existing auth
            _httpClient.DefaultRequestHeaders.Remove("X-API-Key");
            _httpClient.DefaultRequestHeaders.Add("X-API-Key", _config.ApiKey);
            
            _logger.LogInformation("Sending request to Outerport service at {Url}", url);
            var response = await _httpClient.PostAsync(new Uri(url), content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Outerport service returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Outerport service returned {response.StatusCode}: {errorContent}");
            }
            
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Outerport service response received: {Length} bytes", responseContent.Length);

            // Parse the response into OuterportResult
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            var result = new OuterportResult();
            
            // Parse junctions
            if (jsonElement.TryGetProperty("junctions", out var junctionsElement) && junctionsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var junctionElement in junctionsElement.EnumerateArray())
                {
                    var junction = new OuterportJunction();
                    
                    if (junctionElement.TryGetProperty("id", out var idElement))
                    {
                        junction.Id = idElement.GetString() ?? string.Empty;
                    }
                    
                    if (junctionElement.TryGetProperty("label_bbox", out var labelBboxElement) && labelBboxElement.ValueKind == JsonValueKind.Array)
                    {
                        var bboxArray = new List<int>();
                        foreach (var item in labelBboxElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Number)
                            {
                                bboxArray.Add(item.GetInt32());
                            }
                        }
                        junction.LabelBbox = bboxArray.ToArray();
                    }
                    
                    if (junctionElement.TryGetProperty("bbox", out var bboxElement))
                    {
                        if (bboxElement.ValueKind == JsonValueKind.Null)
                        {
                            junction.Bbox = null;
                        }
                        else if (bboxElement.ValueKind == JsonValueKind.Array)
                        {
                            var bboxArray = new List<int>();
                            foreach (var item in bboxElement.EnumerateArray())
                            {
                                if (item.ValueKind == JsonValueKind.Number)
                                {
                                    bboxArray.Add(item.GetInt32());
                                }
                            }
                            if (bboxArray.Count > 0)
                            {
                                junction.Bbox = bboxArray.ToArray();
                            }
                        }
                    }
                    
                    if (junctionElement.TryGetProperty("expected_directions", out var directionsElement))
                    {
                        if (directionsElement.ValueKind == JsonValueKind.Null)
                        {
                            junction.ExpectedDirections = null;
                        }
                        else if (directionsElement.ValueKind == JsonValueKind.Array)
                        {
                            var directions = new List<string>();
                            foreach (var dirElement in directionsElement.EnumerateArray())
                            {
                                if (dirElement.ValueKind == JsonValueKind.String)
                                {
                                    directions.Add(dirElement.GetString() ?? string.Empty);
                                }
                            }
                            junction.ExpectedDirections = directions.Count > 0 ? directions : null;
                        }
                    }
                    
                    result.Junctions.Add(junction);
                }
            }
            
            // Parse materials
            if (jsonElement.TryGetProperty("materials", out var materialsElement) && materialsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var materialElement in materialsElement.EnumerateArray())
                {
                    var material = new OuterportMaterial();
                    
                    if (materialElement.TryGetProperty("text", out var textElement))
                    {
                        material.Text = textElement.GetString() ?? string.Empty;
                    }
                    
                    if (materialElement.TryGetProperty("bbox", out var bboxElement) && bboxElement.ValueKind == JsonValueKind.Array)
                    {
                        var bboxArray = new List<int>();
                        foreach (var item in bboxElement.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.Number)
                            {
                                bboxArray.Add(item.GetInt32());
                            }
                        }
                        material.Bbox = bboxArray.ToArray();
                    }
                    
                    result.Materials.Add(material);
                }
            }

            _logger.LogInformation("Outerport processing completed. Found {JunctionCount} junctions and {MaterialCount} materials", 
                result.Junctions.Count, result.Materials.Count);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing drainage plan with Outerport service");
            throw;
        }
    }

    private OuterportResult GenerateMockOuterportResult()
    {
        _logger.LogInformation("Generating mock Outerport results for testing");

        var result = new OuterportResult
        {
            Junctions = new List<OuterportJunction>
            {
                new OuterportJunction
                {
                    Id = "S-6",
                    LabelBbox = new int[] { 246, 281, 271, 296 },
                    Bbox = new int[] { 224, 280, 246, 297 },
                    ExpectedDirections = new List<string> { "NIE", "SIE" }
                },
                new OuterportJunction
                {
                    Id = "YD-9",
                    LabelBbox = new int[] { 371, 130, 398, 143 },
                    Bbox = new int[] { 231, 511, 247, 523 },
                    ExpectedDirections = null
                },
                new OuterportJunction
                {
                    Id = "S-7",
                    LabelBbox = new int[] { 808, 286, 835, 299 },
                    Bbox = new int[] { 785, 285, 807, 300 },
                    ExpectedDirections = new List<string> { "N", "E" }
                },
                new OuterportJunction
                {
                    Id = "S-1",
                    LabelBbox = new int[] { 150, 200, 175, 213 },
                    Bbox = new int[] { 128, 199, 149, 214 },
                    ExpectedDirections = new List<string> { "N", "S", "E", "W" }
                },
                new OuterportJunction
                {
                    Id = "S-2",
                    LabelBbox = new int[] { 300, 350, 325, 363 },
                    Bbox = new int[] { 278, 349, 299, 364 },
                    ExpectedDirections = new List<string> { "NIE", "SW" }
                }
            },
            Materials = new List<OuterportMaterial>
            {
                new OuterportMaterial
                {
                    Text = "VU100",
                    Bbox = new int[] { 400, 500, 450, 520 }
                },
                new OuterportMaterial
                {
                    Text = "12\" HDPE",
                    Bbox = new int[] { 600, 400, 680, 420 }
                },
                new OuterportMaterial
                {
                    Text = "18\" RCP",
                    Bbox = new int[] { 200, 600, 270, 620 }
                },
                new OuterportMaterial
                {
                    Text = "6\" HDPE",
                    Bbox = new int[] { 500, 300, 560, 320 }
                }
            }
        };

        _logger.LogInformation("Generated mock Outerport results: {JunctionCount} junctions, {MaterialCount} materials",
            result.Junctions.Count, result.Materials.Count);

        return result;
    }
}

