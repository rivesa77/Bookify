namespace Bookify.Domain.Tests.Users
{
    using Bookify.Domain.Users;
    using Bookify.Domain.Users.Events;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class UserTests
    {
        private static readonly FirstName FirstName = new("Ana");

        private static readonly LastName LastName = new("Garcia");

        private static readonly Email Email = new("ana@example.com");

        [TestMethod]
        public void Create_Should_SetPropertiesRegisteredRoleAndEvent()
        {
            // Arrange
            Role expectedRole = Role.Registered;

            // Act
            User user = CreateUser();

            // Assert
            user.Id.Should().NotBeEmpty();

            user.FirstName.Should().Be(FirstName);

            user.LastName.Should().Be(LastName);

            user.Email.Should().Be(Email);

            user.IdentityId.Should().BeEmpty();

            user.Roles.Should().ContainSingle().Which.Should().BeSameAs(expectedRole);

            user.GetDomainEvents().Should().ContainSingle().Which.Should().Be(new UserCreatedDomainEvent(user.Id));
        }

        [TestMethod]
        public void Create_Should_GenerateIndependentIdentifiersAndRoleCollections()
        {
            // Arrange
            User first = CreateUser();

            // Act
            User second = CreateUser();

            // Assert
            first.Id.Should().NotBe(second.Id);

            first.Roles.Should().NotBeSameAs(second.Roles);

            first.Roles.Should().Equal(second.Roles);
        }

        [TestMethod]
        [DataRow("external-id")]
        [DataRow("")]
        public void SetIdentityId_Should_ReplaceIdentityWithoutRaisingEvent(string identityId)
        {
            // Arrange
            User user = CreateUser();

            user.SetIdentityId("previous-id");

            user.ClearDomainEvent();

            // Act
            user.SetIdentityId(identityId);

            // Assert
            user.IdentityId.Should().Be(identityId);

            user.Roles.Should().ContainSingle().Which.Should().BeSameAs(Role.Registered);

            user.GetDomainEvents().Should().BeEmpty();
        }

        private static User CreateUser() => User.Create(
            FirstName,
            LastName,
            Email);
    }
}
