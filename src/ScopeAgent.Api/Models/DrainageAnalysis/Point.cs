namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents a 2D point
/// </summary>
public class Point
{
    public double X { get; set; }
    public double Y { get; set; }

    /// <summary>
    /// Calculate distance to another point
    /// </summary>
    public double DistanceTo(Point other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}

