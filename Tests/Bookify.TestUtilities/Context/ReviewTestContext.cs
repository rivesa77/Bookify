namespace Bookify.TestUtilities.Context
{
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    public sealed class ReviewTestContext : ContextTestsBase
    {
        private static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        public Mock<IBookingRepository> Bookings { get; } = new(MockBehavior.Strict);
        public Mock<IReviewRepository> Reviews { get; } = new(MockBehavior.Strict);

        public ReviewTestContext()
        {
            Mock<IDateTimeProvider> clock = new();
            clock.SetupGet(c => c.UtcNow).Returns(UtcNow);

            RegisterContext(registrations =>
            {
                registrations.AddSingleton(Bookings.Object);
                registrations.AddSingleton(Reviews.Object);
                registrations.AddSingleton(clock.Object);
            });
        }
    }
}