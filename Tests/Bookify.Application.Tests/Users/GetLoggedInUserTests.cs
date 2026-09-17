namespace Bookify.Application.Tests.Users
{
    using Bookify.Application.Tests.Support;
    using Bookify.Application.Users.GetLoggedInUser;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Moq;

    [TestClass]
    [TestCategory("Application")]
    public sealed class GetLoggedInUserTests
    {
        private const string FirstName = "Ana";

        private const string LastName = "Garcia";

        private const string Email = "ana@example.com";

        private const string IdentityId = "external-identity";

        private readonly Bookify.Application.Tests.Support.ApplicationTestContext context = new();

        private readonly SqlQueryStub sql = UserTable();

        private readonly GetLoggedInUserQuery query = new();

        [TestInitialize]
        public void Initialize()
        {
            context.Sql.Setup(f => f.CreateConnection()).Returns(sql.Connection.Object);

            context.UserContext.SetupGet(u => u.IdentityId).Returns(IdentityId);
        }

        [TestCleanup]
        public void Cleanup()
        {
            context.Dispose();

            sql.Dispose();
        }

        [TestMethod]
        public async Task Send_Should_QueryExternalIdentityAndMapProfile()
        {
            // Arrange

            UserResponse user = new()
            {
                Id = Guid.NewGuid(),
                FirstName = FirstName,
                LastName = LastName,
                Email = Email
            };

            sql.AddRow(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email);

            // Act

            Domain.Abstractions.Result<UserResponse> result = await context.Sender.Send(query);

            // Assert

            result.Value.Should().BeEquivalentTo(user);

            sql.Parameter("IdentityId").Should().Be(IdentityId);

            sql.Sql.Should().Contain("WHERE identity_id = @IdentityId");

            context.UserContext.VerifyGet(u => u.UserId, Times.Never);

            sql.Disposed.Should().BeTrue();
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public async Task Send_NonUniqueProfile_Should_PropagateQuerySingleFailure(int rows)
        {
            // Arrange

            for (int i = 0; i < rows; i++)
            {
                sql.AddRow(
                    Guid.NewGuid(),
                    FirstName,
                    LastName,
                    Email);
            }

            // Act

            Func<Task> act = () => context.Sender.Send(query);

            // Assert

            await act.Should().ThrowAsync<InvalidOperationException>();

            sql.Disposed.Should().BeTrue();
        }

        private static SqlQueryStub UserTable() => new(
            ("Id", typeof(Guid)),
            ("FirstName", typeof(string)),
            ("LastName", typeof(string)),
            ("Email", typeof(string)));
    }
}