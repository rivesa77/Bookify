namespace Bookify.Domain.Tests.Abstractions
{
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users.Events;
    using FluentAssertions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Domain")]
    public sealed class EntityTests
    {
        private static readonly Guid Id = Guid.Parse("20000000-0000-0000-0000-000000000001");

        private readonly TestEntity entity = new(Id);

        private readonly UserCreatedDomainEvent firstEvent = new(Id);

        [TestMethod]
        public void Constructor_Should_SetIdentifierWithoutEvents()
        {
            // Arrange
            Guid expected = Id;

            // Act
            Guid actual = entity.Id;

            // Assert
            actual.Should().Be(expected);

            entity.GetDomainEvents().Should().BeEmpty();
        }

        [TestMethod]
        public void DefaultConstructor_Should_AllowIdentifierInitialization()
        {
            // Arrange
            Guid expected = Id;

            // Act
            TestEntity defaultEntity = new();

            TestEntity initialized = new() { Id = expected };

            // Assert
            defaultEntity.Id.Should().BeEmpty();

            initialized.Id.Should().Be(expected);

            initialized.GetDomainEvents().Should().BeEmpty();
        }

        [TestMethod]
        public void RaiseDomainEvent_Should_PreserveOrderAndDuplicates()
        {
            // Arrange
            UserCreatedDomainEvent secondEvent = new(Guid.NewGuid());

            // Act
            entity.Raise(firstEvent);

            entity.Raise(secondEvent);

            entity.Raise(firstEvent);

            // Assert
            entity.GetDomainEvents().Should().Equal(
                firstEvent,
                secondEvent,
                firstEvent);
        }

        [TestMethod]
        public void GetDomainEvents_Should_ReturnIndependentSnapshot()
        {
            // Arrange
            entity.Raise(firstEvent);

            IReadOnlyList<IDomainEvent> snapshot = entity.GetDomainEvents();

            // Act
            entity.Raise(new UserCreatedDomainEvent(Guid.NewGuid()));

            entity.ClearDomainEvent();

            // Assert
            snapshot.Should().ContainSingle().Which.Should().Be(firstEvent);

            entity.GetDomainEvents().Should().BeEmpty();
        }

        [TestMethod]
        public void ClearDomainEvent_Should_AllowReuseAndRepeatedClearing()
        {
            // Arrange
            entity.Raise(firstEvent);

            // Act
            entity.ClearDomainEvent();

            entity.ClearDomainEvent();

            entity.Raise(firstEvent);

            // Assert
            entity.GetDomainEvents().Should().ContainSingle().Which.Should().Be(firstEvent);
        }

        private sealed class TestEntity : Entity
        {
            internal TestEntity()
            {
            }

            internal TestEntity(Guid id) : base(id)
            {
            }

            internal void Raise(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
        }
    }
}
