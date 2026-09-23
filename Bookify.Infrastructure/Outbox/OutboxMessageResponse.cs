namespace Bookify.Infrastructure.Outbox
{
    using System;

    internal sealed record OutboxMessageResponse(Guid Id, string Content);
}