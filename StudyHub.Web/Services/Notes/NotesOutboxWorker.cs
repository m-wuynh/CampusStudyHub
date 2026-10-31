using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using StudyHub.DAL.Persistence;
using StudyHub.Web.Hubs;

namespace StudyHub.Web.Services.Notes;

public sealed class NotesOutboxWorker(
    IServiceScopeFactory scopeFactory,
    IHubContext<NotesHub> hub,
    ILogger<NotesOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<StudyHubDbContext>();
                var installed = await db.Database.SqlQueryRaw<int>(
                    "SELECT CASE WHEN OBJECT_ID(N'dbo.NotesOutboxEvents', N'U') IS NULL THEN 0 ELSE 1 END AS [Value]")
                    .SingleAsync(stoppingToken) == 1;
                if (installed)
                {
                    var claimed = new List<ClaimedNotesEvent>();
                    await db.Database.OpenConnectionAsync(stoppingToken);
                    await using (var command = db.Database.GetDbConnection().CreateCommand())
                    {
                        command.CommandText = """
                        ;WITH next_events AS
                        (
                            SELECT TOP (20) * FROM dbo.NotesOutboxEvents WITH (READPAST, UPDLOCK, READCOMMITTEDLOCK, ROWLOCK)
                            WHERE PublishedAtUtc IS NULL
                              AND (LeaseUntilUtc IS NULL OR LeaseUntilUtc < SYSUTCDATETIME())
                            ORDER BY NotesOutboxEventId
                        )
                        UPDATE next_events
                        SET LeaseUntilUtc = DATEADD(second, 30, SYSUTCDATETIME()),
                            AttemptCount = AttemptCount + 1
                        OUTPUT INSERTED.NotesOutboxEventId, INSERTED.StudyGroupId,
                               INSERTED.EventName, INSERTED.PayloadJson;
                        """;
                        await using var reader = await command.ExecuteReaderAsync(stoppingToken);
                        while (await reader.ReadAsync(stoppingToken))
                            claimed.Add(new ClaimedNotesEvent
                            {
                                NotesOutboxEventId = reader.GetInt64(0),
                                StudyGroupId = reader.GetInt64(1),
                                EventName = reader.GetString(2),
                                PayloadJson = reader.GetString(3)
                            });
                    }
                    foreach (var item in claimed)
                    {
                        try
                        {
                            using var payload = JsonDocument.Parse(item.PayloadJson);
                            await NotesEventDelivery.SendToActiveMembersAsync(
                                db, hub, item.StudyGroupId, item.EventName, payload.RootElement, stoppingToken);
                            await db.Database.ExecuteSqlInterpolatedAsync(
                                $"UPDATE dbo.NotesOutboxEvents SET PublishedAtUtc = SYSUTCDATETIME(), LeaseUntilUtc = NULL WHERE NotesOutboxEventId = {item.NotesOutboxEventId} AND PublishedAtUtc IS NULL",
                                stoppingToken);
                        }
                        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                        {
                            logger.LogWarning(exception, "Notes outbox event {EventId} will be retried", item.NotesOutboxEventId);
                        }
                    }
                }
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(exception, "Notes outbox polling failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        }
    }

    public sealed class ClaimedNotesEvent
    {
        public long NotesOutboxEventId { get; set; }
        public long StudyGroupId { get; set; }
        public string EventName { get; set; } = null!;
        public string PayloadJson { get; set; } = null!;
    }
}
