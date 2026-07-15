using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Domain.Models;

namespace DataAccessLayer.Models
{
    public class UserTransaction
    {
        [Key]
        public Guid TransactionId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public int PackageId { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(50)]
        public string PaymentGateway { get; set; } = "MoMo";

        [Required]
        [StringLength(20)]
        public string TransactionStatus { get; set; } = "Pending"; // "Pending", "Success", "Failed"

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }

        [ForeignKey("PackageId")]
        public virtual SubscriptionPackage? Package { get; set; }
    }
}
