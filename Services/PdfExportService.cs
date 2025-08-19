using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ShiftScheduler.Shared.Models;
using System.Globalization;

namespace ShiftScheduler.Services
{
    public class PdfExportService
    {
        private static readonly Color borderColor = Colors.Grey.Darken1;
        private static readonly float boarderWidth = 1;
        
        static PdfExportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateMonthlySchedulePdf(Dictionary<DateTime, string> schedule, IReadOnlyList<Shift> shifts, IReadOnlyList<ShiftTransport>? shiftTransports = null)
        {
            var year = schedule.First().Key.Year;
            var month = schedule.First().Key.Month;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Text($"Shift Schedule for {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}")
                        .FontSize(16).Bold().AlignCenter();
                    
                    page.Content().Element(e => ComposeTable(e, schedule, shifts, shiftTransports));
                });
            });

            return document.GeneratePdf();
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
                    page.Margin(30);
                    page.Size(PageSizes.A4.Landscape());
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header()
                        .Text($"Shift Schedule for {CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(month)} {year}")
                        .FontSize(16).Bold().AlignCenter();
                    
                    page.Content().Element(e => ComposeTable(e, shiftsWithTransport));
                });
            });

            return document.GeneratePdf();
        }
        
        void ComposeTable(IContainer container, IReadOnlyDictionary<DateTime, string> schedule, IReadOnlyList<Shift> shifts, IReadOnlyList<ShiftTransport>? shiftTransports)
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
                
                var year = schedule.First().Key.Year;
                var month = schedule.First().Key.Month;
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
                    RenderShiftForWeek(table, week, schedule, shifts, shiftTransports);
                }
            });
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
                }
            });
        }

        private static void RenderShiftForWeek(TableDescriptor table, IGrouping<int, DateTime> week,
            IReadOnlyDictionary<DateTime, string> schedule, IReadOnlyList<Shift> shifts, IReadOnlyList<ShiftTransport>? shiftTransports)
        {
            var weekDates = week.ToList();
            var dayInWeek = weekDates.Count;
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var dddd = weekDates.ElementAtOrDefault(day).ToString("dd.MM.yyyy (ddd)");
                table.Cell().Element(CellStyleFirst).Text(dddd);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftName = shifts.Single(s => s.Name == schedule[weekDates.ElementAtOrDefault(day)]).Name;
                table.Cell().Element(CellStyleMiddle).Text(shiftName);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var morningTime = shifts.Single(s => s.Name == schedule[weekDates.ElementAtOrDefault(day)]).MorningTime;
                table.Cell().Element(CellStyleMiddle).Text(morningTime);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var afternoonTime = shifts.Single(s => s.Name == schedule[weekDates.ElementAtOrDefault(day)]).AfternoonTime;
                table.Cell().Element(CellStyleMiddle).Text(afternoonTime);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var icon = shifts.Single(s => s.Name == schedule[weekDates.ElementAtOrDefault(day)]).Icon;
                table.Cell().Element(CellStyleMiddle).Text(icon);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }
            
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftName = shifts.Single(s => s.Name == schedule[weekDates.ElementAtOrDefault(day)]).Name;
                var transport = shiftTransports?.FirstOrDefault(t => t.ShiftName == shiftName);
                var transportInfo = GetTransportSummary(transport);
                table.Cell().Element(CellStyleLast).Text(transportInfo);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }
        }

        private static void RenderShiftForWeek(TableDescriptor table, IGrouping<int, DateTime> week,
            IReadOnlyDictionary<DateTime, ShiftWithTransport> shiftsWithTransport)
        {
            var weekDates = week.ToList();
            var dayInWeek = weekDates.Count;
            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var dddd = weekDates.ElementAtOrDefault(day).ToString("dd.MM.yyyy (ddd)");
                table.Cell().Element(CellStyleFirst).Text(dddd);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var shiftName = shiftWithTransport?.Shift.Name ?? "";
                table.Cell().Element(CellStyleMiddle).Text(shiftName);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var morningTime = shiftWithTransport?.Shift.MorningTime ?? "";
                table.Cell().Element(CellStyleMiddle).Text(morningTime);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var afternoonTime = shiftWithTransport?.Shift.AfternoonTime ?? "";
                table.Cell().Element(CellStyleMiddle).Text(afternoonTime);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }

            foreach (var day in Enumerable.Range(0, dayInWeek))
            {
                var shiftWithTransport = shiftsWithTransport.GetValueOrDefault(weekDates.ElementAtOrDefault(day));
                var icon = shiftWithTransport?.Shift.Icon ?? "";
                table.Cell().Element(CellStyleMiddle).Text(icon);
            }
            foreach (var day in Enumerable.Range(dayInWeek, 7 - dayInWeek))
            {
                RenderEmptyCell(table);
            }
            
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

         private static string GetTransportSummary(ShiftTransport? transport)
        {
            var transportLines = new List<string>();

            if (transport?.MorningTransport != null && !string.IsNullOrEmpty(transport.MorningTransport.DepartureTime))
            {
                var morningInfo = FormatTransportConnection(transport.MorningTransport, "Morning");
                transportLines.Add(morningInfo);
            }

            if (transport?.AfternoonTransport != null && !string.IsNullOrEmpty(transport.AfternoonTransport.DepartureTime))
            {
                var afternoonInfo = FormatTransportConnection(transport.AfternoonTransport, "Afternoon");
                transportLines.Add(afternoonInfo);
            }

            return transportLines.Count > 0 ? string.Join("\n", transportLines) : "-";
        }

        private static string GetTransportSummary(ShiftWithTransport? shiftWithTransport)
        {
            if (shiftWithTransport == null) return "-";

            var transportLines = new List<string>();

            if (shiftWithTransport.MorningTransport != null && !string.IsNullOrEmpty(shiftWithTransport.MorningTransport.DepartureTime))
            {
                var morningInfo = FormatTransportConnection(shiftWithTransport.MorningTransport, "Morning");
                transportLines.Add(morningInfo);
            }

            if (shiftWithTransport.AfternoonTransport != null && !string.IsNullOrEmpty(shiftWithTransport.AfternoonTransport.DepartureTime))
            {
                var afternoonInfo = FormatTransportConnection(shiftWithTransport.AfternoonTransport, "Afternoon");
                transportLines.Add(afternoonInfo);
            }

            return transportLines.Count > 0 ? string.Join("\n", transportLines) : "-";
        }

        private static string FormatTransportConnection(TransportConnection transport, string timeOfDay)
        {
            var departure = DateTime.TryParse(transport.DepartureTime, out var dep) ? dep.ToString("HH:mm") : transport.DepartureTime;
            var arrival = DateTime.TryParse(transport.ArrivalTime, out var arr) ? arr.ToString("HH:mm") : transport.ArrivalTime;
            
            var mainJourney = transport.Sections?.FirstOrDefault()?.Journey;
            var trainInfo = mainJourney != null ? $"{mainJourney.Category} {mainJourney.Number}" : "Train";

            return $"{timeOfDay}: {trainInfo} {departure}→{arrival}";
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