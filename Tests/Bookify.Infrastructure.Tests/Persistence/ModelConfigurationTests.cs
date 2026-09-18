namespace Bookify.Infrastructure.Tests.Persistence
{
    using Bookify.Domain.Apartments;
    using Bookify.Domain.Bookings;
    using Bookify.Domain.Commons;
    using Bookify.Domain.Reviews;
    using Bookify.Domain.Users;
    using Bookify.Infrastructure.Tests.Support;
    using FluentAssertions;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Infrastructure;
    using Microsoft.EntityFrameworkCore.Metadata;
    using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    [TestCategory("Infrastructure")]
    public sealed class ModelConfigurationTests
    {
        private readonly ApplicationDbContext context = InfrastructureTestData.CreateContext();

        [TestCleanup]
        public void Cleanup() => context.Dispose();

        [TestMethod]
        [DataRow(typeof(Apartment), "apartments")]
        [DataRow(typeof(Booking), "bookings")]
        [DataRow(typeof(Review), "reviews")]
        [DataRow(typeof(User), "users")]
        [DataRow(typeof(Role), "roles")]
        [DataRow(typeof(Permission), "permissions")]
        [DataRow(typeof(RolePermission), "role_permissions")]
        public void Configuration_Should_UseExpectedTableAndPrimaryKey(Type type, string table)
        {
            // Arrange
            IEntityType entity = Entity(type);

            // Act
            string? actual = entity.GetTableName();

            // Assert
            actual.Should().Be(table);

            entity.FindPrimaryKey().Should().NotBeNull();
        }

        [TestMethod]
        [DataRow(typeof(Apartment), nameof(Apartment.Name), 200)]
        [DataRow(typeof(Apartment), nameof(Apartment.Description), 2000)]
        [DataRow(typeof(User), nameof(User.FirstName), 200)]
        [DataRow(typeof(User), nameof(User.LastName), 200)]
        [DataRow(typeof(User), nameof(User.Email), 400)]
        [DataRow(typeof(Review), nameof(Review.Comment), 200)]
        public void TextProperties_Should_HaveLengthsAndRoundTripConverters(
            Type type,
            string propertyName,
            int length)
        {
            // Arrange
            IProperty property = Entity(type).FindProperty(propertyName)!;

            ValueConverter converter = property.GetValueConverter()!;

            const string text = "Example";

            // Act
            object? valueObject = converter.ConvertFromProvider(text);

            object? stored = converter.ConvertToProvider(valueObject);

            // Assert
            property.GetMaxLength().Should().Be(length);

            stored.Should().Be(text);
        }

        [TestMethod]
        [DataRow(typeof(Apartment), nameof(Apartment.Price))]
        [DataRow(typeof(Apartment), nameof(Apartment.CleaningFeeAmount))]
        [DataRow(typeof(Booking), nameof(Booking.PriceForPeriod))]
        [DataRow(typeof(Booking), nameof(Booking.CleaningFee))]
        [DataRow(typeof(Booking), nameof(Booking.AmenitiesUpChange))]
        [DataRow(typeof(Booking), nameof(Booking.TotalPrice))]
        public void MoneyProperties_Should_BeOwnedWithCurrencyConversion(Type owner, string navigation)
        {
            // Arrange
            IEntityType owned = Entity(owner).FindNavigation(navigation)!.TargetEntityType;

            ValueConverter converter = owned.FindProperty(nameof(Money.Currency))!.GetValueConverter()!;

            // Act
            object? stored = converter.ConvertToProvider(Currency.Eur);

            object? restored = converter.ConvertFromProvider("USD");

            // Assert
            owned.IsOwned().Should().BeTrue();

            stored.Should().Be("EUR");

            restored.Should().Be(Currency.Usd);
        }

        [TestMethod]
        public void ApartmentVersion_Should_MapPostgresXminConcurrencyToken()
        {
            // Arrange
            IProperty property = Entity(typeof(Apartment)).FindProperty("Version")!;

            // Act
            bool concurrency = property.IsConcurrencyToken;

            // Assert
            concurrency.Should().BeTrue();

            property.ClrType.Should().Be(typeof(uint));

            property.ValueGenerated.Should().Be(ValueGenerated.OnAddOrUpdate);

            property.GetColumnName().Should().Be("xmin");
        }

        [TestMethod]
        [DataRow(nameof(User.Email))]
        [DataRow(nameof(User.IdentityId))]
        public void UserIndexes_Should_BeUnique(string property)
        {
            // Arrange
            IEntityType entity = Entity(typeof(User));

            // Act
            IIndex index = entity.GetIndexes().Single(i => i.Properties.Select(p => p.Name).SequenceEqual(new[] { property }));

            // Assert
            index.IsUnique.Should().BeTrue();
        }

        [TestMethod]
        public void Permissions_Should_UseJoinTableWithoutShadowRoleForeignKey()
        {
            // Arrange
            IEntityType permission = Entity(typeof(Permission));

            IEntityType join = Entity(typeof(RolePermission));

            // Act
            string[] key = join.FindPrimaryKey()!.Properties.Select(p => p.Name).ToArray();

            // Assert
            key.Should().Equal(nameof(RolePermission.RoleId), nameof(RolePermission.PermissionId));

            join.GetForeignKeys().Select(f => f.PrincipalEntityType.ClrType).Should().BeEquivalentTo(new[] { typeof(Role), typeof(Permission) });

            permission.FindProperty(nameof(Permission.Name))!.IsNullable.Should().BeFalse();

            permission.GetProperties().Select(p => p.Name).Should().BeEquivalentTo(nameof(Permission.Id), nameof(Permission.Name));

            Entity(typeof(Role)).FindSkipNavigation(nameof(Role.Permissions))!.JoinEntityType.Should().Be(join);
        }

        [TestMethod]
        public void RelationshipsAndRating_Should_HaveExpectedMappings()
        {
            // Arrange
            IEntityType booking = Entity(typeof(Booking));

            IEntityType review = Entity(typeof(Review));

            ValueConverter converter = review.FindProperty(nameof(Review.Rating))!.GetValueConverter()!;

            // Act
            object? stored = converter.ConvertToProvider(Rating.Create(4).Value);

            object? restored = converter.ConvertFromProvider(5);

            // Assert
            stored.Should().Be(4);

            restored.Should().Be(Rating.Create(5).Value);

            booking.FindNavigation(nameof(Booking.Duration))!.TargetEntityType.IsOwned().Should().BeTrue();

            Entity(typeof(Apartment)).FindNavigation(nameof(Apartment.Address))!.TargetEntityType.IsOwned().Should().BeTrue();

            booking.GetForeignKeys().Select(f => f.PrincipalEntityType.ClrType).Should().BeEquivalentTo(new[] { typeof(Apartment), typeof(User) });

            review.GetForeignKeys().Select(f => f.PrincipalEntityType.ClrType).Should().BeEquivalentTo(new[] { typeof(Apartment), typeof(Booking), typeof(User) });
        }

        [TestMethod]
        public void Seeds_Should_LinkRegisteredRoleToUserReadPermission()
        {
            // Arrange
            IModel model = context.GetService<IDesignTimeModel>().Model;

            // Act
            IDictionary<string, object?> role = model.FindEntityType(typeof(Role))!.GetSeedData().Single();

            IDictionary<string, object?> permission = model.FindEntityType(typeof(Permission))!.GetSeedData().Single();

            IDictionary<string, object?> link = model.FindEntityType(typeof(RolePermission))!.GetSeedData().Single();

            // Assert
            role[nameof(Role.Id)].Should().Be(Role.Registered.Id);

            permission[nameof(Permission.Id)].Should().Be(Permission.UserRead.Id);

            link[nameof(RolePermission.RoleId)].Should().Be(Role.Registered.Id);

            link[nameof(RolePermission.PermissionId)].Should().Be(Permission.UserRead.Id);
        }

        private IEntityType Entity(Type type) => context.Model.FindEntityType(type)!;
    }
}
