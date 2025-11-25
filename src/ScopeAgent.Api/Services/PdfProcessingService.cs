using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ScopeAgent.Api.Models.DrainageAnalysis;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Service for processing PDF files and converting pages to images
/// Note: PDF to image conversion requires a Python service with pdf2image library
/// </summary>
public class PdfProcessingService : IPdfProcessingService
{
    private readonly ILogger<PdfProcessingService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ImageProcessingConfig _imageProcessingConfig;

    public PdfProcessingService(
        ILogger<PdfProcessingService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<ImageProcessingConfig> imageProcessingConfig)
    {
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient();
        _httpClient.BaseAddress = null;
        // Use a much longer timeout for PDF conversion (10 minutes) since large PDFs can take time
        // This includes time to upload the PDF and process it on the Python service
        _httpClient.Timeout = TimeSpan.FromMinutes(10);
        _imageProcessingConfig = imageProcessingConfig.Value;
    }

    public async Task<byte[]> ConvertPageToImageAsync(byte[] pdfBytes, int pageNumber, int dpi = 300)
    {
        var startTime = DateTime.UtcNow;
        try
        {
            var pdfSizeMB = pdfBytes.Length / (1024.0 * 1024.0);
            _logger.LogInformation("Converting PDF page {PageNumber} to image via Python service (DPI: {Dpi}, PDF size: {SizeMB:F2} MB, {Size} bytes)", 
                pageNumber, dpi, pdfSizeMB, pdfBytes.Length);
            
            var serviceUrl = _imageProcessingConfig.ServiceUrl.TrimEnd('/');
            var url = $"{serviceUrl}/convert-pdf-page?page_number={pageNumber}&dpi={dpi}";
            
            _logger.LogInformation("Uploading PDF to Python service at {Url} (this may take a while for large PDFs)...", url);
            
            using var content = new MultipartFormDataContent();
            var pdfContent = new ByteArrayContent(pdfBytes);
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(pdfContent, "file", "document.pdf");
            
            _logger.LogInformation("Sending PDF conversion request (timeout: {TimeoutMinutes} minutes)", _httpClient.Timeout.TotalMinutes);
            var response = await _httpClient.PostAsync(new Uri(url), content);
            
            var uploadTime = DateTime.UtcNow - startTime;
            _logger.LogInformation("Received response from Python service after {ElapsedSeconds:F2} seconds", uploadTime.TotalSeconds);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Python service returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotImplemented || 
                    response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    throw new NotImplementedException($"PDF to image conversion not available: {errorContent}");
                }
                
                throw new HttpRequestException($"Python service returned {response.StatusCode}: {errorContent}");
            }
            
            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);
            
            if (result.TryGetProperty("image", out var imageBase64))
            {
                var base64String = imageBase64.GetString();
                if (!string.IsNullOrEmpty(base64String))
                {
                    var imageBytes = Convert.FromBase64String(base64String);
                    var totalTime = DateTime.UtcNow - startTime;
                    var imageSizeMB = imageBytes.Length / (1024.0 * 1024.0);
                    _logger.LogInformation("Successfully converted PDF page {PageNumber} to image ({SizeMB:F2} MB, {Size} bytes) in {TotalSeconds:F2} seconds", 
                        pageNumber, imageSizeMB, imageBytes.Length, totalTime.TotalSeconds);
                    return imageBytes;
                }
            }
            
            throw new InvalidOperationException("Failed to get image from Python service response");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            var elapsed = DateTime.UtcNow - startTime;
            var pdfSizeMB = pdfBytes.Length / (1024.0 * 1024.0);
            _logger.LogError(ex, "PDF conversion timed out after {ElapsedSeconds:F2} seconds (timeout: {TimeoutMinutes} minutes). " +
                "PDF size: {SizeMB:F2} MB. The PDF may be too large or the Python service is slow. " +
                "Consider: 1) Reducing DPI, 2) Checking if Python service is running, 3) Processing smaller PDFs", 
                elapsed.TotalSeconds, _httpClient.Timeout.TotalMinutes, pdfSizeMB);
            throw new NotImplementedException($"PDF to image conversion timed out after {elapsed.TotalSeconds:F2} seconds", ex);
        }
        catch (TaskCanceledException ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "PDF conversion request was canceled after {ElapsedSeconds:F2} seconds", elapsed.TotalSeconds);
            throw new NotImplementedException("PDF to image conversion was canceled", ex);
        }
        catch (HttpRequestException ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogWarning(ex, "Failed to convert PDF page via Python service after {ElapsedSeconds:F2} seconds, falling back to NotImplementedException", elapsed.TotalSeconds);
            throw new NotImplementedException("PDF to image conversion service unavailable", ex);
        }
        catch (NotImplementedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var elapsed = DateTime.UtcNow - startTime;
            _logger.LogError(ex, "Error converting PDF page to image after {ElapsedSeconds:F2} seconds: {Message}", elapsed.TotalSeconds, ex.Message);
            throw new NotImplementedException("PDF to image conversion failed", ex);
        }
    }

    public async Task<int> GetPageCountAsync(byte[] pdfBytes)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var stream = new MemoryStream(pdfBytes);
                using var pdfReader = new PdfReader(stream);
                using var pdfDocument = new PdfDocument(pdfReader);
                
                var pageCount = pdfDocument.GetNumberOfPages();
                _logger.LogInformation("PDF has {PageCount} pages", pageCount);
                return pageCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting page count from PDF");
                throw;
            }
        });
    }

    public async Task<string> ExtractTextAsync(byte[] pdfBytes, int pageNumber)
    {
        return await Task.Run(() =>
        {
            try
            {
                using var stream = new MemoryStream(pdfBytes);
                using var pdfReader = new PdfReader(stream);
                using var pdfDocument = new PdfDocument(pdfReader);
                
                if (pageNumber < 1 || pageNumber > pdfDocument.GetNumberOfPages())
                {
                    throw new ArgumentOutOfRangeException(nameof(pageNumber), 
                        $"Page number must be between 1 and {pdfDocument.GetNumberOfPages()}");
                }
                
                var page = pdfDocument.GetPage(pageNumber);
                var strategy = new SimpleTextExtractionStrategy();
                var text = PdfTextExtractor.GetTextFromPage(page, strategy);
                
                _logger.LogInformation("Extracted text from page {PageNumber}", pageNumber);
                return text;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF page {PageNumber}", pageNumber);
                throw;
            }
        });
    }
}

