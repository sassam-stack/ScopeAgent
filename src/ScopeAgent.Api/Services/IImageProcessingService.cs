using ScopeAgent.Api.Models.DrainageAnalysis;

namespace ScopeAgent.Api.Services;

/// <summary>
/// Service for image processing operations (delegates to Python service)
/// </summary>
public interface IImageProcessingService
{
    /// <summary>
    /// Detect lines in an image
    /// </summary>
    Task<List<LineSegment>> DetectLinesAsync(byte[] imageBytes);

    /// <summary>
    /// Detect circles in an image
    /// </summary>
    Task<List<Circle>> DetectCirclesAsync(byte[] imageBytes);

    /// <summary>
    /// Detect rectangles in an image
    /// </summary>
    Task<List<Rectangle>> DetectRectanglesAsync(byte[] imageBytes);

    /// <summary>
    /// Crop an image to the specified bounding box
    /// </summary>
    Task<byte[]> CropImageAsync(byte[] imageBytes, BoundingBox boundingBox);

    /// <summary>
    /// Detect symbols in an image (double rectangles, circles with grids, ovals, etc.)
    /// </summary>
    Task<List<DetectedSymbol>> DetectSymbolsAsync(byte[] imageBytes);
}

/// <summary>
/// Represents a detected circle
/// </summary>
public class Circle
{
    public Point Center { get; set; } = new();
    public double Radius { get; set; }
}

/// <summary>
/// Represents a detected rectangle
/// </summary>
public class Rectangle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public List<Point> Points { get; set; } = new();
}

