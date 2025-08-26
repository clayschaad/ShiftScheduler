using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Json;
using System.Security.Claims;

namespace ShiftScheduler.Client
{
    public class ServerAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;

        public ServerAuthenticationStateProvider(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var userInfo = await _httpClient.GetFromJsonAsync<UserInfo>("api/auth/user");
                
                if (userInfo?.IsAuthenticated == true && !string.IsNullOrEmpty(userInfo.Email))
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.Name, userInfo.Email),
                        new Claim(ClaimTypes.Email, userInfo.Email)
                    };

                    var identity = new ClaimsIdentity(claims, "Server authentication");
                    var user = new ClaimsPrincipal(identity);

                    return new AuthenticationState(user);
                }
            }
            catch (HttpRequestException)
            {
                // User is not authenticated
            }

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public void NotifyAuthenticationStateChanged()
        {
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }

    public class UserInfo
    {
        public string? Email { get; set; }
        public bool IsAuthenticated { get; set; }
    }
}