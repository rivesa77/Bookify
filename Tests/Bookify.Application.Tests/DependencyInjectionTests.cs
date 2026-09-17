namespace Bookify.Application.Tests
{
    using Bookify.Application.Abstractions.Behaviors;
    using Bookify.Application.Bookings.GetBooking;
    using Bookify.Application.Tests.Support;
    using Bookify.Application.Users.CreateUser;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using FluentAssertions;
    using FluentValidation;
    using MediatR;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Application")]
    public sealed class DependencyInjectionTests
    {
        [TestMethod]
        public void AddApplication_Should_ResolveEveryHandlerValidatorAndPricingService()
        {
            // Arrange
            using ApplicationTestContext context = new();

            ServiceCollection services = new();

            services.AddLogging();

            services.AddSingleton(context.Users.Object);

            services.AddSingleton(context.Apartments.Object);

            services.AddSingleton(context.Bookings.Object);

            services.AddSingleton(context.Reviews.Object);

            services.AddSingleton(context.Authentication.Object);

            services.AddSingleton(context.Jwt.Object);

            services.AddSingleton(context.Sql.Object);

            services.AddSingleton(context.UserContext.Object);

            services.AddSingleton(context.Clock.Object);

            services.AddSingleton(context.Email.Object);

            services.AddSingleton(context.UnitOfWork.Object);

            // Act

            IServiceCollection returned = services.AddApplication();

            using ServiceProvider provider = services.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,

                ValidateScopes = true
            });

            using IServiceScope scope = provider.CreateScope();

            IServiceProvider scopedProvider = scope.ServiceProvider;

            Type[] contracts = typeof(DependencyInjection).Assembly.GetTypes()
                .Where(t => t.IsClass && !t.IsAbstract && !t.ContainsGenericParameters)
                .SelectMany(t => t.GetInterfaces())
                .Where(t => t.IsGenericType && (t.GetGenericTypeDefinition() == typeof(IRequestHandler<,>)
            || t.GetGenericTypeDefinition() == typeof(INotificationHandler<>)
            || t.GetGenericTypeDefinition() == typeof(IValidator<>)))
                .Distinct().ToArray();

            // Assert

            returned.Should().BeSameAs(services);

            contracts.Should().NotBeEmpty();

            foreach (Type? contract in contracts)
            {
                scopedProvider.GetRequiredService(contract).Should().NotBeNull($"{contract.Name} must be registered");
            }

            scopedProvider.GetRequiredService<PricingServices>().Should().NotBeNull();

            scopedProvider.GetRequiredService<ISender>().Should().NotBeNull();

            scopedProvider.GetRequiredService<IPublisher>().Should().NotBeNull();

            scopedProvider.GetServices<IPipelineBehavior<CreateUserCommand, Result<Guid>>>().Select(b => b.GetType())
                .Should().Equal(typeof(LoggingBehavior<CreateUserCommand, Result<Guid>>), typeof(ValidationBehavior<CreateUserCommand, Result<Guid>>));

            scopedProvider.GetServices<IPipelineBehavior<GetBookingQuery, Result<BookingResponse>>>().Should().BeEmpty();
        }
    }
}