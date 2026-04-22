namespace WolverineOutboxDemo.Api.Models
{
    public class MessageHistory
    {
        public Guid Id { get; set; }
        public Guid CorrelationId { get; set; }
        public string MessageType { get; set; } = string.Empty;
        public string Direction { get; set; } = string.Empty; // "Sent", "Received", "Completed"
        public string Description { get; set; } = string.Empty;
        public DateTimeOffset Timestamp { get; set; }
    }
}
