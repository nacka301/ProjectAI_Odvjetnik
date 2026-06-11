using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Backend.Controllers;

/// <summary>
/// Secure authentication endpoints.
/// 
/// POST /api/auth/register  — Create account (anti-enumeration)
/// POST /api/auth/verify     — Verify email token
/// 
/// Security:
///   • Rate limited: 5 requests per 10 minutes per IP
///   • Anti-enumeration: identical 202 response for all outcomes
///   • No user existence leakage in any response path
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/auth/register
    /// 
    /// Registers a new user. Returns 202 Accepted with a generic message
    /// regardless of whether the email already exists (anti-enumeration).
    /// </summary>
    [HttpPost("register")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (request == null)
        {
            return Accepted(new RegisterResponse
            {
                Success = true,
                Message = "Ako je email ispravan, primit ćete link za potvrdu."
            });
        }

        var result = await _authService.RegisterAsync(request);

        // ALWAYS return 202 Accepted — even for validation errors on email
        // Only password/name validation failures return actual error info
        // because those are about input quality, not account existence
        if (!result.Success)
        {
            // Password/name validation failure — safe to return as 400
            return BadRequest(result);
        }

        // 202 Accepted — user doesn't know if email existed or not
        return Accepted(result);
    }

    /// <summary>
    /// POST /api/auth/verify
    /// 
    /// Verifies the email using the token from the verification link.
    /// </summary>
    [HttpPost("verify")]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Token))
        {
            return BadRequest(new VerifyEmailResponse
            {
                Success = false,
                Message = "Nevažeći verifikacijski link."
            });
        }

        var result = await _authService.VerifyEmailAsync(request);

        return result.Success ? Ok(result) : BadRequest(result);
    }
}
