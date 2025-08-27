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
                RedirectUri = "/"
            }, GoogleDefaults.AuthenticationScheme);
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