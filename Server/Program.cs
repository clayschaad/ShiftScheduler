using ShiftScheduler.Services;
using ShiftScheduler.Shared;

var builder = WebApplication.CreateBuilder(args);

// Load configurations from appsettings.json
var shifts = builder.Configuration.GetSection("Shifts").Get<List<Shift>>() ?? new();
var transportConfig = builder.Configuration.GetSection("Transport").Get<TransportConfiguration>() ?? new();

// Create application configuration
var appConfiguration = new ApplicationConfiguration
{
    Transport = transportConfig,
    Shifts = shifts
};

// Register services
builder.Services.AddSingleton<IConfigurationService>(new ConfigurationService(appConfiguration));
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<TransportApiService>();
builder.Services.AddSingleton<ShiftService>();
builder.Services.AddSingleton<IcsExportService>();
builder.Services.AddSingleton<PdfExportService>();
builder.Services.AddSingleton<ITransportApiService, TransportApiService>();
builder.Services.AddSingleton<ITransportService, TransportService>();

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


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
