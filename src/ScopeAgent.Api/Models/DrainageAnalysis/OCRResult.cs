namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Structured OCR results from Azure Computer Vision
/// </summary>
public class OCRResult
{
    public List<OCRPage> Pages { get; set; } = new();
}

public class OCRPage
{
    public int PageNumber { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public List<OCRLine> Lines { get; set; } = new();
}

public class OCRLine
{
    public string Text { get; set; } = string.Empty;
    public BoundingBox BoundingBox { get; set; } = new();
    public List<OCRWord> Words { get; set; } = new();
    public double Confidence { get; set; }
}

public class OCRWord
{
    public string Text { get; set; } = string.Empty;
    public BoundingBox BoundingBox { get; set; } = new();
    public double Confidence { get; set; }
}

