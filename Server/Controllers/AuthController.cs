using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ShiftScheduler.Server.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly List<string> _authorizedEmails;

        public AuthController(List<string> authorizedEmails)
        {
            _authorizedEmails = authorizedEmails;
        }

        [HttpGet("login")]
        public IActionResult Login()
        {
            return Challenge(new AuthenticationProperties
            {
                RedirectUri = "/api/auth/callback"
            }, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("callback")]
        public async Task<IActionResult> Callback()
        {
            var result = await HttpContext.AuthenticateAsync();
            if (!result.Succeeded)
            {
                return Redirect("/?error=auth_failed");
            }

            var emailClaim = result.Principal?.FindFirst(ClaimTypes.Email) ??
                           result.Principal?.FindFirst("email");
            
            if (emailClaim?.Value == null || !_authorizedEmails.Contains(emailClaim.Value))
            {
                await HttpContext.SignOutAsync();
                return Redirect("/?error=unauthorized");
            }

            return Redirect("/");
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return Ok();
        }

        [HttpGet("user")]
        [Authorize]
        public IActionResult GetUser()
        {
            var emailClaim = User.FindFirst(ClaimTypes.Email) ??
                           User.FindFirst("email");
            
            return Ok(new { Email = emailClaim?.Value, IsAuthenticated = true });
        }
    }
}