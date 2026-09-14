namespace Bookify.TestUtilities.Context
{
    using Bookify.Domain.Apartments;
    using Microsoft.Extensions.DependencyInjection;
    using Moq;

    public sealed class ApartmentTestContext : ContextTestsBase
    {
        public Mock<IApartmentRepository> Apartments { get; } = new(MockBehavior.Strict);

        public ApartmentTestContext()
        {
            RegisterContext(registrations =>
            {
                registrations.AddSingleton(Apartments.Object);
            });
        }
    }
}