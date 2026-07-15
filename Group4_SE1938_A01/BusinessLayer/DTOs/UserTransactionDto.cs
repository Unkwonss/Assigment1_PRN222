using System;

namespace BusinessLayer.DTOs
{
    public class UserTransactionDto
    {
        public Guid TransactionId { get; set; }
        public int UserId { get; set; }
        public int PackageId { get; set; }
        public decimal Amount { get; set; }
        public string PaymentGateway { get; set; } = "MoMo";
        public string TransactionStatus { get; set; } = "Pending";
        public DateTime CreatedAt { get; set; }
        public string? FullName { get; set; }
        public string? PackageName { get; set; }
    }
}
