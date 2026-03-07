using InfluencerMatch.Application.DTOs;

namespace InfluencerMatch.Application.Interfaces
{
    public interface ISubscriptionService
    {
        Task<SubscriptionResponseDto> SubscribeAsync(int userId, SubscribeRequestDto request, string? idempotencyKey = null);
        Task<SubscriptionResponseDto> UpgradeAsync(int userId, UpgradeSubscriptionRequestDto request, string? idempotencyKey = null);
        Task<SubscriptionResponseDto> CancelAsync(int userId, CancelSubscriptionRequestDto request, string? idempotencyKey = null);
        Task<SubscriptionResponseDto> ReactivateAsync(int userId, ReactivateSubscriptionRequestDto request, string? idempotencyKey = null);
        Task<SubscriptionResponseDto> UpdatePaymentMethodAsync(int userId, UpdatePaymentMethodRequestDto request, string? idempotencyKey = null);
        Task<SubscriptionResponseDto?> GetCurrentSubscriptionAsync(int userId);
        Task<List<InvoiceListItemDto>> GetInvoicesAsync(int userId);
        Task<string?> GetReceiptAsync(int userId, int invoiceId);
        Task<BillingSummaryDto?> GetBillingSummaryAsync(int userId, int? targetPlanId = null, string? billingCycle = null);
        Task<SubscriptionRecoveryStatusDto?> GetRecoveryStatusAsync(int userId);
        Task<SubscriptionResponseDto> RetryPaymentRecoveryAsync(int userId, string? idempotencyKey = null);
    }
}
