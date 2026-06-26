using System.ComponentModel.DataAnnotations;

namespace CTSHIPDashboard.Models;

public class ReferredHospital
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [StringLength(100)]
    public string? State { get; set; }

    [StringLength(100)]
    public string? Lga { get; set; }

    [StringLength(250)]
    public string? Address { get; set; }

    [StringLength(100)]
    public string? ContactPerson { get; set; }

    [StringLength(50)]
    public string? PhoneNumber { get; set; }

    [StringLength(150)]
    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
}
