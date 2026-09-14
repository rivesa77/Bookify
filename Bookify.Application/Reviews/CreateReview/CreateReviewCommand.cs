namespace Bookify.Application.Reviews.CreateReview
{
    using Bookify.Application.Abstractions.Messaging;

    public sealed record CreateReviewCommand(
        Guid BookingId,
        int Rating,
        string Comment) : ICommand<Guid>;
}