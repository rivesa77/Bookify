namespace Bookify.Application.Bookings.GetBooking
{
    using System.Data;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Dapper;

    internal sealed class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingResponse>
    {
        private readonly ISqlConnectionFactory sqlConnectionFactory;

        public GetBookingQueryHandler(ISqlConnectionFactory sqlConnectionFactory)
        {
            this.sqlConnectionFactory = sqlConnectionFactory;
        }

        public async Task<Result<BookingResponse>> Handle(GetBookingQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection connection = sqlConnectionFactory.CreateConnection();

            BookingResponse bookingResponse = new()
            {
                Id = request.BookingId,
            };

            const string sql = """
                SELECT
                    id AS Id,
                    apartment_id AS ApartmentId,
                    user_id AS UserId,
                    status AS Status,
                    price_for_period_amount AS PriceAmount,
                    price_for_period_currency AS PriceCurrency,
                    cleaning_fee_amount AS CleaningFeeAmount,
                    cleaning_fee_currency AS CleaningFeeCurrency,
                    amenities_up_charge_amount AS AmenitiesUpChargeAmount,
                    amenities_up_charge_currency AS AmenitiesUpChargeCurrency,
                    total_price_amount AS TotalPriceAmount,
                    total_price_currency AS TotalPriceCurrency,
                    duration_start AS DurationStart,
                    duration_end AS DurationEnd,
                    created_on_utc AS CreatedOnUtc
                FROM bookings
                WHERE id = @BookingId
                """;

            BookingResponse? booking = await connection.QueryFirstOrDefaultAsync<BookingResponse>(
                sql,
                bookingResponse);

            return booking;
        }
    }
}