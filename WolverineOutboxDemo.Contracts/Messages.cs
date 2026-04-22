namespace WolverineOutboxDemo.Contracts
{
    public record RegisterUser(Guid UserId, string Email, string Name);
    public record UserRegistered(Guid UserId, string Email, DateTimeOffset RegisteredAt);
    public record SendWelcomeEmail(Guid UserId, string Email, string Name);
    public record WelcomeEmailSent(Guid UserId, DateTimeOffset SentAt);
    public record UserRegistrationCompleted(Guid UserId, DateTimeOffset CompletedAt);
}
