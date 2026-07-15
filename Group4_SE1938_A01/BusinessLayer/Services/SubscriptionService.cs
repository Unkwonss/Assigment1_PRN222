using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DataAccessLayer.Repository;
using DataAccessLayer.Models;
using Domain.Models;
using BusinessLayer.Interfaces;
using BusinessLayer.DTOs;

namespace BusinessLayer.Services
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly IGenericRepository<SubscriptionPackage> _packageRepo;
        private readonly IGenericRepository<UserTransaction> _transactionRepo;
        private readonly IGenericRepository<User> _userRepo;

        public SubscriptionService(
            IGenericRepository<SubscriptionPackage> packageRepo,
            IGenericRepository<UserTransaction> transactionRepo,
            IGenericRepository<User> userRepo)
        {
            _packageRepo = packageRepo;
            _transactionRepo = transactionRepo;
            _userRepo = userRepo;
        }

        public async Task<IEnumerable<SubscriptionPackageDto>> GetAllPackagesAsync()
        {
            var packages = await _packageRepo.GetAllAsync();
            return packages.Select(p => new SubscriptionPackageDto
            {
                PackageId = p.PackageId,
                PackageName = p.PackageName,
                Price = p.Price,
                ExtraTokenAmount = p.ExtraTokenAmount,
                DurationUnit = p.DurationUnit,
                DurationValue = p.DurationValue
            }).ToList();
        }

        public async Task<SubscriptionPackageDto?> GetPackageByIdAsync(int id)
        {
            var p = await _packageRepo.GetByIdAsync(id);
            if (p == null) return null;
            return new SubscriptionPackageDto
            {
                PackageId = p.PackageId,
                PackageName = p.PackageName,
                Price = p.Price,
                ExtraTokenAmount = p.ExtraTokenAmount,
                DurationUnit = p.DurationUnit,
                DurationValue = p.DurationValue
            };
        }

        public async Task<UserTransactionDto> CreateTransactionAsync(int userId, int packageId)
        {
            var package = await _packageRepo.GetByIdAsync(packageId);
            if (package == null) throw new ArgumentException("Gói đăng ký không tồn tại.");

            var transaction = new UserTransaction
            {
                TransactionId = Guid.NewGuid(),
                UserId = userId,
                PackageId = packageId,
                Amount = package.Price,
                PaymentGateway = "MoMo",
                TransactionStatus = "Pending",
                CreatedAt = DateTime.UtcNow
            };

            await _transactionRepo.AddAsync(transaction);
            await _transactionRepo.SaveAsync();

            return new UserTransactionDto
            {
                TransactionId = transaction.TransactionId,
                UserId = transaction.UserId,
                PackageId = transaction.PackageId,
                Amount = transaction.Amount,
                PaymentGateway = transaction.PaymentGateway,
                TransactionStatus = transaction.TransactionStatus,
                CreatedAt = transaction.CreatedAt,
                PackageName = package.PackageName
            };
        }

        public async Task<UserTransactionDto?> GetTransactionByIdAsync(Guid id)
        {
            var t = await _transactionRepo.GetByIdAsync(id);
            if (t == null) return null;
            return new UserTransactionDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                PackageId = t.PackageId,
                Amount = t.Amount,
                PaymentGateway = t.PaymentGateway,
                TransactionStatus = t.TransactionStatus,
                CreatedAt = t.CreatedAt
            };
        }

        public async Task<bool> UpdateTransactionStatusAsync(Guid transactionId, string status)
        {
            var t = await _transactionRepo.GetByIdAsync(transactionId);
            if (t == null) return false;
            t.TransactionStatus = status;
            _transactionRepo.Update(t);
            await _transactionRepo.SaveAsync();
            return true;
        }

        public async Task<bool> ProcessSuccessfulSubscriptionAsync(Guid transactionId)
        {
            var t = await _transactionRepo.GetByIdAsync(transactionId);
            if (t == null) return false;

            // Nếu giao dịch đã được xử lý thành công trước đó (tránh xử lý trùng lặp do IPN & Redirect)
            if (t.TransactionStatus == "Success") return true;

            var package = await _packageRepo.GetByIdAsync(t.PackageId);
            var user = await _userRepo.GetByIdAsync(t.UserId);

            if (package == null || user == null) return false;

            // 1. Cập nhật trạng thái giao dịch
            t.TransactionStatus = "Success";
            _transactionRepo.Update(t);

            // 2. Cộng dồn token mua thêm của user
            user.PurchasedTokenBalance += package.ExtraTokenAmount;

            // 3. Tính toán ngày hết hạn mới
            DateTime now = DateTime.UtcNow;
            DateTime currentExpiry = user.PurchasedTokenExpiry ?? now;
            if (currentExpiry < now) currentExpiry = now; // Nếu đã hết hạn cũ thì tính từ hôm nay

            if (package.DurationUnit.Equals("Week", StringComparison.OrdinalIgnoreCase))
            {
                user.PurchasedTokenExpiry = currentExpiry.AddDays(7 * package.DurationValue);
            }
            else // Mặc định là tháng
            {
                user.PurchasedTokenExpiry = currentExpiry.AddMonths(package.DurationValue);
            }

            _userRepo.Update(user);
            
            // Lưu thay đổi cả hai bảng
            await _transactionRepo.SaveAsync();
            await _userRepo.SaveAsync();

            return true;
        }

        public async Task SeedDefaultPackagesAsync()
        {
            var count = (await _packageRepo.GetAllAsync()).Count();
            if (count == 0)
            {
                var defaults = new List<SubscriptionPackage>
                {
                    new SubscriptionPackage
                    {
                        PackageName = "Gói Tuần Học Tập",
                        Price = 15000,
                        ExtraTokenAmount = 50000,
                        DurationUnit = "Week",
                        DurationValue = 1
                    },
                    new SubscriptionPackage
                    {
                        PackageName = "Gói Tháng Đột Phá",
                        Price = 50000,
                        ExtraTokenAmount = 200000,
                        DurationUnit = "Month",
                        DurationValue = 1
                    },
                    new SubscriptionPackage
                    {
                        PackageName = "Gói Siêu Cấp VIP",
                        Price = 150000,
                        ExtraTokenAmount = 1000000,
                        DurationUnit = "Month",
                        DurationValue = 3
                    }
                };

                foreach (var p in defaults)
                {
                    await _packageRepo.AddAsync(p);
                }
                await _packageRepo.SaveAsync();
            }
        }
    }
}
