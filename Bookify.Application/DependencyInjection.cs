namespace Bookify.Application
{
    using Bookify.Application.Abstractions.Behaviors;
    using Bookify.Domain.Bookings;
    using Microsoft.Extensions.DependencyInjection;

    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly);
                configuration.AddOpenBehavior(typeof(LoggingBehavior<,>));
            });

            services.AddTransient<PricingServices>();

            return services;
        }
    }
}