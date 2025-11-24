using System.Text.RegularExpressions;
using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Helper class for parsing OCR results and extracting specific patterns
/// </summary>
public static class OCRHelper
{
    /// <summary>
    /// Extract module labels (e.g., "S-1", "S-2", "M1", "M2") from OCR results
    /// </summary>
    public static List<OCRWord> ExtractModuleLabels(OCRResult ocrResult)
    {
        var moduleLabels = new List<OCRWord>();
        var modulePattern = new Regex(@"\b([SM]-\d+|[SM]\d+)\b", RegexOptions.IgnoreCase);

        foreach (var page in ocrResult.Pages)
        {
            foreach (var line in page.Lines)
            {
                foreach (var word in line.Words)
                {
                    if (modulePattern.IsMatch(word.Text))
                    {
                        moduleLabels.Add(word);
                    }
                }
            }
        }

        return moduleLabels;
    }

    /// <summary>
    /// Extract "ST" labels from OCR results
    /// </summary>
    public static List<OCRWord> ExtractSTLabels(OCRResult ocrResult)
    {
        var stLabels = new List<OCRWord>();
        var stPattern = new Regex(@"\bST\b", RegexOptions.IgnoreCase);

        foreach (var page in ocrResult.Pages)
        {
            foreach (var line in page.Lines)
            {
                foreach (var word in line.Words)
                {
                    if (stPattern.IsMatch(word.Text))
                    {
                        stLabels.Add(word);
                    }
                }
            }
        }

        return stLabels;
    }

    /// <summary>
    /// Extract pipe specifications (e.g., "18\" RCP", "30\" HDPE") from OCR results
    /// </summary>
    public static List<(OCRWord word, double? diameter, string? material)> ExtractPipeSpecifications(OCRResult ocrResult)
    {
        var specifications = new List<(OCRWord, double?, string?)>();
        // Pattern: number followed by " or inches, then material abbreviation
        var specPattern = new Regex(@"(\d+)\s*[""']?\s*(RCP|HDPE|PVC|CP|DI|CI|CONC)", RegexOptions.IgnoreCase);

        foreach (var page in ocrResult.Pages)
        {
            foreach (var line in page.Lines)
            {
                // Check full line text for specifications
                var lineText = line.Text;
                var matches = specPattern.Matches(lineText);
                
                foreach (Match match in matches)
                {
                    if (double.TryParse(match.Groups[1].Value, out var diameter))
                    {
                        var material = match.Groups[2].Value.ToUpper();
                        
                        // Find the word that contains this match
                        var word = line.Words.FirstOrDefault(w => 
                            w.Text.Contains(match.Groups[1].Value) || 
                            w.Text.Contains(match.Groups[2].Value));
                        
                        if (word != null)
                        {
                            specifications.Add((word, diameter, material));
                        }
                    }
                }
            }
        }

        return specifications;
    }

    /// <summary>
    /// Extract elevation labels (G.E., I.E., EOP) with their values
    /// </summary>
    public static List<(OCRWord label, OCRWord? value, string type, double? elevationValue)> ExtractElevations(OCRResult ocrResult)
    {
        var elevations = new List<(OCRWord, OCRWord?, string, double?)>();
        var gePattern = new Regex(@"\bG\.?E\.?\b", RegexOptions.IgnoreCase);
        var iePattern = new Regex(@"\bI\.?E\.?\b", RegexOptions.IgnoreCase);
        var eopPattern = new Regex(@"\bE\.?O\.?P\.?\b", RegexOptions.IgnoreCase);
        var numberPattern = new Regex(@"(\d+\.?\d*)");

        foreach (var page in ocrResult.Pages)
        {
            foreach (var line in page.Lines)
            {
                var lineText = line.Text;
                OCRWord? labelWord = null;
                string? elevationType = null;

                // Check for G.E.
                if (gePattern.IsMatch(lineText))
                {
                    labelWord = line.Words.FirstOrDefault(w => gePattern.IsMatch(w.Text));
                    elevationType = "G.E.";
                }
                // Check for I.E.
                else if (iePattern.IsMatch(lineText))
                {
                    labelWord = line.Words.FirstOrDefault(w => iePattern.IsMatch(w.Text));
                    elevationType = "I.E.";
                }
                // Check for EOP
                else if (eopPattern.IsMatch(lineText))
                {
                    labelWord = line.Words.FirstOrDefault(w => eopPattern.IsMatch(w.Text));
                    elevationType = "EOP";
                }

                if (labelWord != null && !string.IsNullOrEmpty(elevationType))
                {
                    // Find numeric value in the same line
                    var numberMatch = numberPattern.Match(lineText);
                    OCRWord? valueWord = null;
                    double? elevationValue = null;

                    if (numberMatch.Success && double.TryParse(numberMatch.Value, out var value))
                    {
                        elevationValue = value;
                        valueWord = line.Words.FirstOrDefault(w => w.Text.Contains(numberMatch.Value));
                    }

                    elevations.Add((labelWord, valueWord, elevationType, elevationValue));
                }
            }
        }

        return elevations;
    }

