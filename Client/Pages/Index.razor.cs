using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using ShiftScheduler.Shared.Models;
using System.Net.Http.Json;
using System.Text.Json;

namespace ShiftScheduler.Client.Pages
{
    public partial class Index : ComponentBase
    {
        [Inject] private HttpClient HttpClient { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;

        private List<DateTime> DaysInMonth { get; set; } = new();
        private List<Shift> Shifts { get; set; } = new();
        private Dictionary<DateTime, string> SelectedSchedule { get; set; } = new();
        private int EditMonth { get; set; }
        private int EditYear { get; set; }

        protected override async Task OnInitializedAsync()
        {
            (EditMonth, EditYear) = GetNextMonthAndYear();

            DaysInMonth = Enumerable.Range(1, DateTime.DaysInMonth(EditYear, EditMonth))
                                    .Select(d => new DateTime(EditYear, EditMonth, d))
                                    .ToList();

            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();

            await LoadScheduleFromStorage();
        }

        private (int nextMonth, int year) GetNextMonthAndYear()
        {
            var today = DateTime.Today;
            var nextMonth = today.Month == 12 ? 1 : today.Month + 1;
            var year = today.Month == 12 ? today.Year + 1 : today.Year;
            return (nextMonth, year);
        }

        private async Task SelectShift(DateTime day, string shiftName)
        {
            SelectedSchedule[day] = shiftName;
            await SaveScheduleToStorage();
        }

        private async Task ExportToIcs()
        {
            var response = await HttpClient.PostAsJsonAsync("api/shift/export_ics", SelectedSchedule);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);
            var fileUrl = $"data:text/calendar;base64,{base64}";
            NavigationManager.NavigateTo(fileUrl, true);
        }

        private async Task ExportToPdf()
        {
            var response = await HttpClient.PostAsJsonAsync("api/shift/export_pdf", SelectedSchedule);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);
            var fileUrl = $"data:application/octet-stream;base64,{base64}";
            NavigationManager.NavigateTo(fileUrl, true);
        }

        private async Task SaveScheduleToStorage()
        {
            try
            {
                var storageKey = $"ShiftSchedule_{EditYear}_{EditMonth:D2}";
                
                var serializableSchedule = SelectedSchedule.ToDictionary(
                    kvp => kvp.Key.ToString("yyyy-MM-dd"),
                    kvp => kvp.Value
                );
                
                var json = JsonSerializer.Serialize(serializableSchedule);
                await JSRuntime.InvokeVoidAsync("localStorage.setItem", storageKey, json);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async Task LoadScheduleFromStorage()
        {
            try
            {
                var storageKey = $"ShiftSchedule_{EditYear}_{EditMonth:D2}";
                var json = await JSRuntime.InvokeAsync<string>("localStorage.getItem", storageKey);
                
                if (!string.IsNullOrEmpty(json))
                {
                    var serializableSchedule = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (serializableSchedule != null)
                    {
                        SelectedSchedule = serializableSchedule.ToDictionary(
                            kvp => DateTime.Parse(kvp.Key),
                            kvp => kvp.Value
                        );
                    }
                }
            }
            catch (Exception)
            {
                SelectedSchedule = new Dictionary<DateTime, string>();
            }
        }

        private async Task ResetSchedule()
        {
            SelectedSchedule.Clear();
            
            try
            {
                var storageKey = $"ShiftSchedule_{EditYear}_{EditMonth:D2}";
                await JSRuntime.InvokeVoidAsync("localStorage.removeItem", storageKey);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }
}
