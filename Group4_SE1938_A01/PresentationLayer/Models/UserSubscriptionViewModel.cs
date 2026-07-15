using System.Collections.Generic;
using BusinessLayer.DTOs;

namespace PresentationLayer.Models
{
    public class UserSubscriptionViewModel
    {
        public UserDto User { get; set; } = null!;
        public int WeeklyUsedTokens { get; set; }
        public IEnumerable<SubscriptionPackageDto> Packages { get; set; } = new List<SubscriptionPackageDto>();
        public IEnumerable<UserTransactionDto> PersonalTransactions { get; set; } = new List<UserTransactionDto>();
    }
}
