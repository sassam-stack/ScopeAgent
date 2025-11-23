using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Options;

namespace ScopeAgent.Api.Services;

public class PdfAnalysisService : IPdfAnalysisService
{
    private readonly ILogger<PdfAnalysisService> _logger;
    private readonly DocumentIntelligenceClient? _documentIntelligenceClient;

    public PdfAnalysisService(IOptions<DocumentIntelligenceConfig> config, ILogger<PdfAnalysisService> logger)
    {
        _logger = logger;
        
        if (!string.IsNullOrEmpty(config.Value.Endpoint) && !string.IsNullOrEmpty(config.Value.ApiKey))
        {
            var endpoint = config.Value.Endpoint.TrimEnd('/');
            _documentIntelligenceClient = new DocumentIntelligenceClient(
                new Uri(endpoint),
                new AzureKeyCredential(config.Value.ApiKey)
            );
            _logger.LogInformation("Document Intelligence client initialized");
        }
        else
        {
            _logger.LogWarning("Document Intelligence not configured. Will fall back to iText7.");
        }
    }

    public async Task<string> ExtractTextFromPdfAsync(byte[] pdfBytes)
    {
        // Use Document Intelligence if available, otherwise fall back to iText7
        if (_documentIntelligenceClient != null)
        {
            try
            {
                _logger.LogInformation("Extracting text using Azure Document Intelligence");
                
                var content = new BinaryData(pdfBytes);

                var operation = await _documentIntelligenceClient.AnalyzeDocumentAsync(
                    WaitUntil.Completed,
                    "prebuilt-read",
                    content);

                var result = operation.Value;
                
                var text = new System.Text.StringBuilder();
                
                // Document Intelligence returns content organized by pages
                if (result.Pages != null && result.Pages.Count > 0)
                {
                    foreach (var page in result.Pages)
                    {
                        var pageNumber = page.PageNumber;
                        text.AppendLine($"=== PAGE {pageNumber} ===");
                        
                        // Extract text from paragraphs on this page
                        if (result.Paragraphs != null)
                        {
                            var pageParagraphs = result.Paragraphs
                                .Where(p => p.BoundingRegions != null && 
                                           p.BoundingRegions.Any(br => br.PageNumber == pageNumber));
                            
                            foreach (var paragraph in pageParagraphs)
                            {
                                if (!string.IsNullOrWhiteSpace(paragraph.Content))
                                {
                                    text.AppendLine(paragraph.Content);
                                }
                            }
                        }
                        else if (result.Content != null)
                        {
                            // Fallback to full content if paragraphs aren't available
                            text.AppendLine(result.Content);
                        }
                        
                        text.AppendLine();
                    }
                }
                else if (result.Content != null)
                {
                    // If no page structure, use full content
                    text.AppendLine(result.Content);
                }
                
                _logger.LogInformation("Text extraction completed using Document Intelligence");
                return text.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text with Document Intelligence, falling back to iText7");
                return await ExtractTextWithIText7Async(pdfBytes);
            }
        }
        
        return await ExtractTextWithIText7Async(pdfBytes);
    }

    public async Task<List<int>> IdentifyDrainagePagesAsync(byte[] pdfBytes)
    {
        // Use Document Intelligence if available for better text extraction
        string pdfText;
        if (_documentIntelligenceClient != null)
        {
            try
            {
                pdfText = await ExtractTextFromPdfAsync(pdfBytes);
            }
            catch
            {
                pdfText = await ExtractTextWithIText7Async(pdfBytes);
            }
        }
        else
        {
            pdfText = await ExtractTextWithIText7Async(pdfBytes);
        }

        var drainagePages = new List<int>();
        var drainageKeywords = new[] { "drainage", "drain", "module", "m1", "m2", "m3", "m4", "compass", "north", "scheme", "schema" };
        
        var lines = pdfText.Split('\n');
        int currentPage = 1;
        
        foreach (var line in lines)
        {
            if (line.StartsWith("=== PAGE "))
            {
                var pageMatch = System.Text.RegularExpressions.Regex.Match(line, @"PAGE (\d+)");
                if (pageMatch.Success)
                {
                    currentPage = int.Parse(pageMatch.Groups[1].Value);
                }
            }
            else if (!string.IsNullOrWhiteSpace(line))
            {
                var lowerLine = line.ToLower();
                if (drainageKeywords.Any(keyword => lowerLine.Contains(keyword)))
                {
                    if (!drainagePages.Contains(currentPage))
                    {
                        drainagePages.Add(currentPage);
                        _logger.LogInformation("Identified page {PageNumber} as potential drainage page", currentPage);
                    }
                }
            }
        }
        
        return drainagePages;
    }


    public async Task<AnalyzeResult?> AnalyzeDocumentRawAsync(byte[] pdfBytes)
    {
        if (_documentIntelligenceClient == null)
        {
            _logger.LogWarning("Document Intelligence not configured. Cannot analyze document.");
            return null;
        }

        try
        {
            _logger.LogInformation("Analyzing document with Azure Document Intelligence");
            
            var content = new BinaryData(pdfBytes);

            var operation = await _documentIntelligenceClient.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                "prebuilt-read",
                content);

            var result = operation.Value;
            _logger.LogInformation("Document analysis completed");
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing document with Document Intelligence");
            throw;
        }
    }

    private async Task<string> ExtractTextWithIText7Async(byte[] pdfBytes)
    {
        return await Task.Run(() =>
        {
            try
            {
                _logger.LogInformation("Extracting text using iText7 fallback");
                using var stream = new MemoryStream(pdfBytes);
                using var pdfReader = new iText.Kernel.Pdf.PdfReader(stream);
                using var pdfDocument = new iText.Kernel.Pdf.PdfDocument(pdfReader);
                
                var text = new System.Text.StringBuilder();
                var strategy = new iText.Kernel.Pdf.Canvas.Parser.Listener.SimpleTextExtractionStrategy();
                
                for (int i = 1; i <= pdfDocument.GetNumberOfPages(); i++)
                {
                    var page = pdfDocument.GetPage(i);
                    var pageText = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page, strategy);
                    text.AppendLine($"=== PAGE {i} ===");
                    text.AppendLine(pageText);
                    text.AppendLine();
                }
                
                return text.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error extracting text from PDF with iText7");
                throw;
            }
        });
    }
}

