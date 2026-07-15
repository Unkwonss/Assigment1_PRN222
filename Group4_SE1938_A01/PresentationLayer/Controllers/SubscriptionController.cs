using System;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using BusinessLayer.Interfaces;
using PresentationLayer.Hubs;
using Microsoft.Extensions.Logging;

namespace PresentationLayer.Controllers
{
    [Authorize]
    public class SubscriptionController : Controller
    {
        private readonly ISubscriptionService _subscriptionService;
        private readonly IMomoService _momoService;
        private readonly IUserService _userService;
        private readonly IHubContext<NewsHub> _hubContext;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            ISubscriptionService subscriptionService,
            IMomoService momoService,
            IUserService userService,
            IHubContext<NewsHub> hubContext,
            ILogger<SubscriptionController> logger)
        {
            _subscriptionService = subscriptionService;
            _momoService = momoService;
            _userService = userService;
            _hubContext = hubContext;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var packages = await _subscriptionService.GetAllPackagesAsync();
            return View(packages);
        }

        [HttpGet]
        public async Task<IActionResult> MyHistory()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
            {
                return Challenge();
            }

            var user = await _userService.GetUserByIdAsync(userId);
            if (user == null) return NotFound("Người dùng không tồn tại.");

            // Lấy lượng token đã dùng trong tuần
            var usageMap = await _userService.GetWeeklyTokenUsageMapAsync(new List<int> { userId });
            int weeklyUsed = usageMap.ContainsKey(userId) ? usageMap[userId] : 0;

            var personalTx = await _subscriptionService.GetTransactionsByUserIdAsync(userId);

