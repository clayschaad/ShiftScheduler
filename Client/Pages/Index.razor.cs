using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Client.Pages
{
    public partial class Index : ComponentBase
    {
        [Inject] private HttpClient HttpClient { get; set; } = default!;
        [Inject] private IJSRuntime JSRuntime { get; set; } = default!;
        [Inject] private NavigationManager NavigationManager { get; set; } = default!;
        
        private List<Shift> Shifts { get; set; } = new();
        private Dictionary<DateTime, string> SelectedSchedule { get; set; } = new();
        private Dictionary<DateTime, ShiftWithTransport> SelectedShiftsWithTransport { get; set; } = new();

        private bool _isCurrentMonth = true;
        private bool _isLoadingTransport = false;
        private bool _isLoadingInitial = false;
        private bool _showConfigDialog = false;
        private bool _isSyncing = false;
        private bool _showCalendarSelector = false;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private List<GoogleCalendar> _availableCalendars = new();

        private MonthAndYear SelectedDate => _isCurrentMonth ? MonthAndYear.Current() : MonthAndYear.Next();

        protected override async Task OnInitializedAsync()
        {
            _isLoadingInitial = true;
            
            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();

            await LoadScheduleFromStorage();
            
            _isLoadingInitial = false;
            StateHasChanged();
        }

        private async Task SelectCurrentMonth()
        {
            _isCurrentMonth = true;
            await LoadScheduleFromStorage();
            StateHasChanged();
        }

        private async Task SelectNextMonth()
        {
            _isCurrentMonth = false;
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
                    await JSRuntime.InvokeVoidAsync("downloadFile", $"Schedule {SelectedDate.Year}-{SelectedDate.Month:D2}.pdf", "application/pdf", base64);
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

        private async Task SyncToGoogleCalendar()
        {
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                _isSyncing = true;
                StateHasChanged();

                // Get available calendars
                var response = await HttpClient.GetAsync("api/shift/google_calendars");
                if (response.IsSuccessStatusCode)
                {
                    _availableCalendars = await response.Content.ReadFromJsonAsync<List<GoogleCalendar>>() ?? new();
                    
                    if (_availableCalendars.Count == 1)
                    {
                        // If only one calendar, sync directly
                        await PerformCalendarSync(_availableCalendars[0].Id);
                    }
                    else if (_availableCalendars.Count > 1)
                    {
                        // Show calendar selector
                        _showCalendarSelector = true;
                    }
                    else
                    {
                        _errorMessage = "No writable calendars found in your Google account.";
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to retrieve calendars: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error accessing Google Calendar: {ex.Message}";
            }
            finally
            {
                _isSyncing = false;
                StateHasChanged();
            }
        }

        private async Task SelectCalendarAndSync(string calendarId)
        {
            _showCalendarSelector = false;
            await PerformCalendarSync(calendarId);
        }

        private async Task PerformCalendarSync(string calendarId)
        {
            try
            {
                _isSyncing = true;
                StateHasChanged();

                var shiftsWithTransportList = SelectedShiftsWithTransport.Values.ToList();
                var request = new
                {
                    CalendarId = calendarId,
                    ShiftsWithTransport = shiftsWithTransportList
                };

                var response = await HttpClient.PostAsJsonAsync("api/shift/sync_to_google_calendar", request);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<dynamic>();
                    _successMessage = "Shifts synced to Google Calendar successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to sync to Google Calendar: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error syncing to Google Calendar: {ex.Message}";
            }
            finally
            {
                _isSyncing = false;
                StateHasChanged();
            }
        }

        private void CloseCalendarSelector()
        {
            _showCalendarSelector = false;
            _availableCalendars.Clear();
            StateHasChanged();
        }

        private async Task SaveScheduleToStorage()
        {
            try
            {
                var request = new
                {
                    Year = SelectedDate.Year,
                    Month = SelectedDate.Month,
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
                var response = await HttpClient.GetAsync($"api/shift/load_schedule/{SelectedDate.Year}/{SelectedDate.Month}");
                
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
                await HttpClient.DeleteAsync($"api/shift/delete_schedule/{SelectedDate.Year}/{SelectedDate.Month}");
            }
            catch (Exception)
            {
                // ignored
            }
        }

        // Helper method to get transport summary for display
        private string GetTransportSummary(DateTime date)
        {
            if (!SelectedShiftsWithTransport.TryGetValue(date, out var shiftWithTransport))
                return string.Empty;

            var summaries = new List<string>();
            
            var morningTransport = shiftWithTransport.GetMorningTransportSummary();
            if (!string.IsNullOrEmpty(morningTransport))
                summaries.Add(morningTransport);
                
            var afternoonTransport = shiftWithTransport.GetAfternoonTransportSummary();
            if (!string.IsNullOrEmpty(afternoonTransport))
                summaries.Add(afternoonTransport);
            
            if (summaries.Count == 0) { return string.Empty; }
                
            return "🚂 " + string.Join(" | ", summaries);
        }

        // Helper method to get shift times for display
        private string GetShiftTimes(DateTime date)
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
