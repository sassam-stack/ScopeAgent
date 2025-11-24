namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a bounding box with 4-point polygon coordinates
/// </summary>
public class BoundingBox
{
    /// <summary>
    /// 4-point polygon: [x1, y1, x2, y2, x3, y3, x4, y4]
    /// </summary>
    public List<double> Points { get; set; } = new();

    /// <summary>
    /// Alternative rectangle representation: {x, y, width, height}
    /// </summary>
    public double? X { get; set; }
    public double? Y { get; set; }
    public double? Width { get; set; }
    public double? Height { get; set; }

    /// <summary>
    /// Convert to rectangle format if points are available
    /// </summary>
    public void FromPoints(List<double> points)
    {
        if (points == null || points.Count < 8)
            return;

        Points = points;

        // Calculate bounding rectangle from 4-point polygon
        var xCoords = new[] { points[0], points[2], points[4], points[6] };
        var yCoords = new[] { points[1], points[3], points[5], points[7] };

        X = xCoords.Min();
        Y = yCoords.Min();
        Width = xCoords.Max() - X;
        Height = yCoords.Max() - Y;
    }

    /// <summary>
    /// Get center point of bounding box
    /// </summary>
    public Point GetCenter()
    {
        if (X.HasValue && Y.HasValue && Width.HasValue && Height.HasValue)
        {
            return new Point
            {
                X = X.Value + Width.Value / 2,
                Y = Y.Value + Height.Value / 2
            };
        }

        if (Points.Count >= 8)
        {
            var xCoords = new[] { Points[0], Points[2], Points[4], Points[6] };
            var yCoords = new[] { Points[1], Points[3], Points[5], Points[7] };
            return new Point
            {
                X = xCoords.Average(),
                Y = yCoords.Average()
            };
        }

        return new Point { X = 0, Y = 0 };
    }
}

