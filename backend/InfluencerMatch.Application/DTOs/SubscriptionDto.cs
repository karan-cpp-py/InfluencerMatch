namespace InfluencerMatch.Application.DTOs
{
    public class SubscriptionPlanDto
    {
        public int PlanId { get; set; }
        public string PlanName { get; set; } = string.Empty;
        public decimal PriceMonthly { get; set; }
        public decimal PriceYearly { get; set; }
        public int? MaxCreatorSearch { get; set; }
        public bool ExportAllowed { get; set; }
        public string AnalyticsAccessLevel { get; set; } = "Basic";
    }

    public class SubscribeRequestDto
    {
        public int PlanId { get; set; }
        public string BillingCycle { get; set; } = "monthly";
        public string PaymentProvider { get; set; } = "stripe";
        public string Currency { get; set; } = "INR";
    }

    public class UpgradeSubscriptionRequestDto : SubscribeRequestDto
    {
    }

    public class CancelSubscriptionRequestDto
    {
        public string Reason { get; set; } = "User requested cancellation";
    }

    public class ReactivateSubscriptionRequestDto
    {
        public string Reason { get; set; } = "User requested reactivation";
    }

    public class UpdatePaymentMethodRequestDto
    {
        public string Brand { get; set; } = "Card";
        public string Last4 { get; set; } = string.Empty;
    }

    public class SubscriptionResponseDto
    {
        public int SubscriptionId { get; set; }
        public int UserId { get; set; }
        public SubscriptionPlanDto Plan { get; set; } = new();
        public string BillingCycle { get; set; } = "monthly";
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Active";
        public string PaymentStatus { get; set; } = "Pending";
        public int CreatorSearchUsed { get; set; }

        // Payment context used by checkout confirmation UI.
        public string? PaymentProvider { get; set; }
        public string? ProviderPaymentId { get; set; }
        public string? PaymentMessage { get; set; }
        public bool AutoRenew { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public DateTime? CancelledAt { get; set; }
        public string? PaymentMethodDisplay { get; set; }
        public DateTime NextBillingDate { get; set; }
        public DateTime? GracePeriodEndsAt { get; set; }
        public int PaymentRetryCount { get; set; }
        public DateTime? LastPaymentRetryAt { get; set; }
    }

    public class SubscriptionRecoveryStatusDto
    {
        public bool InGracePeriod { get; set; }
        public DateTime? GracePeriodEndsAt { get; set; }
        public int GraceDaysRemaining { get; set; }
        public int PaymentRetryCount { get; set; }
        public string CurrentPaymentStatus { get; set; } = string.Empty;
        public string SuggestedAction { get; set; } = string.Empty;
    }

    public class InvoiceListItemDto
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string Status { get; set; } = "Pending";
        public string Provider { get; set; } = string.Empty;
        public string? ProviderInvoiceId { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class BillingSummaryDto
    {
        public string CurrentPlanName { get; set; } = string.Empty;
        public string BillingCycle { get; set; } = "monthly";
        public DateTime NextBillingDate { get; set; }
        public decimal CurrentCycleAmount { get; set; }
        public decimal? ProrationPreviewAmount { get; set; }
        public string Currency { get; set; } = "INR";
    }

    public class PaymentInitResultDto
    {
        public string Provider { get; set; } = string.Empty;
        public string PaymentStatus { get; set; } = "Pending";
        public string? ProviderPaymentId { get; set; }
        public string? Message { get; set; }
    }

    public class SearchAccessResultDto
    {
        public bool Allowed { get; set; }
        public string? Reason { get; set; }
        public int? RemainingSearches { get; set; }
        public string? RequiredPlan { get; set; }
        public string? CurrentPlan { get; set; }
        public string? ErrorCode { get; set; }
    }

    public class FeatureAccessResultDto
    {
        public bool Allowed { get; set; }
        public string? Reason { get; set; }
        public string? RequiredPlan { get; set; }
        public string? CurrentPlan { get; set; }
        public string? ErrorCode { get; set; }
    }
}
