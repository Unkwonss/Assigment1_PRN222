using System;

namespace PresentationLayer.Models
{
    public class UserTokenUsageViewModel
    {
        public int UserId { get; set; }
        public string FullName { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Username { get; set; } = null!;
        public string Role { get; set; } = null!;
        public int WeeklyTokenLimit { get; set; }
        public int WeeklyTokenUsed { get; set; }
        
        public int RemainingTokens => Math.Max(0, WeeklyTokenLimit - WeeklyTokenUsed);
        
        public double UsagePercentage
        {
            get
            {
                if (WeeklyTokenLimit <= 0) return 100.0;
                double pct = (double)WeeklyTokenUsed / WeeklyTokenLimit * 100.0;
                return Math.Min(100.0, Math.Round(pct, 1));
            }
        }
    }
}
