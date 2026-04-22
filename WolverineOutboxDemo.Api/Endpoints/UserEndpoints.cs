using Microsoft.EntityFrameworkCore;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using WolverineOutboxDemo.Api.Data;
using WolverineOutboxDemo.Api.Models;
using WolverineOutboxDemo.Contracts;

namespace WolverineOutboxDemo.Api.Endpoints
{
    public static class UserEndpoints
    {
        public static void MapUserEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/users/register-outbox", async (
                RegisterUserRequest request,
                AppDbContext dbContext,
                IDbContextOutbox<AppDbContext> outbox,
                CancellationToken cancellationToken) =>
            {
                var userId = Guid.NewGuid();

                // 创建用户
                var user = new Models.User
                {
                    Id = userId,
                    Email = request.Email,
                    Name = request.Name,
                    Status = "Pending",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                dbContext.Users.Add(user);

                dbContext.MessageHistories.Add(new MessageHistory
                {
                    Id = Guid.NewGuid(),
                    CorrelationId = userId,
                    MessageType = nameof(RegisterUser),
                    Direction = "Sent",
                    Description = $"RegisterUser sent via Outbox for {request.Email}",
                    Timestamp = DateTimeOffset.UtcNow
                });

                await outbox.SaveChangesAndFlushMessagesAsync(cancellationToken);

                return Results.Ok(new { UserId = userId, Message = "User registration initiated with Outbox pattern" });
            });

            app.MapPost("/api/users/register-unsafe", async (
                RegisterUserRequest request,
                AppDbContext dbContext,
                IMessageBus messageBus,
                CancellationToken cancellationToken) =>
            {
                var userId = Guid.NewGuid();

                var user = new Models.User
                {
                    Id = userId,
                    Email = request.Email,
                    Name = request.Name,
                    Status = "Pending",
                    CreatedAt = DateTimeOffset.UtcNow
                };

                dbContext.Users.Add(user);

                dbContext.MessageHistories.Add(new MessageHistory
                {
                    Id = Guid.NewGuid(),
                    CorrelationId = userId,
                    MessageType = nameof(RegisterUser),
                    Direction = "Sent",
                    Description = $"RegisterUser sent (unsafe/dual-write) for {request.Email}",
                    Timestamp = DateTimeOffset.UtcNow
                });

                await dbContext.SaveChangesAsync(cancellationToken);

                // 风险：如果这里失败，消息不会发送
                // 或者消息发送成功但后续操作失败
                await messageBus.SendAsync(new RegisterUser(userId, request.Email, request.Name));

                return Results.Ok(new { UserId = userId, Message = "User registration initiated (unsafe - possible dual write issue)" });
            })
            .WithName("RegisterUserUnsafe");

            app.MapGet("/api/users/{userId:guid}", async (
                Guid userId,
                AppDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var user = await dbContext.Users.FindAsync([userId], cancellationToken);

                return user is null
                    ? Results.NotFound()
                    : Results.Ok(new { 
                        user.Id, 
                        user.Email, 
                        user.Name, 
                        user.Status, 
                        user.CreatedAt, 
                        user.CompletedAt });
            })
            .WithName("GetUser");

            // 列出所有用户
            app.MapGet("/api/users", async (
                AppDbContext dbContext,
                CancellationToken cancellationToken) =>
            {
                var users = await dbContext.Users
                    .OrderByDescending(u => u.CreatedAt)
                    .Select(u => new UserDto(
                        u.Id,
                        u.Email,
                        u.Name,
                        u.Status,
                        u.CreatedAt,
                        u.CompletedAt))
                    .ToListAsync(cancellationToken);

                return Results.Ok(users);
            })
            .WithName("ListUsers");

            app.MapPost("/api/users/{userId:guid}/retry", async (
                Guid userId,
                IMessageBus messageBus,
                CancellationToken cancellationToken) =>
            {
                // 模拟重试机制
                await messageBus.SendAsync(new SendWelcomeEmail(userId, "test@example.com", "Test User"));
                return Results.Ok(new { Message = "Retry triggered" });
            })
        .WithName("RetryUserProcessing");

            app.MapGet("/api/outbox", async (AppDbContext dbContext, CancellationToken ct) =>
            {
                var messages = await dbContext.Database.SqlQueryRaw<EnvelopeInfo>(
                    "SELECT id, destination, message_type AS MessageType, COALESCE(attempts, 0) AS Attempts, deliver_by AS DeliverBy FROM wolverine.wolverine_outgoing_envelopes ORDER BY id")
                    .ToListAsync(ct);
                return Results.Ok(messages);
            });

            app.MapGet("/api/inbox", async (AppDbContext dbContext, CancellationToken ct) =>
            {
                var messages = await dbContext.Database.SqlQueryRaw<EnvelopeInfo>(
                    "SELECT id, received_at AS Destination, message_type AS MessageType, COALESCE(attempts, 0) AS Attempts, execution_time AS DeliverBy FROM wolverine.wolverine_incoming_envelopes ORDER BY id")
                    .ToListAsync(ct);
                return Results.Ok(messages);
            });

            app.MapGet("/api/message-history", async (AppDbContext dbContext, CancellationToken ct) =>
            {
                var history = await dbContext.MessageHistories
                    .OrderByDescending(h => h.Timestamp)
                    .Select(h => new MessageHistoryDto(
                        h.Id,
                        h.CorrelationId,
                        h.MessageType,
                        h.Direction,
                        h.Description,
                        h.Timestamp))
                    .ToListAsync(ct);
                return Results.Ok(history);
            })
            .WithName("GetMessageHistory");
        }
    }

    public record UserDto(
        Guid Id,
        string Email,
        string Name,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? CompletedAt);

    public record RegisterUserRequest(string Email, string Name);

    public record EnvelopeInfo(Guid Id, string Destination, string MessageType, int Attempts, DateTimeOffset? DeliverBy);

    public record MessageHistoryDto(
        Guid Id,
        Guid CorrelationId,
        string MessageType,
        string Direction,
        string Description,
        DateTimeOffset Timestamp);
}
