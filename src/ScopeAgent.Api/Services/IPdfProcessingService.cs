using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Service for processing PDF files and converting pages to images
/// </summary>
public interface IPdfProcessingService
{
    /// <summary>
    /// Convert a PDF page to a high-resolution image
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <param name="dpi">Resolution in DPI (default: 300)</param>
    /// <returns>Image bytes (PNG format)</returns>
    Task<byte[]> ConvertPageToImageAsync(byte[] pdfBytes, int pageNumber, int dpi = 300);

    /// <summary>
    /// Get the total number of pages in a PDF
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes</param>
    /// <returns>Number of pages</returns>
    Task<int> GetPageCountAsync(byte[] pdfBytes);

    /// <summary>
    /// Extract text from a specific page
    /// </summary>
    /// <param name="pdfBytes">PDF file bytes</param>
    /// <param name="pageNumber">Page number (1-based)</param>
    /// <returns>Extracted text</returns>
    Task<string> ExtractTextAsync(byte[] pdfBytes, int pageNumber);
}

