namespace Bookify.Api.Tests.Extensions
{
    using System.Data;
    using Bookify.Api.Extensions;
    using Bookify.Api.Tests.Support;
    using Bookify.Domain.Apartments;
    using FluentAssertions;
    using Microsoft.AspNetCore.Builder;
    using Moq;

    [TestClass]
    [TestCategory("Extensions")]
    public sealed class SeedDataExtensionsTests
    {
        private readonly SeedTestContext context = new();

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        public void SeedData_ExistingApartments_Should_NotInsertAndCommitTransaction()
        {
            // Arrange
            context.HasApartments = true;

            ApplicationBuilder app = context.CreateApp();

            // Act
            app.SeedData();

            // Assert
            context.Operations.Should().HaveCount(2);

            AssertLockAndExistenceOrder();

            context.Inserts.Should().BeEmpty();

            context.Transaction.Verify(t => t.Commit(), Times.Once);

            AssertResourcesDisposed();
        }

        [TestMethod]
        public void SeedData_EmptyTable_Should_InsertOneHundredValidRowsInSameTransaction()
        {
            // Arrange
            ApplicationBuilder app = context.CreateApp();

            // Act
            app.SeedData();

            // Assert
            AssertLockAndExistenceOrder();

            context.Inserts.Should().HaveCount(100);

            context.Inserts.Select(row => (Guid)row["Id"]!).Should().OnlyHaveUniqueItems().And.NotContain(Guid.Empty);

            foreach (Dictionary<string, object?> row in context.Inserts)
            {
                ((string)row["Name"]!).Should().HaveLength(Name.ExactLength);

                ((decimal)row["PriceAmount"]!).Should().BeInRange(50m, 1000m);

                ((decimal)row["CleaningFeeAmount"]!).Should().BeInRange(25m, 200m);

                row["PriceCurrency"].Should().Be("EUR");

                row["CleaningFeeCurrency"].Should().Be("EUR");

                row["LastBookedOn"].Should().Be(DBNull.Value);
            }

            context.CommandTransactions.Should().OnlyContain(transaction => ReferenceEquals(transaction, context.Transaction.Object));

            context.Transaction.Verify(t => t.Commit(), Times.Once);

            AssertResourcesDisposed();
        }

        [TestMethod]
        public void SeedData_FailedInsert_Should_NotCommitAndShouldDisposeResources()
        {
            // Arrange
            context.FailOnInsert = true;

            ApplicationBuilder app = context.CreateApp();

            // Act
            Action act = () => app.SeedData();

            // Assert
            act.Should().Throw<InvalidOperationException>().WithMessage("Insert failed");

            context.Transaction.Verify(t => t.Commit(), Times.Never);

            AssertResourcesDisposed();
        }

        [TestMethod]
        public void SeedData_SecondStartupWithExistingRows_Should_NotInsertAgain()
        {
            // Arrange
            ApplicationBuilder app = context.CreateApp();

            app.SeedData();

            context.HasApartments = true;

            // Act
            app.SeedData();

            // Assert
            context.Inserts.Should().HaveCount(100);

            context.Transaction.Verify(t => t.Commit(), Times.Exactly(2));
        }

        private void AssertLockAndExistenceOrder()
        {
            context.Operations[0].Should().Contain("LOCK TABLE public.apartments IN SHARE ROW EXCLUSIVE MODE");

            context.Operations[1].Should().Contain("SELECT EXISTS");
        }

        private void AssertResourcesDisposed()
        {
            context.Connection.Verify(c => c.Dispose(), Times.Once);

            context.Transaction.Verify(t => t.Dispose(), Times.Once);

            context.Factory.Verify(f => f.CreateConnection(), Times.Once);
        }
    }
}