    /// <summary>
    /// Extract scale information (e.g., "1\"=30'", "SCALE: 1:100")
    /// </summary>
    public static (string? text, double? ratio, string? unit) ExtractScale(OCRResult ocrResult)
    {
        var scalePatterns = new[]
        {
            new Regex(@"(\d+)\s*[""']\s*=\s*(\d+)\s*['']", RegexOptions.IgnoreCase), // 1"=30'
            new Regex(@"SCALE\s*:?\s*(\d+)\s*:\s*(\d+)", RegexOptions.IgnoreCase), // SCALE: 1:100
            new Regex(@"(\d+)\s*INCH\s*=\s*(\d+)\s*FEET?", RegexOptions.IgnoreCase) // 1 INCH = 30 FEET
        };

        foreach (var page in ocrResult.Pages)
        {
            foreach (var line in page.Lines)
            {
                var lineText = line.Text;
                
                foreach (var pattern in scalePatterns)
                {
                    var match = pattern.Match(lineText);
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, out var numerator) &&
                            double.TryParse(match.Groups[2].Value, out var denominator))
                        {
                            var ratio = denominator / numerator;
                            var unit = lineText.Contains("FEET") || lineText.Contains("'") ? "feet" : "unknown";
                            return (lineText, ratio, unit);
                        }
                    }
                }
            }
        }

        return (null, null, null);
    }

    /// <summary>
    /// Convert OCR result from ComputerVisionService format to structured OCRResult
    /// </summary>
    public static OCRResult ConvertToStructuredOCR(object? ocrResponse)
    {
        var result = new OCRResult();
        
        if (ocrResponse == null)
            return result;

        try
        {
            // The OCR response from ComputerVisionService.ReadTextAsync returns a Dictionary
            // with "pages" array containing page data
            if (ocrResponse is Dictionary<string, object?> dict)
            {
                if (dict.TryGetValue("pages", out var pagesObj) && pagesObj is List<object> pagesList)
                {
                    foreach (var pageObj in pagesList)
                    {
                        if (pageObj is Dictionary<string, object?> pageDict)
                        {
                            var page = new OCRPage();
                            
                            if (pageDict.TryGetValue("pageNumber", out var pageNum))
                                page.PageNumber = Convert.ToInt32(pageNum);
                            
                            if (pageDict.TryGetValue("width", out var width))
                                page.Width = Convert.ToInt32(width);
                            
                            if (pageDict.TryGetValue("height", out var height))
                                page.Height = Convert.ToInt32(height);
                            
                            if (pageDict.TryGetValue("lines", out var linesObj) && linesObj is List<object> linesList)
                            {
                                foreach (var lineObj in linesList)
                                {
                                    if (lineObj is Dictionary<string, object?> lineDict)
                                    {
                                        var line = new OCRLine();
                                        
                                        if (lineDict.TryGetValue("text", out var text))
                                            line.Text = text?.ToString() ?? string.Empty;
                                        
                                        if (lineDict.TryGetValue("boundingBox", out var bboxObj) && bboxObj is double[] bboxArray)
                                        {
                                            line.BoundingBox = new BoundingBox();
                                            line.BoundingBox.Points = bboxArray.ToList();
                                            line.BoundingBox.FromPoints(bboxArray.ToList());
                                        }
                                        
                                        if (lineDict.TryGetValue("confidence", out var conf))
                                            line.Confidence = Convert.ToDouble(conf);
                                        
                                        if (lineDict.TryGetValue("words", out var wordsObj) && wordsObj is List<object> wordsList)
                                        {
                                            foreach (var wordObj in wordsList)
                                            {
                                                if (wordObj is Dictionary<string, object?> wordDict)
                                                {
                                                    var word = new OCRWord();
                                                    
                                                    if (wordDict.TryGetValue("text", out var wordText))
                                                        word.Text = wordText?.ToString() ?? string.Empty;
                                                    
                                                    if (wordDict.TryGetValue("boundingBox", out var wordBboxObj) && wordBboxObj is double[] wordBboxArray)
                                                    {
                                                        word.BoundingBox = new BoundingBox();
                                                        word.BoundingBox.Points = wordBboxArray.ToList();
                                                        word.BoundingBox.FromPoints(wordBboxArray.ToList());
                                                    }
                                                    
                                                    if (wordDict.TryGetValue("confidence", out var wordConf))
                                                        word.Confidence = Convert.ToDouble(wordConf);
                                                    
                                                    line.Words.Add(word);
                                                }
                                            }
                                        }
                                        
                                        page.Lines.Add(line);
                                    }
                                }
                            }
                            
                            result.Pages.Add(page);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Log error but return empty result
            System.Diagnostics.Debug.WriteLine($"Error converting OCR result: {ex.Message}");
        }
        
        return result;
    }
}

