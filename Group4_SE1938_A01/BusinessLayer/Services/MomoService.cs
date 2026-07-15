using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using BusinessLayer.Models;
using BusinessLayer.Interfaces;

namespace BusinessLayer.Services
{
    public class MomoService : IMomoService
    {
        private readonly MomoSettings _settings;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<MomoService> _logger;

        public MomoService(
            IOptions<MomoSettings> settings,
            IHttpClientFactory httpClientFactory,
            ILogger<MomoService> logger)
        {
            _settings = settings.Value;
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        private string ComputeHmacSha256(string message, string secretKey)
        {
            var keyBytes = Encoding.UTF8.GetBytes(secretKey);
            var messageBytes = Encoding.UTF8.GetBytes(message);
            using (var hmac = new HMACSHA256(keyBytes))
            {
                var hashBytes = hmac.ComputeHash(messageBytes);
                return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
            }
        }

        public async Task<(bool Success, string PayUrl, string Message)> CreatePaymentUrlAsync(string orderId, string orderInfo, long amount, string extraData = "")
        {
            try
            {
                var requestId = Guid.NewGuid().ToString();
                var requestType = "captureWallet";

                // Sắp xếp các trường và tạo chuỗi thô để ký theo đúng tài liệu MoMo API
                var rawSignature = $"accessKey={_settings.AccessKey}" +
                                   $"&amount={amount}" +
                                   $"&extraData={extraData}" +
                                   $"&ipnUrl={_settings.IpnUrl}" +
                                   $"&orderId={orderId}" +
                                   $"&orderInfo={orderInfo}" +
                                   $"&partnerCode={_settings.PartnerCode}" +
                                   $"&redirectUrl={_settings.RedirectUrl}" +
                                   $"&requestId={requestId}" +
                                   $"&requestType={requestType}";

                var signature = ComputeHmacSha256(rawSignature, _settings.SecretKey);

                var requestBody = new
                {
                    partnerCode = _settings.PartnerCode,
                    accessKey = _settings.AccessKey,
                    partnerName = "VietRAG System",
                    storeId = "VietRAG_Store",
                    requestId = requestId,
                    amount = amount.ToString("F0"),
                    orderId = orderId,
                    orderInfo = orderInfo,
                    redirectUrl = _settings.RedirectUrl,
                    ipnUrl = _settings.IpnUrl,
                    lang = "vi",
                    extraData = extraData,
                    requestType = requestType,
                    signature = signature
                };

                var client = _httpClientFactory.CreateClient();
                var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

                _logger.LogInformation("[MOMO] Sending payment request for OrderId={OrderId}, Amount={Amount}", orderId, amount.ToString("F0"));
                var response = await client.PostAsync(_settings.PaymentUrl, jsonContent);
                var responseContent = await response.Content.ReadAsStringAsync();

                _logger.LogInformation("[MOMO] Response: {Response}", responseContent);

                if (!response.IsSuccessStatusCode)
                {
                    return (false, string.Empty, $"MoMo API error: {response.StatusCode}");
                }

                using var doc = JsonDocument.Parse(responseContent);
                var root = doc.RootElement;

                if (root.TryGetProperty("resultCode", out var resCodeProp) && resCodeProp.GetInt32() == 0)
                {
                    if (root.TryGetProperty("payUrl", out var payUrlProp))
                    {
                        return (true, payUrlProp.GetString() ?? string.Empty, "Thành công");
                    }
                }

                var message = root.TryGetProperty("message", out var msgProp) ? msgProp.GetString() : "Lỗi không xác định từ MoMo";
                return (false, string.Empty, message ?? "Lỗi không xác định");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi MoMo API: {Message}", ex.Message);
                return (false, string.Empty, $"Exception: {ex.Message}");
            }
        }

        public bool ValidateCallbackSignature(string rawData, string signatureFromMomo)
        {
            var calculatedSignature = ComputeHmacSha256(rawData, _settings.SecretKey);
            return calculatedSignature.Equals(signatureFromMomo, StringComparison.OrdinalIgnoreCase);
        }
    }
}
