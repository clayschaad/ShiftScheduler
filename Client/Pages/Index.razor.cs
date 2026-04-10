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
        private Dictionary<DateTime, bool> _isLoadingTransportPerDay { get; set; } = new();

        private bool _isCurrentMonth = true;
        private bool _isLoadingInitial = false;
        private bool _showConfigDialog = false;
        private bool _isSyncing = false;
        private bool _showCalendarSelector = false;
        private string _syncProvider = string.Empty;
        private string _errorMessage = string.Empty;
        private string _successMessage = string.Empty;
        private List<CalendarInfo> _availableCalendars = new();

        private MonthAndYear SelectedDate => _isCurrentMonth ? MonthAndYear.Current() : MonthAndYear.Next();

        protected override async Task OnInitializedAsync()
        {
            _isLoadingInitial = true;

            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();

            _isLoadingInitial = false;
            StateHasChanged();

            _ = Task.Run(async () =>
            {
                await LoadScheduleFromStorage();
                await InvokeAsync(StateHasChanged);
            });
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

            _isLoadingTransportPerDay[day] = true;
            StateHasChanged();

            try
            {
                var request = new { ShiftName = shiftName, Date = day };
                var response = await HttpClient.PostAsJsonAsync("api/shift/shift_transport", request);

                if (response.IsSuccessStatusCode)
                {
                    var shiftWithTransport = await response.Content.ReadFromJsonAsync<ShiftWithTransport>();
                    if (shiftWithTransport != null)
                        SelectedShiftsWithTransport[day] = shiftWithTransport;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching transport data: {ex.Message}");
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
                _isLoadingTransportPerDay[day] = false;
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

                var request = new { Year = SelectedDate.Year, Month = SelectedDate.Month };
                var response = await HttpClient.PostAsJsonAsync("api/shift/export_ics", request);

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

                var request = new { Year = SelectedDate.Year, Month = SelectedDate.Month };
                var response = await HttpClient.PostAsJsonAsync("api/shift/export_pdf", request);

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

        private Task SyncToGoogleCalendar() => StartCalendarSync("google");
        private Task SyncToNextcloud() => StartCalendarSync("nextcloud");

        private async Task StartCalendarSync(string provider)
        {
            try
            {
                _errorMessage = string.Empty;
                _successMessage = string.Empty;
                _syncProvider = provider;
                _isSyncing = true;
                StateHasChanged();

                var response = await HttpClient.GetAsync($"api/shift/calendars?provider={provider}");
                if (response.IsSuccessStatusCode)
                {
                    _availableCalendars = await response.Content.ReadFromJsonAsync<List<CalendarInfo>>() ?? new();

                    if (_availableCalendars.Count == 1)
                    {
                        await PerformCalendarSync(_availableCalendars[0].Id);
                    }
                    else if (_availableCalendars.Count > 1)
                    {
                        _showCalendarSelector = true;
                    }
                    else
                    {
                        _errorMessage = "No writable calendars found.";
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
                _errorMessage = $"Error accessing calendar: {ex.Message}";
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

                var request = new
                {
                    Provider = _syncProvider,
                    CalendarId = calendarId,
                    Year = SelectedDate.Year,
                    Month = SelectedDate.Month
                };

                var response = await HttpClient.PostAsJsonAsync("api/shift/sync_to_calendar", request);

                if (response.IsSuccessStatusCode)
                {
                    _successMessage = "Shifts synced successfully!";
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _errorMessage = $"Failed to sync: {response.StatusCode}. {errorContent}";
                }
            }
            catch (Exception ex)
            {
                _errorMessage = $"Error syncing: {ex.Message}";
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
            SelectedShiftsWithTransport.Clear();
            _isLoadingTransportPerDay.Clear();

            foreach (var kvp in SelectedSchedule)
                _isLoadingTransportPerDay[kvp.Key] = true;

            StateHasChanged();

            var transportTasks = SelectedSchedule.Select(async kvp =>
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
                            SelectedShiftsWithTransport[day] = shiftWithTransport;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reloading transport data for {day}: {ex.Message}");
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
                    _isLoadingTransportPerDay[day] = false;
                    await InvokeAsync(StateHasChanged);
                }
            }).ToArray();

            await Task.WhenAll(transportTasks);
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

            if (summaries.Count == 0) return string.Empty;

            return "🚂 " + string.Join(" | ", summaries);
        }

        private string GetShiftTimes(DateTime date)
        {
            if (!SelectedSchedule.TryGetValue(date, out var shiftName))
                return string.Empty;

            var shift = Shifts.FirstOrDefault(s => s.Name == shiftName);
            if (shift == null) return string.Empty;

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
            Shifts = await HttpClient.GetFromJsonAsync<List<Shift>>("api/shift/shifts") ?? new();
            StateHasChanged();
        }

        private bool IsLoadingTransportForDay(DateTime day) =>
            _isLoadingTransportPerDay.GetValueOrDefault(day, false);
    }
}
