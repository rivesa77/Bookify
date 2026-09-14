namespace Bookify.Application.Tests.Reviews.CreateReview
{
    using System;
    using Bookify.Application.Abstractions.DateTimeProvider;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Reviews;
    using MediatR;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    internal sealed class ReviewTestContext : IDisposable
    {
        private static readonly DateTime UtcNow = new(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);

        public Mock<IBookingRepository> Bookings { get; } = new(MockBehavior.Strict);
        public Mock<IReviewRepository> Reviews { get; } = new(MockBehavior.Strict);
        public Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Strict);
        private readonly ServiceProvider services;
        public ISender Sender => services.GetRequiredService<ISender>();

        public ReviewTestContext()
        {
            Mock<IDateTimeProvider> clock = new();
            clock.SetupGet(c => c.UtcNow).Returns(UtcNow);
            ServiceCollection registrations = new();
            registrations.AddLogging();
            registrations.AddApplication();
            registrations.AddSingleton(Bookings.Object);
            registrations.AddSingleton(Reviews.Object);
            registrations.AddSingleton(UnitOfWork.Object);
            registrations.AddSingleton(clock.Object);
            services = registrations.BuildServiceProvider();
        }

        public void Dispose() => services.Dispose();
    }
}