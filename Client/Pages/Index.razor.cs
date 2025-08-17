using Microsoft.AspNetCore.Components;
using ShiftScheduler.Shared.Models;
using System.Net.Http.Json;

namespace ShiftScheduler.Client.Pages
{
    public partial class Index : ComponentBase
    {
        [Inject] private HttpClient HttpClient { get; set; } = default!;

        private List<DateTime> DaysInMonth { get; set; } = new();
        private List<Shift> Shifts { get; set; } = new();
        private Dictionary<DateTime, string> SelectedSchedule { get; set; } = new();

        protected override async Task OnInitializedAsync()
        {
            var (nextMonth, year) = GetNextMonthAndYear();

            DaysInMonth = Enumerable.Range(1, DateTime.DaysInMonth(year, nextMonth))
                                    .Select(d => new DateTime(year, nextMonth, d))
                                    .ToList();

            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();
        }

        private (int nextMonth, int year) GetNextMonthAndYear()
        {
            var today = DateTime.Today;
            var nextMonth = today.Month == 12 ? 1 : today.Month + 1;
            var year = today.Month == 12 ? today.Year + 1 : today.Year;
            return (nextMonth, year);
        }

        private void SelectShift(DateTime day, string shiftName)
        {
            SelectedSchedule[day] = shiftName;
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

        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    }
}
