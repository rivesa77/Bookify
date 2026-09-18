namespace Bookify.Api.Tests.Support
{
    using Bookify.Api.Controllers.Apartments;
    using Bookify.Api.Controllers.Users;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using Bookify.TestUtilities.Constants;

    internal static class ApiTestData
    {
        internal static readonly Guid Id = Guid.Parse("10000000-0000-0000-0000-000000000001");

        internal static readonly Guid UserId = Guid.Parse("20000000-0000-0000-0000-000000000001");

        internal static readonly DateOnly StartDate = new(2026, 10, 1);

        internal static readonly Error Failure = new("Test.Failure", "Test failure");

        internal static CreateApartmentRequest ApartmentRequest() => new(
            ApartmentConstants.Name,
            ApartmentConstants.Description,
            ApartmentConstants.Country,
            ApartmentConstants.State,
            ApartmentConstants.ZipCode,
            ApartmentConstants.City,
            ApartmentConstants.Street,
            ApartmentConstants.PriceAmount,
            ApartmentConstants.CleaningFeeAmount,
            ApartmentConstants.Currency,
            [Amenity.Wifi, Amenity.Parking]);

        internal static CreateUserRequest UserRequest() => new(
            "Ana",
            "Garcia",
            "ana@example.com",
            "TestOnly1!");
    }
}