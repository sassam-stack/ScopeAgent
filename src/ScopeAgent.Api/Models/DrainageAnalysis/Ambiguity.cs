namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Represents an ambiguity or issue found during analysis
/// </summary>
public class Ambiguity
{
    public AmbiguityType Type { get; set; }
    public string Description { get; set; } = string.Empty;
    public List<string> AffectedIds { get; set; } = new();
    public AmbiguitySeverity Severity { get; set; }
}

/// <summary>
/// Types of ambiguities
/// </summary>
public enum AmbiguityType
{
    MissingSpec,
    UnclearFlow,
    AmbiguousConnection,
    MissingElevation
}

/// <summary>
/// Severity levels
/// </summary>
public enum AmbiguitySeverity
{
    Low,
    Medium,
    High
}

