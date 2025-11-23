using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;

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

    public async Task<object?> AnalyzeImageAsync(byte[] imageBytes)
    {
        if (string.IsNullOrEmpty(_config.ServiceUrl))
        {
            _logger.LogWarning("YOLO service not configured. Cannot analyze image.");
            return null;
        }

        try
        {
            _logger.LogInformation("Analyzing image with YOLO service");
            
            var serviceUrl = _config.ServiceUrl.TrimEnd('/');
            var url = $"{serviceUrl}/analyze";
            
            using var content = new MultipartFormDataContent();
            using var imageContent = new ByteArrayContent(imageBytes);
            imageContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(imageContent, "file", "image.jpg");
            
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

