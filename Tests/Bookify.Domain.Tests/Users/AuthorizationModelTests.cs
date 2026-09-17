namespace Bookify.Domain.Tests.Users
{
    using Bookify.Domain.Tests.Support;
    using Bookify.Domain.Users;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class AuthorizationModelTests
    {
        private const int RoleId = 20;

        private const int PermissionId = 30;

        private const string RoleName = "Reviewer";

        private const string PermissionName = "reviews:read";

        [TestMethod]
        public void PredefinedRoleAndPermission_Should_HaveStableIdentifiersAndNames()
        {
            // Arrange
            const int expectedId = 1;

            // Act
            Role role = Role.Registered;

            Permission permission = Permission.UserRead;

            // Assert
            role.Id.Should().Be(expectedId);

            role.Name.Should().Be("Registered");

            permission.Id.Should().Be(expectedId);

            permission.Name.Should().Be("users:read");
        }

        [TestMethod]
        public void Constructors_Should_PreserveDataAndCreateEmptyCollections()
        {
            // Arrange
            int expectedRoleId = RoleId;

            int expectedPermissionId = PermissionId;

            // Act
            Role role = CreateRole();

            Permission permission = CreatePermission();

            // Assert
            role.Id.Should().Be(expectedRoleId);

            role.Name.Should().Be(RoleName);

            role.Users.Should().BeEmpty();

            role.Permissions.Should().BeEmpty();

            permission.Id.Should().Be(expectedPermissionId);

            permission.Name.Should().Be(PermissionName);
        }

        [TestMethod]
        public void RoleCollections_Should_BeIndependentPerInstance()
        {
            // Arrange
            Role first = CreateRole();

            Role second = CreateRole();

            User user = DomainTestData.CreateUser();

            Permission permission = CreatePermission();

            // Act
            first.Users.Add(user);

            first.Permissions.Add(permission);

            // Assert
            first.Users.Should().ContainSingle().Which.Should().BeSameAs(user);

            first.Permissions.Should().ContainSingle().Which.Should().BeSameAs(permission);

            second.Users.Should().BeEmpty();

            second.Permissions.Should().BeEmpty();
        }

        [TestMethod]
        public void RolePermission_Should_LinkRoleAndPermissionIdentifiers()
        {
            // Arrange
            Role role = CreateRole();

            Permission permission = CreatePermission();

            // Act
            RolePermission link = new()
            {
                RoleId = role.Id,

                PermissionId = permission.Id
            };

            // Assert
            link.RoleId.Should().Be(RoleId);

            link.PermissionId.Should().Be(PermissionId);
        }

        private static Role CreateRole() => new(RoleId, RoleName);

        private static Permission CreatePermission() => new(PermissionId, PermissionName);
    }
}
