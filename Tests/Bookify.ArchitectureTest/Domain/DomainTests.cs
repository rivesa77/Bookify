namespace Bookify.ArchitectureTest.Domain
{
    using Bookify.Domain.Abstractions;
    using FluentAssertions;
    using NetArchTest.Rules;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Architecture")]
    public class DomainTests : BaseTest
    {
        [TestMethod]
        public void DomainEvent_Should_Have_DomainEventPostfix()
        {
            var result = Types.InAssembly(DomainAssembly)
                .That()
                .ImplementInterface(typeof(IDomainEvent))
                .Should().HaveNameEndingWith("DomainEvent")
                .GetResult();

            result.IsSuccessful
                .Should()
                .BeTrue();
        }
    }
}