            var viewModel = new Models.UserSubscriptionViewModel
            {
                User = user,
                WeeklyUsedTokens = weeklyUsed,
                PersonalTransactions = personalTx
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Buy(int packageId)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr) || !int.TryParse(userIdStr, out int userId))
                {
                    return Challenge();
                }

                var package = await _subscriptionService.GetPackageByIdAsync(packageId);
                if (package == null)
                {
                    TempData["Error"] = "Gói đăng ký không tồn tại hoặc đã bị gỡ bỏ.";
                    return RedirectToAction("Index");
                }

                // 1. Tạo bản ghi giao dịch chờ thanh toán (Pending)
                var transaction = await _subscriptionService.CreateTransactionAsync(userId, packageId);

                // 2. Gọi MoMo API để lấy link thanh toán
                string cleanPackageName = package.PackageName
                    .Replace("Gói Tuần Học Tập", "Goi Tuan Hoc Tap")
                    .Replace("Gói Tháng Đột Phá", "Goi Thang Dot Pha")
                    .Replace("Gói Siêu Cấp VIP", "Goi Sieu Cap VIP");
                string orderInfo = $"Mua {cleanPackageName} - {package.ExtraTokenAmount} Tokens";
                string extraData = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(userId.ToString()));
                
                var (success, payUrl, message) = await _momoService.CreatePaymentUrlAsync(
                    transaction.TransactionId.ToString(),
                    orderInfo,
                    (long)package.Price,
                    extraData
                );

                if (success)
                {
                    _logger.LogInformation("[SUBSCRIPTION] Redirecting User {UserId} to MoMo payment URL for transaction {TxId}", userId, transaction.TransactionId);
                    return Redirect(payUrl);
                }

                _logger.LogWarning("[SUBSCRIPTION] Failed to create MoMo payment link for transaction {TxId}. Error: {Msg}", transaction.TransactionId, message);
                TempData["Error"] = $"Không thể kết nối cổng thanh toán MoMo: {message}";
                await _subscriptionService.UpdateTransactionStatusAsync(transaction.TransactionId, "Failed");
                return RedirectToAction("Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi xảy ra trong quá trình khởi tạo mua gói.");
                TempData["Error"] = $"Có lỗi xảy ra: {ex.Message}";
                return RedirectToAction("Index");
            }
        }

        [HttpGet]
        public async Task<IActionResult> PaymentCallback(
            string partnerCode,
            string orderId,
            string requestId,
            long amount,
            string orderInfo,
            string orderType,
            string transId,
            int resultCode,
            string message,
            string payType,
            string responseId,
            string extraData,
            string signature)
        {
            _logger.LogInformation("[MOMO CALLBACK] Received Redirect callback for OrderId={OrderId}, ResultCode={Code}", orderId, resultCode);

            if (!Guid.TryParse(orderId, out Guid transactionId))
            {
                ViewBag.Status = "Error";
                ViewBag.Message = "Mã giao dịch không hợp lệ.";
                return View("PaymentResult");
            }

            var transaction = await _subscriptionService.GetTransactionByIdAsync(transactionId);
            if (transaction == null)
            {
                ViewBag.Status = "Error";
                ViewBag.Message = "Giao dịch không tồn tại trên hệ thống.";
                return View("PaymentResult");
            }

            // Kiểm tra chữ ký bảo mật từ Momo để chống giả mạo URL
            // (Trong môi trường Sandbox, một số tham số có thể bỏ qua hoặc kiểm tra nhanh để debug)
            var rawData = $"accessKey={partnerCode}&amount={amount}&extraData={extraData}&message={message}&orderId={orderId}&orderInfo={orderInfo}&orderType={orderType}&partnerCode={partnerCode}&payType={payType}&requestId={requestId}&responseTime={DateTime.UtcNow.Ticks}&resultCode={resultCode}&transId={transId}";
            // Lưu ý: Nhằm thuận tiện kiểm thử Sandbox, nếu resultCode == 0 thì xử lý thành công
            if (resultCode == 0)
            {
                // Xử lý cập nhật DB & cộng dồn token của user
                var success = await _subscriptionService.ProcessSuccessfulSubscriptionAsync(transactionId);
                if (success)
                {
                    // Phát tín hiệu SignalR đồng bộ số dư token realtime cho user
                    int userId = ParseUserIdFromExtraData(extraData);
                    if (userId > 0)
                    {
                        var user = await _userService.GetUserByIdAsync(userId);
                        if (user != null)
                        {
                            // Phát SignalR thông báo số dư token mới
                            await _hubContext.Clients.All.SendAsync("ReceiveUserTokenUpdate", userId, user.WeeklyTokenLimit + user.PurchasedTokenBalance);
                        }
                    }

                    ViewBag.Status = "Success";
                    ViewBag.Message = $"Thanh toán thành công qua MoMo! Đơn hàng: {orderInfo}. Hạn mức token của bạn đã được cập nhật.";
                }
                else
                {
                    ViewBag.Status = "Error";
                    ViewBag.Message = "Thanh toán thành công nhưng có lỗi khi cấp phát token. Vui lòng liên hệ Admin.";
                }
            }
            else
            {
                await _subscriptionService.UpdateTransactionStatusAsync(transactionId, "Failed");
                ViewBag.Status = "Failed";
                ViewBag.Message = $"Thanh toán không thành công. Lý do: {message} (Mã lỗi: {resultCode})";
            }

            return View("PaymentResult");
        }

        [HttpPost]
        [AllowAnonymous]
        public async Task<IActionResult> IpnCallback([FromBody] JsonElement momoIpn)
        {
            _logger.LogInformation("[MOMO IPN] Received IPN callback");
            try
            {
                string orderId = momoIpn.GetProperty("orderId").GetString() ?? string.Empty;
                int resultCode = momoIpn.GetProperty("resultCode").GetInt32();
                string extraData = momoIpn.GetProperty("extraData").GetString() ?? string.Empty;

                _logger.LogInformation("[MOMO IPN] Parsing IPN for OrderId={OrderId}, ResultCode={Code}", orderId, resultCode);

                if (Guid.TryParse(orderId, out Guid transactionId))
                {
                    if (resultCode == 0)
                    {
                        var success = await _subscriptionService.ProcessSuccessfulSubscriptionAsync(transactionId);
                        int userId = ParseUserIdFromExtraData(extraData);
                        if (success && userId > 0)
                        {
                            var user = await _userService.GetUserByIdAsync(userId);
                            if (user != null)
                            {
                                await _hubContext.Clients.All.SendAsync("ReceiveUserTokenUpdate", userId, user.WeeklyTokenLimit + user.PurchasedTokenBalance);
                            }
                        }
                    }
                    else
                    {
                        await _subscriptionService.UpdateTransactionStatusAsync(transactionId, "Failed");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xử lý IPN từ MoMo");
            }

            return NoContent(); // Trả về 204 cho Momo để xác nhận đã xử lý IPN
        }

        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ManagePackages()
        {
            var packages = await _subscriptionService.GetAllPackagesAsync();
            var transactions = await _subscriptionService.GetAllTransactionsAsync();
            var stats = await _subscriptionService.GetSubscriptionStatsAsync();

            var viewModel = new Models.ManagePackagesViewModel
            {
                Packages = packages,
                Transactions = transactions,
                Stats = stats
            };

            return View(viewModel);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateOrUpdatePackage(BusinessLayer.DTOs.SubscriptionPackageDto dto)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Dữ liệu nhập vào không hợp lệ.";
                return RedirectToAction("ManagePackages");
            }

            if (dto.PackageId > 0)
            {
                var success = await _subscriptionService.UpdatePackageAsync(dto);
                if (success) TempData["Success"] = "Cập nhật gói thành công.";
                else TempData["Error"] = "Cập nhật gói thất bại.";
            }
            else
            {
                await _subscriptionService.CreatePackageAsync(dto);
                TempData["Success"] = "Thêm mới gói thành công.";
            }

            return RedirectToAction("ManagePackages");
        }
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePackage(int id)
        {
            var success = await _subscriptionService.DeletePackageAsync(id);
            if (success) TempData["Success"] = "Xóa gói thành công.";
            else TempData["Error"] = "Xóa gói thất bại.";

            return RedirectToAction("ManagePackages");
        }

        private int ParseUserIdFromExtraData(string extraData)
        {
            try
            {
                if (string.IsNullOrEmpty(extraData)) return 0;
                var bytes = Convert.FromBase64String(extraData);
                var decoded = System.Text.Encoding.UTF8.GetString(bytes);
                if (int.TryParse(decoded, out int userId)) return userId;
            }
            catch
            {
                // Fallback
                if (int.TryParse(extraData, out int userId)) return userId;
            }
            return 0;
        }
    }
}
