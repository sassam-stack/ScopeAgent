using System.ComponentModel.DataAnnotations;

namespace ScopeAgent.Api.Models.DrainageAnalysis;

/// <summary>
/// Request model for uploading a drainage plan PDF
/// </summary>
public class UploadRequest
{
    /// <summary>
    /// PDF file to analyze
    /// </summary>
    [Required]
    public IFormFile? PdfFile { get; set; }
    
    /// <summary>
    /// Page number containing the drainage plan
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Plan page number must be at least 1")]
    public int PlanPageNumber { get; set; }
    
    /// <summary>
    /// Optional: Page number containing content table
    /// </summary>
    public int? ContentTablePageNumber { get; set; }
    
    /// <summary>
    /// Optional: Pre-defined list of module labels (e.g., ["S-1", "S-2", "S-3"])
    /// </summary>
    public List<string>? ModuleList { get; set; }
    
    /// <summary>
    /// Optional: If true, use Outerport service for module extraction. If false, use Azure VR & symbol validation (default).
    /// </summary>
    public bool UseOuterport { get; set; } = false;
}

