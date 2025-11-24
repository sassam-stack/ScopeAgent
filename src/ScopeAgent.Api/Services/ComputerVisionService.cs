using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

public class ComputerVisionService : IComputerVisionService
{
    private readonly ILogger<ComputerVisionService> _logger;
    private readonly HttpClient _httpClient;
    private readonly ComputerVisionConfig _config;

    public ComputerVisionService(IOptions<ComputerVisionConfig> config, ILogger<ComputerVisionService> logger, IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _config = config.Value;
        _httpClient = httpClientFactory.CreateClient();
        // Don't set BaseAddress - we'll use absolute URLs
        _httpClient.BaseAddress = null;
        
        if (!string.IsNullOrEmpty(_config.Endpoint) && !string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogInformation("Computer Vision client initialized");
        }
        else
        {
            _logger.LogWarning("Computer Vision not configured.");
        }
    }

    public async Task<object?> AnalyzeImageAsync(byte[] imageBytes)
    {
        if (string.IsNullOrEmpty(_config.Endpoint) || string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogWarning("Computer Vision not configured. Cannot analyze image.");
            return null;
        }

        try
        {
            _logger.LogInformation("Analyzing image with Azure Computer Vision");
            
            var endpoint = _config.Endpoint.TrimEnd('/');
            // Use the correct API endpoint - Computer Vision API v3.2
            // Include Color for color analysis
            var url = $"{endpoint}/vision/v3.2/analyze?visualFeatures=Categories,Description,Objects,Tags,Color&details=Landmarks&language=en";
            
            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
            {
                Content = content
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", _config.ApiKey);

            _logger.LogInformation("Sending request to: {Url}", url);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Computer Vision API returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Computer Vision API returned {response.StatusCode}: {errorContent}");
            }
            
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<JsonElement>(responseContent);

            _logger.LogInformation("Image analysis completed successfully");

            // Convert the result to a more readable format
            var analysisResult = new Dictionary<string, object?>();

            if (result.TryGetProperty("description", out var description))
            {
                var captions = new List<object>();
                if (description.TryGetProperty("captions", out var captionsArray))
                {
                    foreach (var caption in captionsArray.EnumerateArray())
                    {
                        captions.Add(new
                        {
                            text = caption.GetProperty("text").GetString(),
                            confidence = caption.GetProperty("confidence").GetDouble()
                        });
                    }
                }
                analysisResult["caption"] = captions.Count > 0 ? captions[0] : null;
                analysisResult["captions"] = captions;
            }

            if (result.TryGetProperty("tags", out var tags))
            {
                var tagsList = new List<object>();
                foreach (var tag in tags.EnumerateArray())
                {
                    tagsList.Add(new
                    {
                        name = tag.GetProperty("name").GetString(),
                        confidence = tag.GetProperty("confidence").GetDouble()
                    });
                }
                analysisResult["tags"] = tagsList;
            }

            if (result.TryGetProperty("objects", out var objects))
            {
                var objectsList = new List<object>();
                foreach (var obj in objects.EnumerateArray())
                {
                    objectsList.Add(new
                    {
                        name = obj.GetProperty("object").GetString(),
                        confidence = obj.GetProperty("confidence").GetDouble(),
                        boundingBox = obj.TryGetProperty("rectangle", out var rect) ? new
                        {
                            x = rect.GetProperty("x").GetInt32(),
                            y = rect.GetProperty("y").GetInt32(),
                            width = rect.GetProperty("w").GetInt32(),
                            height = rect.GetProperty("h").GetInt32()
                        } : null
                    });
                }
                analysisResult["objects"] = objectsList;
            }

            // Parse Categories
            if (result.TryGetProperty("categories", out var categories))
            {
                var categoriesList = new List<object>();
                foreach (var category in categories.EnumerateArray())
                {
                    var categoryName = category.TryGetProperty("name", out var name) ? name.GetString() : null;
                    var categoryScore = category.TryGetProperty("score", out var score) ? score.GetDouble() : 0.0;
                    
                    var detail = new Dictionary<string, object?>();
                    if (category.TryGetProperty("detail", out var detailObj))
                    {
                        if (detailObj.TryGetProperty("celebrities", out var celebrities))
                        {
                            var celebList = new List<object>();
                            foreach (var celeb in celebrities.EnumerateArray())
                            {
                                celebList.Add(new
                                {
                                    name = celeb.TryGetProperty("name", out var n) ? n.GetString() : null,
                                    confidence = celeb.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0,
                                    faceRectangle = celeb.TryGetProperty("faceRectangle", out var fr) ? new
                                    {
                                        left = fr.TryGetProperty("left", out var l) ? l.GetInt32() : 0,
                                        top = fr.TryGetProperty("top", out var t) ? t.GetInt32() : 0,
                                        width = fr.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
                                        height = fr.TryGetProperty("height", out var h) ? h.GetInt32() : 0
                                    } : null
                                });
                            }
                            detail["celebrities"] = celebList;
                        }
                        if (detailObj.TryGetProperty("landmarks", out var landmarks))
                        {
                            var landmarkList = new List<object>();
                            foreach (var landmark in landmarks.EnumerateArray())
                            {
                                landmarkList.Add(new
                                {
                                    name = landmark.TryGetProperty("name", out var n) ? n.GetString() : null,
                                    confidence = landmark.TryGetProperty("confidence", out var c) ? c.GetDouble() : 0.0
                                });
                            }
                            detail["landmarks"] = landmarkList;
                        }
                    }
                    
                    categoriesList.Add(new
                    {
                        name = categoryName,
                        score = categoryScore,
                        detail = detail.Count > 0 ? detail : null
                    });
                }
                analysisResult["categories"] = categoriesList;
            }

            // Parse Color information
            if (result.TryGetProperty("color", out var color))
            {
                var colorInfo = new Dictionary<string, object?>();
                
                if (color.TryGetProperty("dominantColorForeground", out var dominantFg))
                    colorInfo["dominantColorForeground"] = dominantFg.GetString();
                
                if (color.TryGetProperty("dominantColorBackground", out var dominantBg))
                    colorInfo["dominantColorBackground"] = dominantBg.GetString();
                
                if (color.TryGetProperty("dominantColors", out var dominantColors))
                {
                    var colorsList = new List<string>();
                    foreach (var col in dominantColors.EnumerateArray())
                    {
                        colorsList.Add(col.GetString() ?? "");
                    }
                    colorInfo["dominantColors"] = colorsList;
                }
                
                if (color.TryGetProperty("accentColor", out var accent))
                    colorInfo["accentColor"] = accent.GetString();
                
                if (color.TryGetProperty("isBwImg", out var isBw))
                    colorInfo["isBlackAndWhite"] = isBw.GetBoolean();
                
                analysisResult["color"] = colorInfo;
            }

            if (result.TryGetProperty("modelVersion", out var modelVersion))
            {
                analysisResult["modelVersion"] = modelVersion.GetString();
            }

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing image with Computer Vision");
            throw;
        }
    }

