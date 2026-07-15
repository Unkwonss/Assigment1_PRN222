using System.Collections.Generic;

namespace BusinessLayer.DTOs
{
    public class SubscriptionStatsDto
    {
        public decimal TotalRevenue { get; set; }
        public int SuccessTransactionsCount { get; set; }
        public int FailedTransactionsCount { get; set; }
        public int PendingTransactionsCount { get; set; }
        public Dictionary<string, decimal> RevenueByPackage { get; set; } = new();
        public Dictionary<string, int> SalesCountByPackage { get; set; } = new();
        public Dictionary<string, decimal> RevenueOverTime { get; set; } = new(); // Key: dd/MM/yyyy, Value: decimal
    }
}
