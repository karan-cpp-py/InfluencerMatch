using System;
using System.Threading.Tasks;
using InfluencerMatch.Application.DTOs;
using InfluencerMatch.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace InfluencerMatch.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableRateLimiting("authPolicy")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegisterDto dto)
        {
            var auth = await _authService.RegisterAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(auth);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login(UserLoginDto dto)
        {
            var auth = await _authService.LoginAsync(dto, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(auth);
        }

        [HttpPost("google")]
        [AllowAnonymous]
        public async Task<IActionResult> GoogleLogin([FromBody] GoogleLoginDto dto)
        {
            var auth = await _authService.LoginWithGoogleAsync(
                dto.IdToken,
                dto.CustomerType,
                dto.Country,
                HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(auth);
        }

        [HttpPost("verify-email")]
        [AllowAnonymous]
        public async Task<IActionResult> VerifyEmail([FromBody] EmailVerificationRequestDto dto)
        {
            var ok = await _authService.VerifyEmailAsync(dto.Token);
            if (!ok) return BadRequest(new { error = "Invalid or expired verification token." });
            return Ok(new { message = "Email verified successfully." });
        }

        [HttpPost("request-password-reset")]
        [AllowAnonymous]
        public async Task<IActionResult> RequestPasswordReset([FromBody] PasswordResetRequestDto dto)
        {
            var token = await _authService.RequestPasswordResetAsync(dto.Email);
            return Ok(new
            {
                message = "If an account exists for this email, a reset link has been generated.",
                resetToken = token
            });
        }

        [HttpPost("reset-password")]
        [AllowAnonymous]
        public async Task<IActionResult> ResetPassword([FromBody] PasswordResetConfirmDto dto)
        {
            var ok = await _authService.ResetPasswordAsync(dto.Token, dto.NewPassword);
            if (!ok) return BadRequest(new { error = "Invalid or expired reset token." });
            return Ok(new { message = "Password reset successful." });
        }

        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto)
        {
            try
            {
                var auth = await _authService.RefreshTokenAsync(dto.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString());
                return Ok(auth);
            }
            catch (ApplicationException ex)
            {
                return Unauthorized(new { error = ex.Message });
            }
        }

        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequestDto dto)
        {
            await _authService.RevokeTokenAsync(dto.RefreshToken, HttpContext.Connection.RemoteIpAddress?.ToString());
            return Ok(new { message = "Refresh token revoked." });
        }
    }
}