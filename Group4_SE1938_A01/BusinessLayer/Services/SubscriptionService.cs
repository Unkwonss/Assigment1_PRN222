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

        public async Task<SubscriptionPackageDto> CreatePackageAsync(SubscriptionPackageDto dto)
        {
            var p = new SubscriptionPackage
            {
                PackageName = dto.PackageName,
                Price = dto.Price,
                ExtraTokenAmount = dto.ExtraTokenAmount,
                DurationUnit = dto.DurationUnit,
                DurationValue = dto.DurationValue
            };
            await _packageRepo.AddAsync(p);
            await _packageRepo.SaveAsync();
            dto.PackageId = p.PackageId;
            return dto;
        }

        public async Task<bool> UpdatePackageAsync(SubscriptionPackageDto dto)
        {
            var p = await _packageRepo.GetByIdAsync(dto.PackageId);
            if (p == null) return false;

            p.PackageName = dto.PackageName;
            p.Price = dto.Price;
            p.ExtraTokenAmount = dto.ExtraTokenAmount;
            p.DurationUnit = dto.DurationUnit;
            p.DurationValue = dto.DurationValue;

            _packageRepo.Update(p);
            await _packageRepo.SaveAsync();
            return true;
        }

        public async Task<bool> DeletePackageAsync(int id)
        {
            var p = await _packageRepo.GetByIdAsync(id);
            if (p == null) return false;

            _packageRepo.Delete(p);
            await _packageRepo.SaveAsync();
            return true;
        }

        public async Task<IEnumerable<UserTransactionDto>> GetAllTransactionsAsync()
        {
            await CleanExpiredPendingTransactionsAsync();
            var transactions = await _transactionRepo.GetAllNoTrackingAsync(
                includeProperties: "User,Package"
            );
            return transactions.Select(t => new UserTransactionDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                PackageId = t.PackageId,
                Amount = t.Amount,
                PaymentGateway = t.PaymentGateway,
                TransactionStatus = t.TransactionStatus,
                CreatedAt = t.CreatedAt,
                FullName = t.User?.FullName ?? "N/A",
                PackageName = t.Package?.PackageName ?? "Gói đã xóa"
            }).OrderByDescending(t => t.CreatedAt).ToList();
        }

        public async Task<IEnumerable<UserTransactionDto>> GetTransactionsByUserIdAsync(int userId)
        {
            await CleanExpiredPendingTransactionsAsync();
            var transactions = await _transactionRepo.GetAllNoTrackingAsync(
                filter: t => t.UserId == userId,
                includeProperties: "Package"
            );
            return transactions.Select(t => new UserTransactionDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                PackageId = t.PackageId,
                Amount = t.Amount,
                PaymentGateway = t.PaymentGateway,
                TransactionStatus = t.TransactionStatus,
                CreatedAt = t.CreatedAt,
                PackageName = t.Package?.PackageName ?? "Gói đã xóa"
            }).OrderByDescending(t => t.CreatedAt).ToList();
        }

        public async Task<SubscriptionStatsDto> GetSubscriptionStatsAsync()
        {
            await CleanExpiredPendingTransactionsAsync();
            var transactions = await _transactionRepo.GetAllNoTrackingAsync(includeProperties: "Package");
            var stats = new SubscriptionStatsDto();

            if (!transactions.Any()) return stats;

            // 1. Phân loại theo Trạng thái
            stats.SuccessTransactionsCount = transactions.Count(t => t.TransactionStatus == "Success");
            stats.FailedTransactionsCount = transactions.Count(t => t.TransactionStatus == "Failed");
            stats.PendingTransactionsCount = transactions.Count(t => t.TransactionStatus == "Pending");

            // 2. Tính Tổng Doanh Thu (chỉ từ giao dịch Success)
            stats.TotalRevenue = transactions
                .Where(t => t.TransactionStatus == "Success")
                .Sum(t => t.Amount);

            // 3. Doanh thu và Số lượng bán theo từng Gói
            var successTransactions = transactions.Where(t => t.TransactionStatus == "Success").ToList();
            
            var revenueMap = new Dictionary<string, decimal>();
            var salesMap = new Dictionary<string, int>();

            foreach (var t in successTransactions)
            {
                string packageName = t.Package?.PackageName ?? "Gói đã xóa";
                
                if (!revenueMap.ContainsKey(packageName)) revenueMap[packageName] = 0;
                revenueMap[packageName] += t.Amount;

                if (!salesMap.ContainsKey(packageName)) salesMap[packageName] = 0;
                salesMap[packageName]++;
            }

            stats.RevenueByPackage = revenueMap;
            stats.SalesCountByPackage = salesMap;

            // 4. Doanh thu theo thời gian (Group theo ngày trong 30 ngày qua)
            var expiryLimit = DateTime.UtcNow.AddDays(-30);
            var last30DaysTransactions = successTransactions
                .Where(t => t.CreatedAt >= expiryLimit)
                .OrderBy(t => t.CreatedAt)
                .ToList();

            var timeMap = new Dictionary<string, decimal>();
            foreach (var t in last30DaysTransactions)
            {
                string dateStr = t.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy");
                if (!timeMap.ContainsKey(dateStr)) timeMap[dateStr] = 0;
                timeMap[dateStr] += t.Amount;
            }
            stats.RevenueOverTime = timeMap;

            // 5. Doanh thu theo tháng (12 tháng qua)
            var monthLimit = DateTime.UtcNow.AddMonths(-12);
            var last12MonthsTransactions = successTransactions
                .Where(t => t.CreatedAt >= monthLimit)
                .OrderBy(t => t.CreatedAt)
                .ToList();
            var monthMap = new Dictionary<string, decimal>();
            foreach (var t in last12MonthsTransactions)
            {
                string monthStr = t.CreatedAt.ToLocalTime().ToString("MM/yyyy");
                if (!monthMap.ContainsKey(monthStr)) monthMap[monthStr] = 0;
                monthMap[monthStr] += t.Amount;
            }
            stats.RevenueByMonth = monthMap;

            // 6. Doanh thu theo quý (trong 8 quý qua)
            var quarterLimit = DateTime.UtcNow.AddYears(-2);
            var last8QuartersTransactions = successTransactions
                .Where(t => t.CreatedAt >= quarterLimit)
                .OrderBy(t => t.CreatedAt)
                .ToList();
            var quarterMap = new Dictionary<string, decimal>();
            foreach (var t in last8QuartersTransactions)
            {
                var dt = t.CreatedAt.ToLocalTime();
                int quarter = (dt.Month - 1) / 3 + 1;
                string quarterStr = $"Quý {quarter}/{dt.Year}";
                if (!quarterMap.ContainsKey(quarterStr)) quarterMap[quarterStr] = 0;
                quarterMap[quarterStr] += t.Amount;
            }
            stats.RevenueByQuarter = quarterMap;

            // 7. Doanh thu theo năm (tất cả các năm)
            var yearMap = new Dictionary<string, decimal>();
            var sortedSuccessTransactions = successTransactions.OrderBy(t => t.CreatedAt).ToList();
            foreach (var t in sortedSuccessTransactions)
            {
                string yearStr = t.CreatedAt.ToLocalTime().ToString("yyyy");
                if (!yearMap.ContainsKey(yearStr)) yearMap[yearStr] = 0;
                yearMap[yearStr] += t.Amount;
            }
            stats.RevenueByYear = yearMap;

            return stats;
        }

        private async Task CleanExpiredPendingTransactionsAsync()
        {
            try
            {
                DateTime threshold = DateTime.UtcNow.AddMinutes(-10);
                var expiredTransactions = await _transactionRepo.GetAllAsync(
                    filter: t => t.TransactionStatus == "Pending" && t.CreatedAt < threshold
                );

                var list = expiredTransactions.ToList();
                if (list.Any())
                {
                    foreach (var t in list)
                    {
                        t.TransactionStatus = "Failed";
                        _transactionRepo.Update(t);
                    }
                    await _transactionRepo.SaveAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CLEAN EXPIRED TX ERROR] {ex.Message}");
            }
        }
    }
}
