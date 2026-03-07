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