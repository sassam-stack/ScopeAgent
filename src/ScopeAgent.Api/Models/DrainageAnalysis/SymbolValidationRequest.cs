using System.ComponentModel.DataAnnotations;

namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Request to validate detected symbols
/// </summary>
public class SymbolValidationRequest
{
    [Required]
    public List<SymbolValidation> Validations { get; set; } = new();
}

public class SymbolValidation
{
    [Required]
    public string SymbolId { get; set; } = string.Empty;
    
    [Required]
    public bool IsModule { get; set; }
}

