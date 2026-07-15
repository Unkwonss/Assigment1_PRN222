using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BusinessLayer.DTOs;

namespace BusinessLayer.Interfaces
{
    public interface ISubscriptionService
    {
        Task<IEnumerable<SubscriptionPackageDto>> GetAllPackagesAsync();
        Task<SubscriptionPackageDto?> GetPackageByIdAsync(int id);
        Task<UserTransactionDto> CreateTransactionAsync(int userId, int packageId);
        Task<UserTransactionDto?> GetTransactionByIdAsync(Guid id);
        Task<bool> UpdateTransactionStatusAsync(Guid transactionId, string status);
        Task<bool> ProcessSuccessfulSubscriptionAsync(Guid transactionId);
        Task SeedDefaultPackagesAsync();
        Task<SubscriptionPackageDto> CreatePackageAsync(SubscriptionPackageDto dto);
        Task<bool> UpdatePackageAsync(SubscriptionPackageDto dto);
        Task<bool> DeletePackageAsync(int id);
        Task<IEnumerable<UserTransactionDto>> GetAllTransactionsAsync();
        Task<IEnumerable<UserTransactionDto>> GetTransactionsByUserIdAsync(int userId);
    }
}
