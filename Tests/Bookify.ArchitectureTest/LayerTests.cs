namespace Bookify.ArchitectureTest
{
    using FluentAssertions;
    using NetArchTest.Rules;

    [TestClass]
    [TestCategory("Architecture")]
    public class LayerTests : BaseTest
    {
        [TestMethod]
        public void DomainLayer_Should_NotHaveDependencyOn_ApplicationLayer()
        {
            // Arrange
            Types domainAssemblies = Types.InAssembly(DomainAssembly);

            // Act
            TestResult result = domainAssemblies
                .Should()
                .NotHaveDependencyOn(ApplicationAssembly.GetName().Name)
                .GetResult();

            // Assert
            result
                .IsSuccessful
                .Should()
                .BeTrue();
        }

        [TestMethod]
        public void DomainLayer_Should_NotHaveDependencyOn_InfrastructureLayer()
        {
            // Arrange
            Types domainAssemblies = Types.InAssembly(DomainAssembly);

            // Act
            TestResult result = domainAssemblies
                .Should()
                .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
                .GetResult();

            // Assert
            result
                .IsSuccessful
                .Should()
                .BeTrue();
        }

        [TestMethod]
        public void ApplicationLayer_Should_NotHaveDependencyOn_InfrastructureLayer()
        {
            // Arrange
            Types domainAssemblies = Types.InAssembly(ApplicationAssembly);

            // Act
            TestResult result = domainAssemblies
                .Should()
                .NotHaveDependencyOn(InfrastructureAssembly.GetName().Name)
                .GetResult();

            // Assert
            result
                .IsSuccessful
                .Should()
                .BeTrue();
        }
    }
}