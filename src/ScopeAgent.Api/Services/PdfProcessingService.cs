using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using Microsoft.Extensions.Logging;
using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Service for processing PDF files and converting pages to images
/// Note: PDF to image conversion requires a Python service with pdf2image library
/// </summary>
public class PdfProcessingService : IPdfProcessingService
{
    private readonly ILogger<PdfProcessingService> _logger;

    public PdfProcessingService(ILogger<PdfProcessingService> logger)
    {
        _logger = logger;
    }

    public async Task<byte[]> ConvertPageToImageAsync(byte[] pdfBytes, int pageNumber, int dpi = 300)
    {
        // TODO: Implement PDF to image conversion
        // This requires either:
        // 1. PdfiumViewer library (requires native DLLs)
        // 2. Python service with pdf2image library (recommended)
        // 3. Ghostscript wrapper
        
        // For now, throw NotImplementedException - will be implemented via Python service
        _logger.LogWarning("PDF to image conversion not yet implemented. Will use Python service.");
        throw new NotImplementedException("PDF to image conversion will be implemented via Python service in STEP-1.4");
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

