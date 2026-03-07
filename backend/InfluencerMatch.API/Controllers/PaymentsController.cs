using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/payments")]
    [EnableRateLimiting("webhookPolicy")]
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentWebhookService _paymentWebhookService;

        public PaymentsController(IPaymentWebhookService paymentWebhookService)
        {
            _paymentWebhookService = paymentWebhookService;
        }

        [AllowAnonymous]
        [HttpPost("webhook/{provider}")]
        public async Task<IActionResult> Webhook(string provider)
        {
            using var reader = new StreamReader(Request.Body);
            var payload = await reader.ReadToEndAsync();
            var headers = Request.Headers.ToDictionary(k => k.Key, v => v.Value.ToString(), StringComparer.OrdinalIgnoreCase);

            try
            {
                var processed = await _paymentWebhookService.ProcessWebhookAsync(provider, payload, headers);
                return Ok(new { processed });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
