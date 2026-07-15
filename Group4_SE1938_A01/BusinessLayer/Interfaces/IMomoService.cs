using System.Threading.Tasks;

namespace BusinessLayer.Interfaces
{
    public interface IMomoService
    {
        Task<(bool Success, string PayUrl, string Message)> CreatePaymentUrlAsync(string orderId, string orderInfo, long amount, string extraData = "");
        bool ValidateCallbackSignature(string rawData, string signatureFromMomo);
    }
}
