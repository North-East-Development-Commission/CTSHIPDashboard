using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.ViewModels;

public class EncounterLaboratoryInputViewModel
{
    [Required]
    public string LaboratoryTestName { get; set; } = string.Empty;

    [StringLength(200)]
    public string? Result { get; set; }

    [StringLength(50)]
    public string? ResultUnit { get; set; }

    [StringLength(200)]
    public string? ReferenceRange { get; set; }

    [StringLength(50)]
    public string? ResultStatus { get; set; } = "Requested";

    [StringLength(1000)]
    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
}