    public async Task<object?> ReadTextAsync(byte[] imageBytes)
    {
        if (string.IsNullOrEmpty(_config.Endpoint) || string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogWarning("Computer Vision not configured. Cannot read text from image.");
            return null;
        }

        try
        {
            _logger.LogInformation("Reading text from image with Azure Computer Vision OCR");
            
            var endpoint = _config.Endpoint.TrimEnd('/');
            // Use the Read API endpoint for OCR
            var url = $"{endpoint}/vision/v3.2/read/analyze";
            
            using var content = new ByteArrayContent(imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
            {
                Content = content
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", _config.ApiKey);

            _logger.LogInformation("Sending OCR request to: {Url}", url);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Computer Vision Read API returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Computer Vision Read API returned {response.StatusCode}: {errorContent}");
            }
            
            // Read API is async - we get an operation location
            if (response.Headers.Contains("Operation-Location"))
            {
                var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                if (string.IsNullOrEmpty(operationLocation))
                {
                    _logger.LogError("No Operation-Location header in response");
                    return null;
                }

                _logger.LogInformation("OCR operation started, polling: {Location}", operationLocation);
                
                // Poll for results (max 60 seconds)
                var maxAttempts = 60;
                var attempt = 0;
                
                while (attempt < maxAttempts)
                {
                    await Task.Delay(1000); // Wait 1 second between polls
                    
                    using var getRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(operationLocation));
                    getRequest.Headers.Add("Ocp-Apim-Subscription-Key", _config.ApiKey);
                    
                    var getResponse = await _httpClient.SendAsync(getRequest);
                    getResponse.EnsureSuccessStatusCode();
                    
                    var resultContent = await getResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(resultContent);
                    
                    var status = result.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                    
                    if (status == "succeeded")
                    {
                        _logger.LogInformation("OCR operation completed");
                        
                        var readResult = new Dictionary<string, object?>();
                        
                        if (result.TryGetProperty("analyzeResult", out var analyzeResult))
                        {
                            var pages = new List<object>();
                            if (analyzeResult.TryGetProperty("readResults", out var readResults))
                            {
                                foreach (var page in readResults.EnumerateArray())
                                {
                                    var pageNumber = page.TryGetProperty("page", out var p) ? p.GetInt32() : 0;
                                    var width = page.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                                    var height = page.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                                    
                                    var lines = new List<object>();
                                    if (page.TryGetProperty("lines", out var linesArray))
                                    {
                                        foreach (var line in linesArray.EnumerateArray())
                                        {
                                            var words = new List<object>();
                                            if (line.TryGetProperty("words", out var wordsArray))
                                            {
                                                foreach (var word in wordsArray.EnumerateArray())
                                                {
                                                    words.Add(new
                                                    {
                                                        text = word.TryGetProperty("text", out var wordText) ? wordText.GetString() : null,
                                                        confidence = word.TryGetProperty("confidence", out var wordConf) ? wordConf.GetDouble() : 0.0,
                                                        boundingBox = word.TryGetProperty("boundingBox", out var wordBbox) ? 
                                                            wordBbox.EnumerateArray().Select(p => p.GetDouble()).ToArray() : null
                                                    });
                                                }
                                            }
                                            
                                            lines.Add(new
                                            {
                                                text = line.TryGetProperty("text", out var lineText) ? lineText.GetString() : null,
                                                boundingBox = line.TryGetProperty("boundingBox", out var lineBbox) ? 
                                                    lineBbox.EnumerateArray().Select(p => p.GetDouble()).ToArray() : null,
                                                words = words
                                            });
                                        }
                                    }
                                    
                                    pages.Add(new
                                    {
                                        pageNumber = pageNumber,
                                        width = width,
                                        height = height,
                                        lines = lines
                                    });
                                }
                            }
                            
                            readResult["pages"] = pages;
                            
                            // Extract all text content
                            var allText = new System.Text.StringBuilder();
                            foreach (var pageObj in pages)
                            {
                                if (pageObj is Dictionary<string, object?> pageDict)
                                {
                                    if (pageDict.TryGetValue("lines", out var pageLines) && pageLines is List<object> linesList)
                                    {
                                        foreach (var lineObj in linesList)
                                        {
                                            if (lineObj is Dictionary<string, object?> lineDict)
                                            {
                                                if (lineDict.TryGetValue("text", out var lineText) && lineText != null)
                                                {
                                                    allText.AppendLine(lineText.ToString());
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            readResult["content"] = allText.ToString();
                        }
                        
                        return readResult;
                    }
                    else if (status == "failed")
                    {
                        if (result.TryGetProperty("error", out var errorProp))
                        {
                            _logger.LogError("OCR operation failed: {Error}", errorProp.ToString());
                        }
                        else
                        {
                            _logger.LogError("OCR operation failed with unknown error");
                        }
                        return null;
                    }
                    
                    attempt++;
                }
                
                _logger.LogWarning("OCR operation timed out after {Attempts} attempts", maxAttempts);
                return null;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading text from image with Computer Vision");
            throw;
        }
    }

    public async Task<OCRResult?> ReadTextStructuredAsync(byte[] imageBytes)
    {
        var ocrResponse = await ReadTextAsync(imageBytes);
        if (ocrResponse == null)
            return null;

        return OCRHelper.ConvertToStructuredOCR(ocrResponse);
    }

    /// <summary>
    /// Read text from PDF using Azure Computer Vision Read API
    /// </summary>
    public async Task<OCRResult?> ReadTextFromPdfAsync(byte[] pdfBytes)
    {
        if (string.IsNullOrEmpty(_config.Endpoint) || string.IsNullOrEmpty(_config.ApiKey))
        {
            _logger.LogWarning("Computer Vision not configured. Cannot read text from PDF.");
            return null;
        }

        try
        {
            _logger.LogInformation("Reading text from PDF with Azure Computer Vision OCR");
            
            var endpoint = _config.Endpoint.TrimEnd('/');
            // Use the Read API endpoint for OCR - it supports PDFs
            var url = $"{endpoint}/vision/v3.2/read/analyze";
            
            using var content = new ByteArrayContent(pdfBytes);
            // Set content type to application/pdf for PDF files
            content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
            
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(url))
            {
                Content = content
            };
            request.Headers.Add("Ocp-Apim-Subscription-Key", _config.ApiKey);

            _logger.LogInformation("Sending OCR request for PDF to: {Url}", url);
            var response = await _httpClient.SendAsync(request);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Computer Vision Read API returned error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                throw new HttpRequestException($"Computer Vision Read API returned {response.StatusCode}: {errorContent}");
            }
            
            // Read API is async - we get an operation location
            if (response.Headers.Contains("Operation-Location"))
            {
                var operationLocation = response.Headers.GetValues("Operation-Location").FirstOrDefault();
                if (string.IsNullOrEmpty(operationLocation))
                {
                    _logger.LogError("No Operation-Location header in response");
                    return null;
                }

                _logger.LogInformation("OCR operation started for PDF, polling: {Location}", operationLocation);
                
                // Poll for results (max 60 seconds)
                var maxAttempts = 60;
                var attempt = 0;
                
                while (attempt < maxAttempts)
                {
                    await Task.Delay(1000); // Wait 1 second between polls
                    
                    using var getRequest = new HttpRequestMessage(HttpMethod.Get, new Uri(operationLocation));
                    getRequest.Headers.Add("Ocp-Apim-Subscription-Key", _config.ApiKey);
                    
                    var getResponse = await _httpClient.SendAsync(getRequest);
                    getResponse.EnsureSuccessStatusCode();
                    
                    var resultContent = await getResponse.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<JsonElement>(resultContent);
                    
                    var status = result.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : null;
                    
                    if (status == "succeeded")
                    {
                        _logger.LogInformation("OCR operation completed for PDF");
                        
                        var readResult = new Dictionary<string, object?>();
                        
                        if (result.TryGetProperty("analyzeResult", out var analyzeResult))
                        {
                            var pages = new List<object>();
                            if (analyzeResult.TryGetProperty("readResults", out var readResults))
                            {
                                foreach (var page in readResults.EnumerateArray())
                                {
                                    var pageNumber = page.TryGetProperty("page", out var p) ? p.GetInt32() : 0;
                                    var width = page.TryGetProperty("width", out var w) ? w.GetInt32() : 0;
                                    var height = page.TryGetProperty("height", out var h) ? h.GetInt32() : 0;
                                    
                                    var lines = new List<object>();
                                    if (page.TryGetProperty("lines", out var linesArray))
                                    {
                                        foreach (var line in linesArray.EnumerateArray())
                                        {
                                            var words = new List<object>();
                                            if (line.TryGetProperty("words", out var wordsArray))
                                            {
                                                foreach (var word in wordsArray.EnumerateArray())
                                                {
                                                    words.Add(new
                                                    {
                                                        text = word.TryGetProperty("text", out var wordText) ? wordText.GetString() : null,
                                                        confidence = word.TryGetProperty("confidence", out var wordConf) ? wordConf.GetDouble() : 0.0,
                                                        boundingBox = word.TryGetProperty("boundingBox", out var wordBbox) ? 
                                                            wordBbox.EnumerateArray().Select(p => p.GetDouble()).ToArray() : null
                                                    });
                                                }
                                            }
                                            
                                            lines.Add(new
                                            {
                                                text = line.TryGetProperty("text", out var lineText) ? lineText.GetString() : null,
                                                boundingBox = line.TryGetProperty("boundingBox", out var lineBbox) ? 
                                                    lineBbox.EnumerateArray().Select(p => p.GetDouble()).ToArray() : null,
                                                words = words
                                            });
                                        }
                                    }
                                    
                                    pages.Add(new
                                    {
                                        pageNumber = pageNumber,
                                        width = width,
                                        height = height,
                                        lines = lines
                                    });
                                }
                            }
                            
                            readResult["pages"] = pages;
                            
                            // Extract all text content
                            var allText = new System.Text.StringBuilder();
                            foreach (var pageObj in pages)
                            {
                                if (pageObj is Dictionary<string, object?> pageDict)
                                {
                                    if (pageDict.TryGetValue("lines", out var pageLines) && pageLines is List<object> linesList)
                                    {
                                        foreach (var lineObj in linesList)
                                        {
                                            if (lineObj is Dictionary<string, object?> lineDict)
                                            {
                                                if (lineDict.TryGetValue("text", out var lineText) && lineText != null)
                                                {
                                                    allText.AppendLine(lineText.ToString());
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            readResult["content"] = allText.ToString();
                        }
                        
                        return OCRHelper.ConvertToStructuredOCR(readResult);
                    }
                    else if (status == "failed")
                    {
                        if (result.TryGetProperty("error", out var errorProp))
                        {
                            _logger.LogError("OCR operation failed for PDF: {Error}", errorProp.ToString());
                        }
                        else
                        {
                            _logger.LogError("OCR operation failed for PDF with unknown error");
                        }
                        return null;
                    }
                    
                    attempt++;
                }
                
                _logger.LogWarning("OCR operation timed out for PDF after {Attempts} attempts", maxAttempts);
                return null;
            }
            
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading text from PDF with Computer Vision");
            throw;
        }
    }
}

