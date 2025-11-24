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
        _httpClient.Timeout = TimeSpan.FromSeconds(120); // Longer timeout for PDF conversion
        _imageProcessingConfig = imageProcessingConfig.Value;
    }

    public async Task<byte[]> ConvertPageToImageAsync(byte[] pdfBytes, int pageNumber, int dpi = 300)
    {
        try
        {
            _logger.LogInformation("Converting PDF page {PageNumber} to image via Python service (DPI: {Dpi})", pageNumber, dpi);
            
            var serviceUrl = _imageProcessingConfig.ServiceUrl.TrimEnd('/');
            var url = $"{serviceUrl}/convert-pdf-page?page_number={pageNumber}&dpi={dpi}";
            
            using var content = new MultipartFormDataContent();
            var pdfContent = new ByteArrayContent(pdfBytes);
            pdfContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            content.Add(pdfContent, "file", "document.pdf");
            
            var response = await _httpClient.PostAsync(new Uri(url), content);
            
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
                    _logger.LogInformation("Successfully converted PDF page {PageNumber} to image ({Size} bytes)", 
                        pageNumber, imageBytes.Length);
                    return imageBytes;
                }
            }
            
            throw new InvalidOperationException("Failed to get image from Python service response");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Failed to convert PDF page via Python service, falling back to NotImplementedException");
            throw new NotImplementedException("PDF to image conversion service unavailable", ex);
        }
        catch (NotImplementedException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting PDF page to image");
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

