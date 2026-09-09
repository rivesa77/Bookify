namespace Bookify.Api.Extensions
{
    using System.Data;
    using Bogus;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Domain.Apartments;
    using Dapper;

    public static class SeedDataExtensions
    {
        public static void SeedData(this IApplicationBuilder app)
        {
            using IServiceScope scope = app.ApplicationServices.CreateScope();

            ISqlConnectionFactory sqlConnectionFactory = scope.ServiceProvider.GetRequiredService<ISqlConnectionFactory>();

            using IDbConnection connection = sqlConnectionFactory.CreateConnection();

            using IDbTransaction transaction = connection.BeginTransaction();

            // Serialize startup seeding so concurrent instances cannot both insert.
            connection.Execute(
                """LOCK TABLE public."Apartments" IN SHARE ROW EXCLUSIVE MODE""",
                transaction: transaction);

            bool hasApartments = connection.ExecuteScalar<bool>(
                """SELECT EXISTS (SELECT 1 FROM public."Apartments")""",
                transaction: transaction);

            if (hasApartments)
            {
                transaction.Commit();
                return;
            }

            Faker faker = new();

            List<Object> apartments = [];

            for (int i = 0; i < 100; i++)
            {
                apartments.Add(new
                {
                    Id = Guid.NewGuid(),
                    Name = faker.Company.CompanyName().PadRight(Name.ExactLength, '.')[..Name.ExactLength],
                    Description = "Fake description",
                    Country = faker.Address.Country(),
                    State = faker.Address.State(),
                    ZipCode = faker.Address.ZipCode(),
                    City = faker.Address.City(),
                    Street = faker.Address.StreetAddress(),
                    PriceAmount = faker.Random.Decimal(50, 1000),
                    PriceCurrency = "EUR",
                    CleaningFeeAmount = faker.Random.Decimal(25, 200),
                    CleaningFeeCurrency = "EUR",
                    Amenities = new List<int> { (int)Amenity.Parking, (int)Amenity.MountainView },
                    LastBookedOn = (DateTime?)null
                });
            }

            const string sql = """
                INSERT INTO public."Apartments" (
                    id,
                    "name",
                    description,
                    address_country,
                    address_state,
                    address_zip_code,
                    address_city,
                    address_street,
                    price_amount,
                    price_currency,
                    cleaning_fee_amount_amount,
                    cleaning_fee_amount_currency,
                    amenities,
                    last_booked_on_utc)
                VALUES(
                    @Id,
                    @Name,
                    @Description,
                    @Country,
                    @State,
                    @ZipCode,
                    @City,
                    @Street,
                    @PriceAmount,
                    @PriceCurrency,
                    @CleaningFeeAmount,
                    @CleaningFeeCurrency,
                    @Amenities,
                    @LastBookedOn);
            """;

            connection.Execute(sql, apartments, transaction: transaction);
            transaction.Commit();
        }
    }
}