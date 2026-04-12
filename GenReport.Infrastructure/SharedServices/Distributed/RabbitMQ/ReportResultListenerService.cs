using System.Text;
using System.Text.Json;
using GenReport.DB.Domain.Entities.Core;
using GenReport.Domain.DBContext;
using GenReport.Domain.Entities.Media;
using GenReport.Infrastructure.Interfaces;
using GenReport.Infrastructure.Models.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace GenReport.Infrastructure.SharedServices.Distributed.RabbitMQ
{
    /// <summary>
    /// Long-running hosted service that subscribes to the <c>report_success</c> and
    /// <c>report_error</c> queues published by the Go report worker.
    ///
    /// <list type="bullet">
    ///   <item>
    ///     <term>report_success</term>
    ///     <description>
    ///       Reads the SQLite file, conditionally uploads Excel to R2 or attaches it to
    ///       email, emails the user, then persists Query / MediaFile / Report / MessageReport
    ///       rows in the database.
    ///     </description>
    ///   </item>
    ///   <item>
    ///     <term>report_error</term>
    ///     <description>Logs the failure for observability.</description>
    ///   </item>
    /// </list>
    /// </summary>
    public sealed class ReportResultListenerService : BackgroundService
    {
        private const string QueueSuccess = "report_success";
        private const string QueueError   = "report_error";

        private readonly IApplicationConfiguration              _config;
        private readonly IServiceScopeFactory                   _scopeFactory;
        private readonly ILogger<ReportResultListenerService>   _logger;

        private IConnection? _connection;
        private IModel?      _channel;

        public ReportResultListenerService(
            IApplicationConfiguration config,
            IServiceScopeFactory scopeFactory,
            ILogger<ReportResultListenerService> logger)
        {
            _config       = config;
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            _logger.LogInformation(
                "[ReportResultListener] Connecting to RabbitMQ at {Host}:{Port}",
                _config.RabbitMQConfiguration.HostName,
                _config.RabbitMQConfiguration.Port);

            try
            {
                ConnectAndSubscribe();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "[ReportResultListener] Failed to connect to RabbitMQ — listener will not run.");
                return;
            }

            await Task.Delay(Timeout.Infinite, stoppingToken).ContinueWith(_ => { }, CancellationToken.None);
        }

        public override void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            base.Dispose();
        }

        // ── RabbitMQ wiring ───────────────────────────────────────────────────

        private void ConnectAndSubscribe()
        {
            var factory = new ConnectionFactory
            {
                HostName               = _config.RabbitMQConfiguration.HostName,
                Port                   = _config.RabbitMQConfiguration.Port > 0
                                             ? _config.RabbitMQConfiguration.Port
                                             : AmqpTcpEndpoint.UseDefaultPort,
                UserName               = _config.RabbitMQConfiguration.UserName,
                Password               = _config.RabbitMQConfiguration.Password,
                ClientProvidedName     = _config.RabbitMQConfiguration.ClientProvidedName
                                             ?? "GenReport.Api.ReportResultListener",
                DispatchConsumersAsync = true,
            };

            _connection = factory.CreateConnection();
            _channel    = _connection.CreateModel();

            foreach (var queue in new[] { QueueSuccess, QueueError })
            {
                _channel.QueueDeclare(
                    queue:      queue,
                    durable:    true,
                    exclusive:  false,
                    autoDelete: false,
                    arguments:  null);
            }

            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            Subscribe(QueueSuccess, HandleSuccessAsync);
            Subscribe(QueueError,   HandleErrorAsync);

            _logger.LogInformation(
                "[ReportResultListener] Subscribed to {Success} and {Error} queues.",
                QueueSuccess, QueueError);
        }

        private void Subscribe(string queue, Func<ReportJobResult, Task> handler)
        {
            var consumer = new AsyncEventingBasicConsumer(_channel!);

            consumer.Received += async (_, ea) =>
            {
                string? body = null;
                try
                {
                    body   = Encoding.UTF8.GetString(ea.Body.ToArray());
                    var result = JsonSerializer.Deserialize<ReportJobResult>(body)
                        ?? throw new InvalidOperationException("Deserialized payload was null.");

                    await handler(result);
                    _channel!.BasicAck(ea.DeliveryTag, multiple: false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "[ReportResultListener] Unhandled error processing message from {Queue}. Body: {Body}",
                        queue, body);
                    _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel!.BasicConsume(
                queue:       queue,
                autoAck:     false,
                consumerTag: $"csharp-{queue}",
                consumer:    consumer);
        }

        // ── Success handler ───────────────────────────────────────────────────

        private async Task HandleSuccessAsync(ReportJobResult result)
        {
            _logger.LogInformation(
                "[ReportResultListener] SUCCESS — sessionId={SessionId} sqlitePath={Path}",
                result.SessionId, result.SqliteFilePath);

            if (string.IsNullOrWhiteSpace(result.SqliteFilePath))
            {
                _logger.LogWarning("[ReportResultListener] report_success has no sqliteFilePath — skipping.");
                return;
            }
            if (!File.Exists(result.SqliteFilePath))
            {
                _logger.LogError(
                    "[ReportResultListener] SQLite file not found at {Path}.", result.SqliteFilePath);
                return;
            }
            if (!long.TryParse(result.SessionId, out var sessionIdLong))
            {
                _logger.LogError(
                    "[ReportResultListener] sessionId '{Id}' is not a valid long.", result.SessionId);
                return;
            }

            // ── 1. Look up session (userId, databaseId, title) ────────────────
            SessionInfo? session;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                session = await db.ChatSessions
                    .AsNoTracking()
                    .Where(s => s.Id == sessionIdLong)
                    .Select(s => new SessionInfo(s.UserId, s.DatabaseId, s.Title))
                    .FirstOrDefaultAsync();
            }

            if (session is null)
            {
                _logger.LogError(
                    "[ReportResultListener] Chat session {Id} not found.", result.SessionId);
                return;
            }

            // ── 2. Read SQLite + deliver (R2 or attachment) ───────────────────
            byte[] fileBytes;
            try
            {
                fileBytes = await File.ReadAllBytesAsync(result.SqliteFilePath);
            }
            finally
            {
                TryDeleteTempFile(result.SqliteFilePath);
            }

            var fileName = Path.GetFileName(result.SqliteFilePath);
            var r2Config = _config.R2Configuration;

            GenReport.Infrastructure.Models.Reports.ReportDeliveryResult delivery;
            using (var scope = _scopeFactory.CreateScope())
            {
                var reportService = scope.ServiceProvider.GetRequiredService<ISqliteReportService>();
                delivery = await reportService.ExportAndDeliverAsync(
                    fileBytes, fileName, session.UserId.ToString(), r2Config);
            }

            _logger.LogInformation(
                "[ReportResultListener] Report delivered for session {SessionId}, user {UserId}.",
                result.SessionId, session.UserId);

            // ── 3. Persist DB records ─────────────────────────────────────────
            await PersistReportRecordsAsync(result, session, sessionIdLong, delivery, fileName);
        }

        // ── DB persistence ────────────────────────────────────────────────────

        private async Task PersistReportRecordsAsync(
            ReportJobResult result,
            SessionInfo session,
            long sessionId,
            GenReport.Infrastructure.Models.Reports.ReportDeliveryResult delivery,
            string sqliteFileName)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            // Resolve databaseId: prefer session's linked DB, fall back to the ID from the message.
            long databaseId = session.DatabaseId
                ?? (long.TryParse(result.DatabaseConnectionId, out var mid) ? mid : 0);

            if (databaseId == 0)
            {
                _logger.LogWarning(
                    "[ReportResultListener] Cannot determine databaseId for session {Id} — DB record skipped.",
                    sessionId);
                return;
            }

            // Fetch the last assistant message ID before the transaction (read-only, no need to be inside it).
            var lastMessageId = await db.ChatMessages
                .AsNoTracking()
                .Where(m => m.SessionId == sessionId && m.Role == "assistant")
                .OrderByDescending(m => m.CreatedAt)
                .Select(m => (long?)m.Id)
                .FirstOrDefaultAsync();

            if (!lastMessageId.HasValue)
            {
                _logger.LogWarning(
                    "[ReportResultListener] No assistant message found in session {Id} — MessageReport will not be created.",
                    sessionId);
            }

            // Determine report name before the transaction (also read-only).
            var reportName = await BuildReportNameAsync(db, sessionId, session.Title);

            // Build all entities first so we can add them in a single SaveChangesAsync.
            // EF Core resolves FKs via navigation/shadow properties when entities are
            // added to the same context before saving — no intermediate flushes needed.
            var mimeType      = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
            var excelFileName = Path.GetFileNameWithoutExtension(sqliteFileName) + ".xlsx";
            var now           = DateTime.UtcNow;

            var query = new Query
            {
                Rawtext         = result.Query,
                DatabaseId      = databaseId,
                CreatedById     = session.UserId,
                InvolvedColumns = [],
                InvolvedTables  = [],
                Comments        = [],
                CreatedAt       = now,
                UpdatedAt       = now,
            };

            var mediaFile = new MediaFile(
                storageUrl: delivery.R2Url,
                fileName:   excelFileName,
                mimeType:   mimeType,
                size:       delivery.ExcelSizeBytes)
            {
                CreatedAt = now,
                UpdatedAt = now,
            };

            var report = new Report
            {
                Name          = reportName,
                Query         = query,      // navigation property — EF resolves QueryId automatically
                MediaFile     = mediaFile,  // navigation property — EF resolves MediaFileId automatically
                NoOfRows      = delivery.NoOfRows,
                NoOfColumns   = delivery.NoOfColumns,
                TimeInSeconds = 0,
                CreatedAt     = now,
                UpdatedAt     = now,
            };

            // Wrap all writes in a single transaction via the provider's execution strategy
            // (handles transient failures / Npgsql retry policy automatically).
            await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync();

                db.Queries.Add(query);
                db.MediaFiles.Add(mediaFile);
                db.Reports.Add(report);

                if (lastMessageId.HasValue)
                {
                    db.MessageReports.Add(new MessageReport
                    {
                        MessageId = lastMessageId.Value,
                        Report    = report,   // EF resolves ReportId automatically
                        CreatedAt = now,
                        UpdatedAt = now,
                    });
                }

                await db.SaveChangesAsync(); // single flush — all or nothing
                await tx.CommitAsync();
            });

            _logger.LogInformation(
                "[ReportResultListener] Persisted: Query={QId} MediaFile={MId} Report={RId} Name={Name}",
                query.Id, mediaFile.Id, report.Id, reportName);
        }


        // ── Report naming ─────────────────────────────────────────────────────

        private static async Task<string> BuildReportNameAsync(
            ApplicationDbContext db, long sessionId, string? sessionTitle)
        {
            var baseName = string.IsNullOrWhiteSpace(sessionTitle)
                ? "Report"
                : sessionTitle.Trim();

            // Count existing reports already linked to this session.
            var existingCount = await db.MessageReports
                .AsNoTracking()
                .Where(mr => mr.Message.SessionId == sessionId)
                .CountAsync();

            // First report → "Session Title", subsequent → "Session Title 2", "Session Title 3" …
            return existingCount == 0 ? baseName : $"{baseName} {existingCount + 1}";
        }

        // ── Error handler ─────────────────────────────────────────────────────

        private Task HandleErrorAsync(ReportJobResult result)
        {
            _logger.LogError(
                "[ReportResultListener] FAILURE — sessionId={SessionId} databaseConnectionId={DbId} error={Error}",
                result.SessionId,
                result.DatabaseConnectionId,
                result.Error);

            return Task.CompletedTask;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void TryDeleteTempFile(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "[ReportResultListener] Could not delete temp SQLite file {Path}.", path);
            }
        }

        private sealed record SessionInfo(long UserId, long? DatabaseId, string? Title);
    }
}
