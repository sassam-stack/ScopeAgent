using System.Net.Http;

namespace ScopeAgent.Api.Services;

public class AzureBlobService : IAzureBlobService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AzureBlobService> _logger;

    public AzureBlobService(HttpClient httpClient, ILogger<AzureBlobService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<byte[]> DownloadPdfFromUrlAsync(string blobUrl)
    {
        try
        {
            _logger.LogInformation("Downloading PDF from URL: {Url}", blobUrl);
            
            var response = await _httpClient.GetAsync(blobUrl);
            response.EnsureSuccessStatusCode();
            
            var pdfBytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Successfully downloaded PDF, size: {Size} bytes", pdfBytes.Length);
            
            return pdfBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading PDF from URL: {Url}", blobUrl);
            throw;
        }
    }

    public async Task<byte[]> DownloadImageFromUrlAsync(string blobUrl)
    {
        try
        {
            _logger.LogInformation("Downloading image from URL: {Url}", blobUrl);
            
            var response = await _httpClient.GetAsync(blobUrl);
            response.EnsureSuccessStatusCode();
            
            var imageBytes = await response.Content.ReadAsByteArrayAsync();
            _logger.LogInformation("Successfully downloaded image, size: {Size} bytes", imageBytes.Length);
            
            return imageBytes;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error downloading image from URL: {Url}", blobUrl);
            throw;
        }
    }
}

