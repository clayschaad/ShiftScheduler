using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using ShiftScheduler.Services;
using ShiftScheduler.Shared;

var builder = WebApplication.CreateBuilder(args);

// Load configurations from appsettings.json
var shifts = builder.Configuration.GetSection("Shifts").Get<List<Shift>>() ?? new();
var transportConfig = builder.Configuration.GetSection("Transport").Get<TransportConfiguration>() ?? new();
var authorizedEmails = builder.Configuration.GetSection("Authentication:AuthorizedEmails").Get<List<string>>() ?? new();

// Create application configuration
var appConfiguration = new ApplicationConfiguration
{
    Transport = transportConfig,
    Shifts = shifts
};

// Register services
builder.Services.AddSingleton(authorizedEmails);
builder.Services.AddSingleton<IConfigurationService>(new ConfigurationService(appConfiguration));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<TransportApiService>();
builder.Services.AddSingleton<IcsExportService>();
builder.Services.AddSingleton<PdfExportService>();
builder.Services.AddSingleton<ITransportApiService, TransportApiService>();
builder.Services.AddSingleton<ITransportService, TransportService>();

// Configure authentication
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "";
    googleOptions.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "";
});

// Configure authorization policy for allowed emails
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AllowedEmails", policy =>
        policy.RequireAssertion(context =>
        {
            var emailClaim = context.User.FindFirst(ClaimTypes.Email) ??
                           context.User.FindFirst("email");
            if (emailClaim?.Value != null)
            {
                return authorizedEmails.Contains(emailClaim.Value);
            }
            return false;
        }));
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
