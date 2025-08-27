using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
            // Explicitly specify the Google authentication scheme for the callback
            var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!result.Succeeded || result.Principal == null)
            {
                return Redirect("/?error=auth_failed");
            }

            var emailClaim = result.Principal.FindFirst(ClaimTypes.Email) ??
                           result.Principal.FindFirst("email");
            
            if (emailClaim?.Value == null || !_authorizedEmails.Contains(emailClaim.Value))
            {
                await HttpContext.SignOutAsync();
                return Redirect("/?error=unauthorized");
            }

            // Sign in with the cookie scheme after successful Google authentication
            await HttpContext.SignInAsync(result.Principal);

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
        public IActionResult GetUser()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var emailClaim = User.FindFirst(ClaimTypes.Email) ??
                               User.FindFirst("email");
                
                // Verify the user is in the authorized emails list
                if (emailClaim?.Value != null && _authorizedEmails.Contains(emailClaim.Value))
                {
                    return Ok(new { Email = emailClaim.Value, IsAuthenticated = true });
                }
            }
            
            return Ok(new { Email = (string?)null, IsAuthenticated = false });
        }
    }
}