using Wolverine;
using WolverineOutboxDemo.Api.Data;
using WolverineOutboxDemo.Api.Models;
using WolverineOutboxDemo.Contracts;

namespace WolverineOutboxDemo.Api.Sagas
{
    public record RegistrationTimeout(Guid Id) : TimeoutMessage(TimeSpan.FromMinutes(2));

    public class UserRegistrationSaga : Saga
    {
        public Guid Id { get; set; }
        public string Email { get; set; } = "";
        public string Name { get; set; } = "";
        public string Status { get; set; } = "Started";
        public DateTimeOffset StartedAt { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }

        public static (UserRegistrationSaga, UserRegistered, RegistrationTimeout) Start(
            RegisterUser cmd)
        {
            var saga = new UserRegistrationSaga
            {
                Id = cmd.UserId,
                Email = cmd.Email,
                Name = cmd.Name,
                Status = "RegistrationStarted",
                StartedAt = DateTimeOffset.UtcNow
            };

            return (
                saga,
                new UserRegistered(cmd.UserId, cmd.Email, DateTimeOffset.UtcNow),
                new RegistrationTimeout(cmd.UserId)
            );
        }

        public async Task<SendWelcomeEmail> Handle(
            UserRegistered @event,
            AppDbContext dbContext)
        {
            Status = "UserRegistered";

            var user = await dbContext.Users.FindAsync(@event.UserId);
            if (user != null)
                user.Status = "Registered";

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = Id,
                MessageType = nameof(UserRegistered),
                Direction = "Received",
                Description = $"UserRegistered -> cascade SendWelcomeEmail",
                Timestamp = DateTimeOffset.UtcNow
            });

            return new SendWelcomeEmail(@event.UserId, @event.Email, Name);
        }

        public async Task<WelcomeEmailSent> Handle(
            SendWelcomeEmail cmd,
            AppDbContext dbContext)
        {
            Status = "WelcomeEmailPending";

            var user = await dbContext.Users.FindAsync(cmd.UserId);
            if (user != null)
                user.Status = "WelcomeEmailPending";

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = Id,
                MessageType = nameof(SendWelcomeEmail),
                Direction = "Received",
                Description = $"SendWelcomeEmail -> simulate send, cascade WelcomeEmailSent",
                Timestamp = DateTimeOffset.UtcNow
            });

            return new WelcomeEmailSent(Id, DateTimeOffset.UtcNow);
        }

        public async Task Handle(
            WelcomeEmailSent @event,
            AppDbContext dbContext)
        {
            Status = "Completed";
            CompletedAt = @event.SentAt;

            var user = await dbContext.Users.FindAsync(@event.UserId);
            if (user != null)
            {
                user.Status = "WelcomeEmailSent";
                user.CompletedAt = @event.SentAt;
            }

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = Id,
                MessageType = nameof(WelcomeEmailSent),
                Direction = "Completed",
                Description = $"WelcomeEmailSent -> registration flow completed",
                Timestamp = DateTimeOffset.UtcNow
            });

            MarkCompleted();
        }

        public async Task Handle(
            RegistrationTimeout timeout,
            AppDbContext dbContext)
        {
            Status = "TimedOut";
            CompletedAt = DateTimeOffset.UtcNow;

            var user = await dbContext.Users.FindAsync(timeout.Id);
            if (user != null)
                user.Status = "TimedOut";

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = Id,
                MessageType = nameof(RegistrationTimeout),
                Direction = "TimedOut",
                Description = $"RegistrationTimeout -> compensation applied",
                Timestamp = DateTimeOffset.UtcNow
            });

            MarkCompleted();
        }

        public static void NotFound(RegistrationTimeout timeout, ILogger<UserRegistrationSaga> logger)
        {
            logger.LogInformation("Timeout for already-completed saga {Id}", timeout.Id);
        }
    }
}
