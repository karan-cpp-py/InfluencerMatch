using System;
using System.Text;
using System.Security.Claims;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/subscriptions")]
    [Authorize]
    [EnableRateLimiting("paymentsPolicy")]
    public class SubscriptionsController : ControllerBase
    {
        private readonly ISubscriptionService _subscriptionService;

        public SubscriptionsController(ISubscriptionService subscriptionService)
        {
            _subscriptionService = subscriptionService;
        }

        [HttpPost("subscribe")]
        public async Task<IActionResult> Subscribe([FromBody] SubscribeRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
                var result = await _subscriptionService.SubscribeAsync(userId, request, idempotencyKey);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("checkout")]
        public async Task<IActionResult> Checkout([FromBody] SubscribeRequestDto request)
            => await Subscribe(request);

        [HttpPost("upgrade")]
        public async Task<IActionResult> Upgrade([FromBody] UpgradeSubscriptionRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
                var result = await _subscriptionService.UpgradeAsync(userId, request, idempotencyKey);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("cancel")]
        public async Task<IActionResult> Cancel([FromBody] CancelSubscriptionRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
                var result = await _subscriptionService.CancelAsync(userId, request, idempotencyKey);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("reactivate")]
        public async Task<IActionResult> Reactivate([FromBody] ReactivateSubscriptionRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
                var result = await _subscriptionService.ReactivateAsync(userId, request, idempotencyKey);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("payment-method")]
        public async Task<IActionResult> UpdatePaymentMethod([FromBody] UpdatePaymentMethodRequestDto request)
        {
            try
            {
                var userId = GetUserId();
                var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
                var result = await _subscriptionService.UpdatePaymentMethodAsync(userId, request, idempotencyKey);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpGet("invoices")]
        public async Task<IActionResult> Invoices()
        {
            var userId = GetUserId();
            var result = await _subscriptionService.GetInvoicesAsync(userId);
            return Ok(result);
        }

        [HttpGet("invoices/{id:int}/receipt")]
        public async Task<IActionResult> Receipt(int id)
        {
            var userId = GetUserId();
            var receipt = await _subscriptionService.GetReceiptAsync(userId, id);
            if (receipt == null)
            {
                return NotFound(new { error = "Invoice not found." });
            }

            return File(Encoding.UTF8.GetBytes(receipt), "text/plain", $"receipt-{id}.txt");
        }

        [HttpGet("billing-summary")]
        public async Task<IActionResult> BillingSummary([FromQuery] int? targetPlanId, [FromQuery] string? billingCycle)
        {
            var userId = GetUserId();
            var result = await _subscriptionService.GetBillingSummaryAsync(userId, targetPlanId, billingCycle);
            if (result == null)
            {
                return NotFound(new { error = "No subscription found." });
            }

            return Ok(result);
        }

        [HttpGet("current")]
        public async Task<IActionResult> Current()
        {
            var userId = GetUserId();
            var result = await _subscriptionService.GetCurrentSubscriptionAsync(userId);
            if (result == null)
            {
                return NotFound(new { error = "No active subscription found." });
            }

            return Ok(result);
        }

        [HttpGet("recovery-status")]
        public async Task<IActionResult> RecoveryStatus()
        {
            var userId = GetUserId();
            var result = await _subscriptionService.GetRecoveryStatusAsync(userId);
            if (result == null)
            {
                return NotFound(new { error = "No subscription found." });
            }

            return Ok(result);
        }

        [HttpPost("retry-payment")]
        public async Task<IActionResult> RetryPayment()
        {
            try
            {
                var userId = GetUserId();
                var idempotencyKey = Request.Headers["Idempotency-Key"].ToString();
                var result = await _subscriptionService.RetryPaymentRecoveryAsync(userId, idempotencyKey);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        private int GetUserId()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(claim, out var userId))
            {
                throw new InvalidOperationException("Invalid user claim.");
            }

            return userId;
        }
    }
}
