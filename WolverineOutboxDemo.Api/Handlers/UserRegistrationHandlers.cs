using WolverineOutboxDemo.Api.Data;
using WolverineOutboxDemo.Api.Models;
using WolverineOutboxDemo.Contracts;

namespace WolverineOutboxDemo.Api.Handlers
{
    public class UserRegistrationHandlers
    {
        public async Task<UserRegistered> Handle(
            RegisterUser command,
            AppDbContext dbContext,
            ILogger<UserRegistrationHandlers> logger
            )
        {
            logger.LogInformation("Processing user registration for {Email}", command.Email);

            var user = new User
            {
                Id = command.UserId,
                Email = command.Email,
                Name = command.Name,
                Status = "Registered",
                CreatedAt = DateTimeOffset.UtcNow
            };

            dbContext.Users.Add(user);

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = command.UserId,
                MessageType = nameof(RegisterUser),
                Direction = "Received",
                Description = $"RegisterUser received for {command.Email}",
                Timestamp = DateTimeOffset.UtcNow
            });

            logger.LogInformation("User {UserId} created in database", command.UserId);

            return new UserRegistered(command.UserId, command.Email, DateTimeOffset.UtcNow);
        }

        public async Task<SendWelcomeEmail> Handle(
            UserRegistered @event,
            AppDbContext dbContext,
            ILogger<UserRegistrationHandlers> logger
            )
        {
            logger.LogInformation("User registered event received for {Email}", @event.Email);

            var user = await dbContext.Users.FindAsync(@event.UserId);
            if (user != null)
                user.Status = "WelcomeEmailPending";

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = @event.UserId,
                MessageType = nameof(UserRegistered),
                Direction = "Received",
                Description = $"UserRegistered event → cascade SendWelcomeEmail",
                Timestamp = DateTimeOffset.UtcNow
            });

            return new SendWelcomeEmail(@event.UserId, @event.Email, "New User");
        }

        public async Task Handle(
            WelcomeEmailSent @event,
            AppDbContext dbContext,
            ILogger<UserRegistrationHandlers> logger
            )
        {
            logger.LogInformation("Completing registration for user {UserId}", @event.UserId);

            var user = await dbContext.Users.FindAsync(@event.UserId);
            if (user != null)
            {
                user.Status = "WelcomeEmailSent";
                user.CompletedAt = @event.SentAt;
            }

            dbContext.MessageHistories.Add(new MessageHistory
            {
                Id = Guid.NewGuid(),
                CorrelationId = @event.UserId,
                MessageType = nameof(WelcomeEmailSent),
                Direction = "Completed",
                Description = $"WelcomeEmailSent → registration flow completed",
                Timestamp = DateTimeOffset.UtcNow
            });

            logger.LogInformation("User {UserId} registration completed", @event.UserId);
        }
    }
}
