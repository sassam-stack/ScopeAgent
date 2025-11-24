using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;

namespace ScopeAgent.Api.Services;

public class YoloService : IYoloService
{
    private readonly ILogger<YoloService> _logger;
    private readonly HttpClient _httpClient;
    private readonly YoloConfig _config;

    public YoloService(IOptions<YoloConfig> config, ILogger<YoloService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = null;
        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
        
        if (!string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _logger.LogInformation("YOLO service client initialized: {ServiceUrl}", _config.ServiceUrl);
        }
        else
        {
            _logger.LogWarning("YOLO service URL not configured.");
        }
    }

    public async Task<object?> AnalyzeImageAsync(byte[] imageBytes, string? context = null)
    {
        if (string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _logger.LogWarning("YOLO service not configured. Cannot analyze image.");
            return null;
        }

        try
        {
            _logger.LogInformation("Analyzing image with YOLO service");
            if (!string.IsNullOrEmpty(context))
            {
                _logger.LogInformation("Context provided: {Context}", context.Substring(0, Math.Min(100, context.Length)));
            }
            
            var serviceUrl = _config.ServiceUrl.TrimEnd('/');
            var url = $"{serviceUrl}/analyze";
            
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
                    // Set appropriate file extension (format.FileExtensions returns extensions like "jpg", "png", etc.)
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
            
            // Add context if provided
            if (!string.IsNullOrEmpty(context))
            {
                content.Add(new StringContent(context), "context");
            }
            
            var response = await _httpClient.PostAsync(new Uri(url), content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("YOLO service returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return null;
            }
            
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            _logger.LogInformation("YOLO analysis completed successfully");

            return result;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex, "YOLO service request timed out after {Timeout} seconds", _config.TimeoutSeconds);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image with YOLO service");
            return null; // Return null instead of throwing to allow graceful degradation
        }
    }
}

