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
        private Dictionary<DateTime, ShiftWithTransport> SelectedShiftsWithTransport { get; set; } = new();
        private int EditMonth { get; set; }
        private int EditYear { get; set; }
        private bool _isLoadingTransport = false;

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
            
            // Get transport information for this shift and date
            _isLoadingTransport = true;
            StateHasChanged();
            
            try
            {
                var request = new { ShiftName = shiftName, Date = day };
                var response = await HttpClient.PostAsJsonAsync("api/shift/shift_transport", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var shiftWithTransport = await response.Content.ReadFromJsonAsync<ShiftWithTransport>();
                    if (shiftWithTransport != null)
                    {
                        SelectedShiftsWithTransport[day] = shiftWithTransport;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching transport data: {ex.Message}");
                // Create a fallback with just the shift data
                var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
                if (shift != null)
                {
                    SelectedShiftsWithTransport[day] = new ShiftWithTransport
                    {
                        Date = day,
                        Shift = shift,
                        MorningTransport = null,
                        AfternoonTransport = null
                    };
                }
            }
            finally
            {
                _isLoadingTransport = false;
                StateHasChanged();
            }
            
            await SaveScheduleToStorage();
        }

        private async Task ExportToIcs()
        {
            var shiftsWithTransportList = SelectedShiftsWithTransport.Values.ToList();
            var response = await HttpClient.PostAsJsonAsync("api/shift/export_ics_with_transport", shiftsWithTransportList);
            var bytes = await response.Content.ReadAsByteArrayAsync();
            var base64 = Convert.ToBase64String(bytes);
            var fileUrl = $"data:text/calendar;base64,{base64}";
            NavigationManager.NavigateTo(fileUrl, true);
        }

        private async Task ExportToPdf()
        {
            var shiftsWithTransportList = SelectedShiftsWithTransport.Values.ToList();
            var response = await HttpClient.PostAsJsonAsync("api/shift/export_pdf_with_transport", shiftsWithTransportList);
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
                        
                        // Reload transport data for all selected shifts
                        await ReloadTransportDataForSchedule();
                    }
                }
            }
            catch (Exception)
            {
                SelectedSchedule = new Dictionary<DateTime, string>();
                SelectedShiftsWithTransport = new Dictionary<DateTime, ShiftWithTransport>();
            }
        }

        private async Task ReloadTransportDataForSchedule()
        {
            foreach (var kvp in SelectedSchedule)
            {
                var day = kvp.Key;
                var shiftName = kvp.Value;
                
                try
                {
                    var request = new { ShiftName = shiftName, Date = day };
                    var response = await HttpClient.PostAsJsonAsync("api/shift/shift_transport", request);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var shiftWithTransport = await response.Content.ReadFromJsonAsync<ShiftWithTransport>();
                        if (shiftWithTransport != null)
                        {
                            SelectedShiftsWithTransport[day] = shiftWithTransport;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reloading transport data for {day}: {ex.Message}");
                    // Create fallback without transport
                    var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
                    if (shift != null)
                    {
                        SelectedShiftsWithTransport[day] = new ShiftWithTransport
                        {
                            Date = day,
                            Shift = shift,
                            MorningTransport = null,
                            AfternoonTransport = null
                        };
                    }
                }
            }
        }

        private async Task ResetSchedule()
        {
            SelectedSchedule.Clear();
            SelectedShiftsWithTransport.Clear();
            
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

        // Helper method to get transport summary for display
        public string GetTransportSummary(DateTime date)
        {
            if (!SelectedShiftsWithTransport.TryGetValue(date, out var shiftWithTransport))
                return string.Empty;

            var summaries = new List<string>();
            
            var morningTransport = shiftWithTransport.GetMorningTransportSummary();
            if (!string.IsNullOrEmpty(morningTransport))
                summaries.Add($"🌅 {morningTransport}");
                
            var afternoonTransport = shiftWithTransport.GetAfternoonTransportSummary();
            if (!string.IsNullOrEmpty(afternoonTransport))
                summaries.Add($"🌆 {afternoonTransport}");
                
            return string.Join(" | ", summaries);
        }
    }
}
