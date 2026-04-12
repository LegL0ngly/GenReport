using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClosedXML.Excel;
using FluentEmail.Core;
using GenReport.Domain.DBContext;
using GenReport.Infrastructure.Configuration;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.Reports;
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
        IHttpClientFactory httpClientFactory,
        ILogger<SqliteReportService> logger) : ISqliteReportService
    {
        // ── Public — legacy attachment-only entry point ───────────────────────

        /// <inheritdoc />
        public async Task ExportAndEmailAsync(
            byte[] fileData,
            string fileName,
            string userId,
            CancellationToken ct = default)
        {
            // Delegate to the new method with no R2 config → always attaches files.
            await ExportAndDeliverAsync(fileData, fileName, userId, r2Config: null, ct);
        }

        // ── Public — conditional R2 / attachment delivery ─────────────────────

        /// <inheritdoc />
        public async Task<ReportDeliveryResult> ExportAndDeliverAsync(
            byte[] fileData,
            string fileName,
            string userId,
            R2Configuration? r2Config = null,
            CancellationToken ct = default)
        {
            if (!long.TryParse(userId, out var userIdLong))
                throw new ArgumentException("userId must be a valid numeric ID.", nameof(userId));

            // 1. Resolve the user's email address.
            var user = await context.Users
                .AsNoTracking()
                .Where(u => u.Id == userIdLong && !u.IsDeleted)
                .Select(u => new { u.Email, u.FirstName })
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"User {userId} not found.");

            logger.LogInformation("Building SQLite report for user {UserId} ({Email}), file: {FileName}",
                userId, user.Email, fileName);

            // 2. Read SQLite data (single pass — captures rows, columns, and raw data).
            var (tables, noOfRows, noOfColumns) = ReadSqliteData(fileData);

            logger.LogInformation("Read {TableCount} table(s), {Rows} rows, {Cols} columns from {FileName}",
                tables.Count, noOfRows, noOfColumns, fileName);

            // 3. Generate Excel and PDF in memory.
            var excelBytes = BuildExcel(tables, fileName);
            var pdfBytes   = BuildPdf(tables, fileName);
            var baseName   = Path.GetFileNameWithoutExtension(fileName);

            // 4a. Try R2 upload if configured.
            string? r2Url = null;
            if (r2Config?.IsConfigured == true)
            {
                r2Url = await TryUploadToR2Async(excelBytes, $"{baseName}.xlsx", r2Config, ct);
            }

            // 4b. Send email — link if R2 succeeded, attachments otherwise.
            if (r2Url is not null)
            {
                await SendLinkEmailAsync(user.Email, user.FirstName, baseName, r2Url, ct);
            }
            else
            {
                await SendAttachmentEmailAsync(user.Email, user.FirstName, baseName, fileName, excelBytes, pdfBytes, ct);
            }

            logger.LogInformation(
                "Report delivered for user {UserId} via {Mode} (rows={Rows}, cols={Cols})",
                userId, r2Url is not null ? "R2 link" : "email attachment", noOfRows, noOfColumns);

            return new ReportDeliveryResult(r2Url, noOfRows, noOfColumns, excelBytes.LongLength);
        }

        // ── R2 Upload ─────────────────────────────────────────────────────────

        private async Task<string?> TryUploadToR2Async(
            byte[] excelBytes,
            string fileName,
            R2Configuration r2Config,
            CancellationToken ct)
        {
            try
            {
                var client = httpClientFactory.CreateClient("GoService");
                var payload = new
                {
                    fileName,
                    content  = Convert.ToBase64String(excelBytes),
                    mimeType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                };

                using var response = await client.PostAsJsonAsync("/storage/upload", payload, ct);
                if (!response.IsSuccessStatusCode)
                {
                    logger.LogWarning("R2 upload failed with status {Status} — falling back to attachment.",
                        response.StatusCode);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<UploadResponse>(cancellationToken: ct);
                if (string.IsNullOrWhiteSpace(result?.Url))
                {
                    logger.LogWarning("R2 upload returned empty URL — falling back to attachment.");
                    return null;
                }

                logger.LogInformation("R2 upload succeeded: {Url}", result.Url);
                return result.Url;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "R2 upload threw an exception — falling back to attachment.");
                return null;
            }
        }

        // ── Email helpers ─────────────────────────────────────────────────────

        private async Task SendLinkEmailAsync(
            string email, string firstName, string baseName, string r2Url, CancellationToken ct)
        {
            await fluentEmail
                .To(email, firstName)
                .Subject($"GenReport — {baseName} report ready")
                .Body($"""
                    <p>Hi {firstName},</p>
                    <p>Your report for <strong>{baseName}</strong> is ready.</p>
                    <p><a href="{r2Url}" style="background:#2D5BE3;color:#fff;padding:10px 20px;border-radius:6px;text-decoration:none;">Download Excel Report</a></p>
                    <p style="color:#888;font-size:12px;">Link expires after 7 days.</p>
                    <p>— GenReport</p>
                    """, isHtml: true)
                .SendAsync(ct);
        }

        private async Task SendAttachmentEmailAsync(
            string email, string firstName, string baseName, string fileName,
            byte[] excelBytes, byte[] pdfBytes, CancellationToken ct)
        {
            await fluentEmail
                .To(email, firstName)
                .Subject($"GenReport — {baseName} report")
                .Body($"""
                    <p>Hi {firstName},</p>
                    <p>Your report for <strong>{fileName}</strong> is ready. Please find the Excel and PDF exports attached.</p>
                    <p>— GenReport</p>
                    """, isHtml: true)
                .Attach(new FluentEmail.Core.Models.Attachment
                {
                    Filename    = $"{baseName}.xlsx",
                    Data        = new MemoryStream(excelBytes),
                    ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                })
                .Attach(new FluentEmail.Core.Models.Attachment
                {
                    Filename    = $"{baseName}.pdf",
                    Data        = new MemoryStream(pdfBytes),
                    ContentType = "application/pdf"
                })
                .SendAsync(ct);
        }

        // ── SQLite Reading ────────────────────────────────────────────────────

        /// <summary>
        /// Writes bytes to a temp file, opens it with SQLite, reads every table.
        /// Returns the table data together with aggregate row and column counts.
        /// </summary>
        private static (List<TableData> Tables, int NoOfRows, int NoOfColumns) ReadSqliteData(byte[] fileData)
        {
            var tempPath = Path.GetTempFileName() + ".db";
            try
            {
                File.WriteAllBytes(tempPath, fileData);

                var connectionString = new SqliteConnectionStringBuilder
                {
                    DataSource = tempPath,
                    Mode       = SqliteOpenMode.ReadOnly
                }.ToString();

                var tables = new List<TableData>();
                int totalRows = 0, totalCols = 0;

                using var connection = new SqliteConnection(connectionString);
                connection.Open();

                var tableNames = new List<string>();
                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText =
                        "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
                    using var reader = cmd.ExecuteReader();
                    while (reader.Read())
                        tableNames.Add(reader.GetString(0));
                }

                foreach (var tableName in tableNames)
                {
                    var columns = new List<string>();
                    var rows    = new List<string[]>();

                    using var cmd = connection.CreateCommand();
                    cmd.CommandText = $"SELECT * FROM \"{tableName}\";";

                    using var reader = cmd.ExecuteReader();

                    for (var i = 0; i < reader.FieldCount; i++)
                        columns.Add(reader.GetName(i));

                    while (reader.Read())
                    {
                        var row = new string[reader.FieldCount];
                        for (var i = 0; i < reader.FieldCount; i++)
                            row[i] = reader.IsDBNull(i) ? string.Empty : reader.GetValue(i)?.ToString() ?? string.Empty;
                        rows.Add(row);
                    }

                    totalRows += rows.Count;
                    totalCols  = Math.Max(totalCols, columns.Count);
                    tables.Add(new TableData(tableName, columns, rows));
                }

                return (tables, totalRows, totalCols);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }

        // ── Excel Generation ──────────────────────────────────────────────────

        private static byte[] BuildExcel(List<TableData> tables, string fileName)
        {
            using var workbook = new XLWorkbook();
            workbook.Properties.Title = Path.GetFileNameWithoutExtension(fileName);

            foreach (var table in tables)
            {
                var sheetName = table.Name.Length > 31 ? table.Name[..31] : table.Name;
                var ws = workbook.Worksheets.Add(sheetName);

                for (var col = 0; col < table.Columns.Count; col++)
                {
                    var cell = ws.Cell(1, col + 1);
                    cell.Value = table.Columns[col];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#2D5BE3");
                    cell.Style.Font.FontColor = XLColor.White;
                    cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }

                for (var row = 0; row < table.Rows.Count; row++)
                {
                    for (var col = 0; col < table.Rows[row].Length; col++)
                        ws.Cell(row + 2, col + 1).Value = table.Rows[row][col];
                    if (row % 2 == 1)
                        ws.Row(row + 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F6FF");
                }

                ws.Columns().AdjustToContents(minWidth: 8, maxWidth: 60);
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        // ── PDF Generation ────────────────────────────────────────────────────

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
                                t.ColumnsDefinition(def =>
                                {
                                    for (var i = 0; i < table.Columns.Count; i++)
                                        def.RelativeColumn();
                                });

                                // Header — must call t.Header exactly once; all cells go inside.
                                t.Header(header =>
                                {
                                    foreach (var colName in table.Columns)
                                    {
                                        header.Cell().Background("#2D5BE3").Padding(4)
                                            .Text(colName).FontColor(Colors.White).Bold().FontSize(8);
                                    }
                                });

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

        // ── Internal types ────────────────────────────────────────────────────

        private sealed record TableData(string Name, List<string> Columns, List<string[]> Rows);

        /// <summary>Shape of the JSON body returned by Go's POST /storage/upload.</summary>
        private sealed class UploadResponse
        {
            [JsonPropertyName("url")]
            public string? Url { get; set; }
        }
    }
}
