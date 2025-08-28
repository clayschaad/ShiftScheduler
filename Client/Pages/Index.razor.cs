using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using System.Text.Json;
using ShiftScheduler.Shared;

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
        private bool _isCurrentMonth = false;
        private bool _isLoadingTransport = false;
        private bool _isLoadingInitial = false;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;

        private string CurrentMonthYear => new DateTime(EditYear, EditMonth, 1).ToString("MMMM yyyy");

        protected override async Task OnInitializedAsync()
        {
            _isLoadingInitial = true;
            
            (EditMonth, EditYear) = GetNextMonthAndYear();

            UpdateDaysInMonth();

            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();

            await LoadScheduleFromStorage();
            
            _isLoadingInitial = false;
            StateHasChanged();
        }

        private void UpdateDaysInMonth()
        {
            DaysInMonth = Enumerable.Range(1, DateTime.DaysInMonth(EditYear, EditMonth))
                                    .Select(d => new DateTime(EditYear, EditMonth, d))
                                    .ToList();
        }

        private (int nextMonth, int year) GetNextMonthAndYear()
        {
            var today = DateTime.Today;
            var nextMonth = today.Month == 12 ? 1 : today.Month + 1;
            var year = today.Month == 12 ? today.Year + 1 : today.Year;
            return (nextMonth, year);
        }

        private async Task SelectCurrentMonth()
        {
            var today = DateTime.Today;
            EditMonth = today.Month;
            EditYear = today.Year;
            _isCurrentMonth = true;
            UpdateDaysInMonth();
            await LoadScheduleFromStorage();
            StateHasChanged();
        }

        private async Task SelectNextMonth()
        {
            (EditMonth, EditYear) = GetNextMonthAndYear();
            _isCurrentMonth = false;
            UpdateDaysInMonth();
            await LoadScheduleFromStorage();
            StateHasChanged();
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
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                StateHasChanged();

                var shiftsWithTransportList = SelectedShiftsWithTransport.Values.ToList();
                var response = await HttpClient.PostAsJsonAsync("api/shift/export_ics", shiftsWithTransportList);
                
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var base64 = Convert.ToBase64String(bytes);
                    var fileUrl = $"data:text/calendar;base64,{base64}";
                    NavigationManager.NavigateTo(fileUrl, true);
                    _successMessage = "ICS file exported successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to export ICS file: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error exporting ICS file: {ex.Message}";
            }
            
            StateHasChanged();
        }

        private async Task ExportToPdf()
        {
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                StateHasChanged();

                var shiftsWithTransportList = SelectedShiftsWithTransport.Values.ToList();
                var response = await HttpClient.PostAsJsonAsync("api/shift/export_pdf", shiftsWithTransportList);
                
                if (response.IsSuccessStatusCode)
                {
                    var bytes = await response.Content.ReadAsByteArrayAsync();
                    var base64 = Convert.ToBase64String(bytes);
                    await JSRuntime.InvokeVoidAsync("downloadFile", $"Schedule {EditYear}-{EditMonth:D2}.pdf", "application/pdf", base64);
                    _successMessage = "PDF file exported successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to export PDF file: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error exporting PDF file: {ex.Message}";
            }
            
            StateHasChanged();
        }

        private async Task SaveScheduleToStorage()
        {
            try
            {
                var request = new
                {
                    Year = EditYear,
                    Month = EditMonth,
                    Schedule = SelectedSchedule
                };
                
                await HttpClient.PostAsJsonAsync("api/shift/save_schedule", request);
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
                var response = await HttpClient.GetAsync($"api/shift/load_schedule/{EditYear}/{EditMonth}");
                
                if (response.IsSuccessStatusCode)
                {
                    SelectedSchedule = await response.Content.ReadFromJsonAsync<Dictionary<DateTime, string>>() ?? new();
                    
                    // Reload transport data for all selected shifts
                    await ReloadTransportDataForSchedule();
                }
                else
                {
                    SelectedSchedule = new Dictionary<DateTime, string>();
                    SelectedShiftsWithTransport = new Dictionary<DateTime, ShiftWithTransport>();
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
                await HttpClient.DeleteAsync($"api/shift/delete_schedule/{EditYear}/{EditMonth}");
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
                summaries.Add($"🚂 {morningTransport}");
                
            var afternoonTransport = shiftWithTransport.GetAfternoonTransportSummary();
            if (!string.IsNullOrEmpty(afternoonTransport))
                summaries.Add($"🚂 {afternoonTransport}");
                
            return string.Join(" | ", summaries);
        }

        // Helper method to get shift times for display
        public string GetShiftTimes(DateTime date)
        {
            if (!SelectedSchedule.TryGetValue(date, out var shiftName))
                return string.Empty;
                
            var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
            if (shift == null)
                return string.Empty;
                
            var times = new List<string>();
            
            if (!string.IsNullOrEmpty(shift.MorningTime))
                times.Add($"🌅 {shift.MorningTime}");
                
            if (!string.IsNullOrEmpty(shift.AfternoonTime))
                times.Add($"🌆 {shift.AfternoonTime}");
                
            return string.Join(" | ", times);
        }

        private bool _showConfigDialog = false;

        private void ShowConfiguration()
        {
            _showConfigDialog = true;
            StateHasChanged();
        }

        private void HideConfiguration()
        {
            _showConfigDialog = false;
            StateHasChanged();
        }

        private async Task OnConfigurationChanged()
        {
            // Reload shifts after configuration changes
            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();
            StateHasChanged();
        }
    }
}
