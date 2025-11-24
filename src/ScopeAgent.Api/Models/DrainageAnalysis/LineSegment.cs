namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a line segment
/// </summary>
public class LineSegment
{
    public Point StartPoint { get; set; } = new();
    public Point EndPoint { get; set; } = new();

    /// <summary>
    /// Calculate length of line segment
    /// </summary>
    public double Length()
    {
        return StartPoint.DistanceTo(EndPoint);
    }

    /// <summary>
    /// Calculate distance from a point to this line segment
    /// </summary>
    public double DistanceToPoint(Point point)
    {
        var A = point.X - StartPoint.X;
        var B = point.Y - StartPoint.Y;
        var C = EndPoint.X - StartPoint.X;
        var D = EndPoint.Y - StartPoint.Y;

        var dot = A * C + B * D;
        var lenSq = C * C + D * D;
        var param = lenSq != 0 ? dot / lenSq : -1;

        double xx, yy;

        if (param < 0)
        {
            xx = StartPoint.X;
            yy = StartPoint.Y;
        }
        else if (param > 1)
        {
            xx = EndPoint.X;
            yy = EndPoint.Y;
        }
        else
        {
            xx = StartPoint.X + param * C;
            yy = StartPoint.Y + param * D;
        }

        var dx = point.X - xx;
        var dy = point.Y - yy;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

