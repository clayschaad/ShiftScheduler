using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using ShiftScheduler.Shared;

namespace ShiftScheduler.Services
{
    public class PdfExportService
    {
        private static readonly Color borderColor = Colors.Grey.Darken1;
        private static readonly float boarderWidth = 1;
        
        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.FontDiscoveryPaths.Add("fonts");
        }

        public byte[] GenerateMonthlySchedulePdf(List<ShiftWithTransport> shiftsWithTransport)
        {
            if (!shiftsWithTransport.Any())
                return new byte[0];

            var year = shiftsWithTransport.First().Date.Year;
            var month = shiftsWithTransport.First().Date.Month;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(15);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Header()
                        .Text($"Shift Schedule for {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}")
                        .FontSize(16).Bold().AlignCenter();
                    
                    page.Content().Element(e => ComposeTable(e, shiftsWithTransport));
                });
            });

            return document.GeneratePdf();
        }

        void ComposeTable(IContainer container, List<ShiftWithTransport> shiftsWithTransport)
        {
            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                    columns.RelativeColumn();
                });
                
                var scheduleDictionary = shiftsWithTransport.ToDictionary(s => s.Date, s => s);
                var year = shiftsWithTransport.First().Date.Year;
                var month = shiftsWithTransport.First().Date.Month;
                var daysInMonth = DateTime.DaysInMonth(year, month);
                var monthDates = Enumerable.Range(1, daysInMonth)
                    .Select(day => new DateTime(year, month, day))
                    .ToList();
                var weeks = monthDates
                    .GroupBy(d => CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(
                        d, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday))
                    .ToList();
                foreach (var week in weeks)
                {
                    RenderShiftForWeek(table, week, scheduleDictionary);
                    RenderEmptyRow(table);
                }
            });
        }

        private static void RenderShiftForWeek(TableDescriptor table, IGrouping<int, DateTime> week,
            IReadOnlyDictionary<DateTime, ShiftWithTransport> shiftsWithTransport)
        {
            var weekDates = week.ToList();
            var dayInWeek = weekDates.Count;
            
            // Date
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var dddd = weekDates.ElementAtOrDefault(day).ToString("dd.MM.yyyy (ddd)");
                table.Cell().Element(CellStyleFirst).Text(dddd);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }
            
            // Icon
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var shift = shiftWithTransport?.Shift;
                
                if (shift != null && shift.IsPngIcon)
                {
                    // For PNG icons, embed the image
                    var iconPath = Path.Combine(AppContext.BaseDirectory, "wwwroot", "icons", shift.Icon);
                    if (File.Exists(iconPath))
                    {
                        table.Cell().Height(24).Element(CellStyleMiddle).Image(iconPath).FitArea();
                    }
                    else
                    {
                        table.Cell().Element(CellStyleMiddle).Text(shift.Name);
                    }
                }
                else
                {
                    // For text/emoji icons, render as text
                    var icon = shift?.Icon ?? shift?.Name;
                    table.Cell().Element(CellStyleMiddle).Text(icon);
                }
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            // Morning times
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var morningTime = shiftWithTransport?.Shift.MorningTime.Length > 0 ? $"🌅 {shiftWithTransport?.Shift.MorningTime}" : "";
                table.Cell().Element(CellStyleMiddle).Text(morningTime);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            // Afternoon times
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var afternoonTime = shiftWithTransport?.Shift.AfternoonTime.Length > 0 ? $"🌆 {shiftWithTransport?.Shift.AfternoonTime}" : "";
                table.Cell().Element(CellStyleMiddle).Text(afternoonTime);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }
            
            // Transport
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var transportInfo = GetTransportSummary(shiftWithTransport);
                table.Cell().Element(CellStyleLast).Text(transportInfo);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }
        }
        
        private static void RenderEmptyRow(TableDescriptor table)
        {
            foreach (var day in Enumerable.Range(0, 7))
            {
                RenderEmptyCell(table);
            }
        }

        private static string GetTransportSummary(ShiftWithTransport? shiftWithTransport)
        {
            if (shiftWithTransport == null) return "-";

            var transportLines = new List<string>();

            if (shiftWithTransport.MorningTransport != null)
            {
                var morningInfo = FormatTransportConnection(shiftWithTransport.MorningTransport);
                transportLines.Add(morningInfo);
            }

            if (shiftWithTransport.AfternoonTransport != null)
            {
                var afternoonInfo = FormatTransportConnection(shiftWithTransport.AfternoonTransport);
                transportLines.Add(afternoonInfo);
            }

            return transportLines.Count > 0 ? string.Join("\n", transportLines) : "";
        }

        private static string FormatTransportConnection(TransportConnection transport)
        {
            var departure = transport.DepartureTime.ToString("HH:mm");
            var arrival = transport.ArrivalTime.ToString("HH:mm");
            return $"🚂 {departure}→{arrival}";
        }

        private static void RenderEmptyCell(TableDescriptor table)
        {
            table.Cell().Element(EmptyCellStyle).Text("");
        }
        
        private static IContainer CellStyleFirst(IContainer container)
        {
            return container
                .Padding(0)
                .BorderLeft(boarderWidth)
                .BorderTop(boarderWidth)
                .BorderRight(boarderWidth)
                .BorderColor(borderColor)
                .AlignCenter()
                .AlignMiddle();
        }
        
        private static IContainer CellStyleMiddle(IContainer container)
        {
            return container
                .Padding(0)
                .BorderLeft(boarderWidth)
                .BorderRight(boarderWidth)
                .BorderColor(borderColor)
                .AlignCenter()
                .AlignMiddle();
        }
        
        private static IContainer CellStyleLast(IContainer container)
        {
            return container
                .Padding(0)
                .BorderLeft(boarderWidth)
                .BorderBottom(boarderWidth)
                .BorderRight(boarderWidth)
                .BorderColor(borderColor)
                .AlignCenter()
                .AlignMiddle();
        }
        
        private static IContainer EmptyCellStyle(IContainer container)
        {
            return container
                .Padding(0)
                .BorderLeft(0)
                .BorderBottom(0)
                .BorderRight(0);
        }
    }
}