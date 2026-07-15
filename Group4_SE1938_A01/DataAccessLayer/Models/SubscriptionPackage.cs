using System.ComponentModel.DataAnnotations;

namespace DataAccessLayer.Models
{
    public class SubscriptionPackage
    {
        [Key]
        public int PackageId { get; set; }

        [Required]
        [StringLength(100)]
        public string PackageName { get; set; } = string.Empty;

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int ExtraTokenAmount { get; set; }

        [Required]
        [StringLength(20)]
        public string DurationUnit { get; set; } = "Month"; // "Week", "Month"

        [Required]
        public int DurationValue { get; set; } = 1;
    }
}
