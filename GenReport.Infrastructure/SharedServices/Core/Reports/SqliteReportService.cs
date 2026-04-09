using ClosedXML.Excel;
using FluentEmail.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Interfaces;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace GenReport.Infrastructure.SharedServices.Core.Reports
{
    /// <inheritdoc />
    public sealed class SqliteReportService(
        ApplicationDbContext context,
        IFluentEmail fluentEmail,
        ILogger<SqliteReportService> logger) : ISqliteReportService
    {
        // ── Public entry point ────────────────────────────────────────────────────

        /// <inheritdoc />
        public async Task ExportAndEmailAsync(
            byte[] fileData,
            string fileName,
            string userId,
            CancellationToken ct = default)
        {
            if (!long.TryParse(userId, out var userIdLong))
                throw new ArgumentException("userId must be a valid numeric ID.", nameof(userId));

            // 1. Resolve the user's email address
            var user = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userIdLong && !u.IsDeleted)
                .Select(u => new { u.Email, u.FirstName })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"User {userId} not found.");

            logger.LogInformation("Building SQLite report for user {UserId} ({Email}), file: {FileName}",
                userId, user.Email, fileName);

            // 2. Read SQLite data
            var tables = ReadSqliteData(fileData);

            logger.LogInformation("Read {TableCount} table(s) from {FileName}", tables.Count, fileName);

            // 3. Generate Excel and PDF in memory
            var excelBytes = BuildExcel(tables, fileName);
            var pdfBytes = BuildPdf(tables, fileName);

            var baseName = Path.GetFileNameWithoutExtension(fileName);

            // 4. Send email with both attachments
            await fluentEmail
                .To(user.Email, user.FirstName)
                .Subject($"GenReport — {baseName} report")
                .Body($"""
                    <p>Hi {user.FirstName},</p>
                    <p>Your report for <strong>{fileName}</strong> is ready. Please find the Excel and PDF exports attached.</p>
                    <p>— GenReport</p>
                    """, isHtml: true)
                .Attach(new FluentEmail.Core.Models.Attachment
                {
                    Filename = $"{baseName}.xlsx",
                    Data = new MemoryStream(excelBytes),
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                })
                .Attach(new FluentEmail.Core.Models.Attachment
                {
                    Filename = $"{baseName}.pdf",
                    Data = new MemoryStream(pdfBytes),
                    ContentType = "application/pdf"
                })
                .SendAsync(ct);

            logger.LogInformation("Report email sent to {Email} for file {FileName}", user.Email, fileName);
        }

        // ── SQLite Reading ────────────────────────────────────────────────────────

        /// <summary>
        /// Writes the file bytes to a temp path, opens it with SQLite, reads every table.
        /// </summary>
        private static List<TableData> ReadSqliteData(byte[] fileData)
        {
            // SQLite requires a real file path — write to a temp file
            var tempPath = Path.GetTempFileName() + ".db";
            try
            {
                File.WriteAllBytes(tempPath, fileData);

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = tempPath,
                    Mode = SqliteOpenMode.ReadOnly
                }.ToString();

                var tables = new List<TableData>();

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                // Get all user tables
                var tableNames = new List<string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        tableNames.Add(reader.GetString(0));
                }

                foreach (var tableName in tableNames)
                {
                    var columns = new List<string>();
                    var rows = new List<string[]>();

                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"SELECT * FROM \"{tableName}\";";

                    using var reader = cmd.ExecuteReader();

                    // Column headers
                    for (var i = 0; i < reader.FieldCount; i++)
                        columns.Add(reader.GetName(i));

                    // Data rows
                    while (reader.Read())
                    {
                        var row = new string[reader.FieldCount];
                        for (var i = 0; i < reader.FieldCount; i++)
                            row[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                        rows.Add(row);
                    }

                    tables.Add(new TableData(tableName, columns, rows));
                }

                return tables;
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        // ── Excel Generation ──────────────────────────────────────────────────────

        private static byte[] BuildExcel(List<TableData> tables, string fileName)
        {
            using var workbook = new XLWorkbook();
            workbook.Properties.Title = Path.GetFileNameWithoutExtension(fileName);

            foreach (var table in tables)
            {
                // Sheet names are limited to 31 chars in Excel
                var sheetName = table.Name.Length > 31 ? table.Name[..31] : table.Name;
                var ws = workbook.Worksheets.Add(sheetName);

                // Header row
                for (var col = 0; col < table.Columns.Count; col++)
                {
                    var cell = ws.Cell(1, col + 1);
                    cell.Value = table.Columns[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D5BE3");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                // Data rows
                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var col = 0; col < table.Rows[row].Length; col++)
                        ws.Cell(row + 2, col + 1).Value = table.Rows[row][col];

                    // Alternating row background
                    if (row % 2 == 1)
                        ws.Row(row + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FF");
                }

                ws.Columns().AdjustToContents(minWidth: 8, maxWidth: 60);
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ── PDF Generation ────────────────────────────────────────────────────────

        private static byte[] BuildPdf(List<TableData> tables, string fileName)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text(Path.GetFileNameWithoutExtension(fileName))
                            .FontSize(16).Bold().FontColor("#2D5BE3");
                        col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC")
                            .FontSize(8).FontColor("#888888");
                        col.Item().PaddingTop(4).LineHorizontal(1).LineColor("#2D5BE3");
                    });

                    page.Content().Column(col =>
                    {
                        foreach (var table in tables)
                        {
                            col.Item().PaddingTop(12).Text(table.Name)
                                .FontSize(11).Bold().FontColor("#2D5BE3");

                            col.Item().PaddingTop(4).Table(t =>
                            {
                                // Define columns
                                t.ColumnsDefinition(def =>
                                {
                                    for (var i = 0; i < table.Columns.Count; i++)
                                        def.RelativeColumn();
                                });

                                // Header
                                foreach (var colName in table.Columns)
                                {
                                    t.Header(header =>
                                    {
                                        header.Cell().Background("#2D5BE3").Padding(4)
                                            .Text(colName).FontColor(Colors.White).Bold().FontSize(8);
                                    });
                                }

                                // Rows
                                for (var rowIdx = 0; rowIdx < table.Rows.Count; rowIdx++)
                                {
                                    var bgColor = rowIdx % 2 == 0 ? "#FFFFFF" : "#F3F6FF";
                                    foreach (var cell in table.Rows[rowIdx])
                                    {
                                        t.Cell().Background(bgColor).Padding(3)
                                            .Text(cell).FontSize(8);
                                    }
                                }
                            });
                        }
                    });

                    page.Footer().AlignRight()
                        .Text(text =>
                        {
                            text.Span("Page ").FontSize(8).FontColor("#888888");
                            text.CurrentPageNumber().FontSize(8).FontColor("#888888");
                            text.Span(" of ").FontSize(8).FontColor("#888888");
                            text.TotalPages().FontSize(8).FontColor("#888888");
                        });
                });
            });

            return document.GeneratePdf();
        }

        // ── Internal DTO ──────────────────────────────────────────────────────────

        private sealed record TableData(string Name, List<string> Columns, List<string[]> Rows);
    }
}
