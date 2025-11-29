using System.Text.Json;

namespace ScopeAgent.Api.Services;

public interface IOuterportService
{
    Task<OuterportResult?> ProcessDrainagePlanAsync(byte[] imageBytes);
}

public class OuterportResult
{
    public List<OuterportJunction> Junctions { get; set; } = new();
    public List<OuterportMaterial> Materials { get; set; } = new();
}

public class OuterportJunction
{
    public string Id { get; set; } = string.Empty;
    public int[]? Bbox { get; set; } // [x1, y1, x2, y2] or null
    public int[] LabelBbox { get; set; } = Array.Empty<int>(); // [x1, y1, x2, y2]
    public List<string>? ExpectedDirections { get; set; } // ["N", "E", "NIE"] or null
}

public class OuterportMaterial
{
    public string Text { get; set; } = string.Empty;
    public int[] Bbox { get; set; } = Array.Empty<int>(); // [x1, y1, x2, y2]
}

