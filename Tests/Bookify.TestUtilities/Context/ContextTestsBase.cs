namespace Bookify.TestUtilities.Context
{
    using Bookify.Application;
    using Bookify.Domain.Abstractions;
    using MediatR;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    public abstract class ContextTestsBase : IDisposable
    {
        public Mock<IUnitOfWork> UnitOfWork { get; } = new(MockBehavior.Strict);

        private ServiceProvider? services;
        public ISender Sender => (services ?? throw new InvalidOperationException(
            "RegisterContext must be called before resolving services."))
            .GetRequiredService<ISender>();

        protected ContextTestsBase()
        {
        }

        protected void RegisterContext(Action<IServiceCollection> configureServices)
        {
            ArgumentNullException.ThrowIfNull(configureServices);

            if (services is not null)
            {
                throw new InvalidOperationException("The context has already been registered.");
            }

            ServiceCollection registrations = new();
            registrations.AddLogging();
            registrations.AddApplication();
            registrations.AddSingleton(UnitOfWork.Object);

            configureServices(registrations);

            services = registrations.BuildServiceProvider();
        }

        public void Dispose() => services?.Dispose();
    }
}
