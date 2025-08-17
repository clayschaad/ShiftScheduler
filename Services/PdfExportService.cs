using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShiftScheduler.Shared.Models;
using System.Globalization;

namespace ShiftScheduler.Services
{
    public class PdfExportService
    {
        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community; 
        }

        public byte[] GenerateMonthlySchedulePdf(Dictionary<DateTime, string> schedule, List<Shift> shifts)
        {
            var year = schedule.First().Key.Year;
            var month = schedule.First().Key.Month;
            var daysInMonth = DateTime.DaysInMonth(year, month);
            var monthDates = Enumerable.Range(1, daysInMonth)
                .Select(day => new DateTime(year, month, day))
                .ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4);
                    page.DefaultTextStyle(x => x.FontSize(12));

                    page.Header()
                        .Text($"Shift Schedule for {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}")
                        .FontSize(18).Bold().AlignCenter();

                    page.Content().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2); // Date
                            columns.RelativeColumn(2); // Shift Name
                            columns.RelativeColumn(2); // Times
                            columns.RelativeColumn(1); // Icon
                        });

                        // Header
                        table.Header(header =>
                        {
                            header.Cell().Text("Date").Bold();
                            header.Cell().Text("Shift").Bold();
                            header.Cell().Text("Times").Bold();
                            header.Cell().Text("Icon").Bold();
                        });

                        foreach (var date in monthDates)
                        {
                            var dateText = date.ToString("dd.MM.yyyy (dddd)", CultureInfo.InvariantCulture);
                            table.Cell().Text(dateText);

                            var shiftName = schedule.ContainsKey(date) ? schedule[date] : null;
                            if (shiftName != null)
                            {
                                var shift = shifts.Single(s => s.Name.Equals(shiftName));
                                table.Cell().Text(shift.Name);
                                table.Cell().Text($"{shift.MorningTime} - {shift.AfternoonTime}");

                                if (!string.IsNullOrWhiteSpace(shift.Icon))
                                {
                                    try
                                    {
                                        table.Cell().Image(shift.Icon, ImageScaling.FitArea);
                                    }
                                    catch
                                    {
                                        table.Cell().Text("[icon missing]");
                                    }
                                }
                                else
                                {
                                    table.Cell().Text("-");
                                }
                            }
                            else
                            {
                                table.Cell().Text("-");
                                table.Cell().Text("-");
                                table.Cell().Text("-");
                            }
                        }
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
