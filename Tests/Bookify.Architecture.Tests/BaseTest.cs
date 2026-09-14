namespace Bookify.ArchitectureTest
{
    using System.Reflection;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Bookify.Infrastructure;

    public class BaseTest
    {
        protected static Assembly DomainAssembly => typeof(Entity).Assembly;

        protected static Assembly ApplicationAssembly => typeof(IBaseCommand).Assembly;

        protected static Assembly InfrastructureAssembly => typeof(ApplicationDbContext).Assembly;
    }
}