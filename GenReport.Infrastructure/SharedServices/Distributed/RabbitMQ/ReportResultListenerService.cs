using System.Text;
using System.Text.Json;
using GenReport.Domain.DBContext;
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
    ///       Reads the SQLite file from the local path supplied by the Go worker,
    ///       generates Excel/PDF, and emails the results to the requesting user.
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
        // ── Queue names — must match the Go worker constants ──────────────────
        private const string QueueSuccess = "report_success";
        private const string QueueError   = "report_error";

        private readonly IApplicationConfiguration  _config;
        private readonly IServiceScopeFactory       _scopeFactory;
        private readonly ILogger<ReportResultListenerService> _logger;

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
            // Give ASP.NET Core time to finish startup before we start blocking.
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);

            _logger.LogInformation("[ReportResultListener] Connecting to RabbitMQ at {Host}:{Port}",
                _config.RabbitMQConfiguration.HostName,
                _config.RabbitMQConfiguration.Port);

            try
            {
                ConnectAndSubscribe();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ReportResultListener] Failed to connect to RabbitMQ — hosted service will not consume messages.");
                return;
            }

            // Keep the service alive until the host shuts down.
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
                HostName          = _config.RabbitMQConfiguration.HostName,
                Port              = _config.RabbitMQConfiguration.Port > 0
                                        ? _config.RabbitMQConfiguration.Port
                                        : AmqpTcpEndpoint.UseDefaultPort,
                UserName          = _config.RabbitMQConfiguration.UserName,
                Password          = _config.RabbitMQConfiguration.Password,
                ClientProvidedName = _config.RabbitMQConfiguration.ClientProvidedName ?? "GenReport.Api.ReportResultListener",
                DispatchConsumersAsync = true,
            };

            _connection = factory.CreateConnection();
            _channel    = _connection.CreateModel();

            // Declare both queues idempotently (durable = true, matches Go worker)
            foreach (var queue in new[] { QueueSuccess, QueueError })
            {
                _channel.QueueDeclare(
                    queue:      queue,
                    durable:    true,
                    exclusive:  false,
                    autoDelete: false,
                    arguments:  null);
            }

            // Process one message at a time per queue to avoid overwhelming the DB.
            _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

            Subscribe(QueueSuccess, HandleSuccessAsync);
            Subscribe(QueueError,   HandleErrorAsync);

            _logger.LogInformation("[ReportResultListener] Subscribed to {Success} and {Error} queues.",
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
                    body = Encoding.UTF8.GetString(ea.Body.ToArray());
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

                    // Nack without requeue — bad messages should not loop forever.
                    _channel!.BasicNack(ea.DeliveryTag, multiple: false, requeue: false);
                }
            };

            _channel!.BasicConsume(
                queue:       queue,
                autoAck:     false,
                consumerTag: $"csharp-{queue}",
                consumer:    consumer);
        }

        // ── Handlers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Handles a successful report job:
        /// reads the SQLite file from disk, generates Excel/PDF, and emails the user.
        /// </summary>
        private async Task HandleSuccessAsync(ReportJobResult result)
        {
            _logger.LogInformation(
                "[ReportResultListener] SUCCESS — sessionId={SessionId} sqlitePath={Path}",
                result.SessionId, result.SqliteFilePath);

            if (string.IsNullOrWhiteSpace(result.SqliteFilePath))
            {
                _logger.LogWarning("[ReportResultListener] report_success message has no sqliteFilePath — skipping.");
                return;
            }

            if (!File.Exists(result.SqliteFilePath))
            {
                _logger.LogError(
                    "[ReportResultListener] SQLite file not found at {Path} — the Go worker may have cleaned it up.",
                    result.SqliteFilePath);
                return;
            }

            // Resolve the requesting user via the chat session so we know who to email.
            string userId;
            using (var scope = _scopeFactory.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                if (!long.TryParse(result.SessionId, out var sessionIdLong))
                {
                    _logger.LogError("[ReportResultListener] sessionId '{Id}' is not a valid long — cannot look up user.", result.SessionId);
                    return;
                }

                var session = await db.ChatSessions
                    .AsNoTracking()
                    .Where(s => s.Id == sessionIdLong)
                    .Select(s => new { s.UserId })
                    .FirstOrDefaultAsync();

                if (session is null)
                {
                    _logger.LogError("[ReportResultListener] Chat session {Id} not found — cannot determine user to email.", result.SessionId);
                    return;
                }

                userId = session.UserId.ToString();
            }

            // Read the SQLite file bytes then delete it — Go left it as a temp file.
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

            using var scope2 = _scopeFactory.CreateScope();
            var reportService = scope2.ServiceProvider.GetRequiredService<ISqliteReportService>();

            await reportService.ExportAndEmailAsync(fileBytes, fileName, userId);

            _logger.LogInformation(
                "[ReportResultListener] Report emailed for session {SessionId}, user {UserId}.",
                result.SessionId, userId);
        }

        /// <summary>Handles a failed report job by logging the error.</summary>
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
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ReportResultListener] Could not delete temp SQLite file {Path}.", path);
            }
        }
    }
}
