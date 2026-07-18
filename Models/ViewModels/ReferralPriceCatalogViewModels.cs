using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CTSHIPDashboard.ViewModels;

public class ReferralPriceCatalogIndexViewModel
{
    public string? State { get; set; }

    public string? Category { get; set; }

    public ReferralPriceCatalogBulkUploadViewModel BulkUpload { get; set; } = new();

    public List<ReferralPriceCatalogItemRowViewModel> Items { get; set; } = new();

    public List<SelectListItem> StateOptions { get; set; } = new();

    public List<SelectListItem> FilterStateOptions { get; set; } = new();

    public List<SelectListItem> CategoryOptions { get; set; } = new();

    public List<SelectListItem> FilterCategoryOptions { get; set; } = new();
}

public class ReferralPriceCatalogBulkUploadViewModel
{
    [Required]
    [Display(Name = "State")]
    public string State { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Category")]
    public string Category { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Catalog Items")]
    public string ItemsText { get; set; } = string.Empty;

    [Display(Name = "Replace active items in this state and category")]
    public bool ReplaceExisting { get; set; }
}

public class ReferralPriceCatalogItemRowViewModel
{
    public int Id { get; set; }

    public string State { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
