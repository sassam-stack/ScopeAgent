namespace ScopeAgent.Api.Models;

public class AnalysisResponse
{
    public List<DrainagePage> DrainagePages { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class DrainagePage
{
    public int PageNumber { get; set; }
    public string? NorthDirection { get; set; }
    public List<Module> Modules { get; set; } = new();
    public List<TableLocation> TableLocations { get; set; } = new();
}

public class Module
{
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public int? PageNumber { get; set; }
}

public class TableLocation
{
    public int PageNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? Position { get; set; }
}

