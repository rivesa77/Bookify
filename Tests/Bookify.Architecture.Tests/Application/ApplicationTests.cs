namespace Bookify.ArchitectureTest.Application
{
    using Bookify.Application.Abstractions.Messaging;
    using FluentAssertions;
    using FluentValidation;
    using NetArchTest.Rules;

    [TestClass]
    [TestCategory("Architecture")]
    public class ApplicationTests : BaseTest
    {
        [TestMethod]
        public void CommandHandlerWithReflection_Should_HaveNameEndingWith_CommandHandler()
        {
            var invalidHandlers = ApplicationAssembly
                .GetTypes()
                .Where(type => type.IsClass && !type.IsAbstract)
                .Where(type => type.GetInterfaces().Any(@interface =>
                    @interface.IsGenericType &&
                    (
                        @interface.GetGenericTypeDefinition() == typeof(ICommandHandler<>) ||
                        @interface.GetGenericTypeDefinition() == typeof(ICommandHandler<,>)
                    )))
                .Where(type => !type.Name.Split('`')[0]
                    .EndsWith("CommandHandler", StringComparison.Ordinal))
                .Select(type => type.FullName)
                .ToArray();

            invalidHandlers.Should().BeEmpty();
        }

        [TestMethod]
        public void CommandHandler_Should_HaveNameEndingWith_CommandHandler()
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .That()
                .ImplementInterface(typeof(ICommandHandler<>))
                .Or()
                .ImplementInterface(typeof(ICommandHandler<,>))
                .Should().HaveNameEndingWith("CommandHandler")
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }

        [TestMethod]
        public void QueryHandler_Should_HaveNameEndingWith_QueryHandler()
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .That()
                .ImplementInterface(typeof(IQueryHandler<,>))
                .Should()
                .HaveNameEndingWith("QueryHandler")
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }

        [TestMethod]
        public void Validator_Should_HaveNameEndingWith_Validator()
        {
            var result = Types.InAssembly(ApplicationAssembly)
                .That()
                .Inherit(typeof(AbstractValidator<>))
                .Should()
                .HaveNameEndingWith("Validator")
                .GetResult();

            result.IsSuccessful.Should().BeTrue();
        }
    }
}