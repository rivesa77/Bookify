namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Migrations;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class MigrationTests
    {
        private readonly ApplicationDbContext context = InfrastructureTestData.CreateContext();

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        [DataRow("0", "20260909102321_Initial_Database")]
        [DataRow("20260909102321_Initial_Database", "20260916085204_Add_User_IdentityId")]
        [DataRow("20260916085204_Add_User_IdentityId", "20260916173041_Add_UserRole")]
        [DataRow("20260916173041_Add_UserRole", "20260917064152_Change_TableName_And_Field")]
        [DataRow("20260917064152_Change_TableName_And_Field", "20260917104130_Add_Permission_Tables")]
        [DataRow("20260917104130_Add_Permission_Tables", "20260923111807_Add_OutBoxMessages")]
        public void Migration_Should_GenerateForwardAndRollbackSql(string previous, string current)
        {
            // Arrange
            IMigrator migrator = context.GetService<IMigrator>();

            // Act
            string forward = migrator.GenerateScript(previous, current);

            string rollback = migrator.GenerateScript(current, previous);

            // Assert
            forward.Should().Contain("__EFMigrationsHistory").And.Contain(current);

            rollback.Should().Contain("DELETE FROM").And.Contain(current);

            forward.Should().NotBe(rollback);
        }

        [TestMethod]
        public void Migrations_Should_MatchCurrentModelAndIncludePermissionAndOutboxSchema()
        {
            // Arrange
            IMigrator migrator = context.GetService<IMigrator>();

            // Act
            string script = migrator.GenerateScript();

            bool pending = context.Database.HasPendingModelChanges();

            // Assert
            script.Should().Contain("CREATE TABLE permissions").And.Contain("CREATE TABLE role_permissions");

            script.Should().Contain("CREATE TABLE outbox_messages");

            pending.Should().BeFalse();
        }
    }
}
