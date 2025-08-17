using ShiftScheduler.Shared.Models;
using ShiftScheduler.Services;

var builder = WebApplication.CreateBuilder(args);

// Load shifts from appsettings.json
var shifts = builder.Configuration.GetSection("Shifts").Get<List<Shift>>() ?? new();

builder.Services.AddSingleton(shifts);
builder.Services.AddSingleton<ShiftService>();
builder.Services.AddSingleton<IcsExportService>();
builder.Services.AddSingleton<PdfExportService>();

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
