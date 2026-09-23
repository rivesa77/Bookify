namespace Bookify.Infrastructure.Outbox
{
    public sealed class OutboxMessage
    {
        public Guid Id { get; private set; }

        public DateTime OccurredOnUtc { get; private set; }

        // Name of Domain Event.
        public string Type { get; private set; }

        // Serialize Domain Event.
        public string Content { get; private set; }

        public DateTime? ProcessedOnUtc { get; private set; }

        public string? Error { get; private set; }

        public OutboxMessage(
            Guid id,
            DateTime occurredOnUtc,
            string type,
            string content)
        {
            Id = id;
            OccurredOnUtc = occurredOnUtc;
            Type = type;
            Content = content;
        }
    }
}
