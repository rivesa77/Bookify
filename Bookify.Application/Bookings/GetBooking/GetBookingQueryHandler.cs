namespace Bookify.Application.Bookings.GetBooking
{
    using System.Data;
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Application.Abstractions.Data;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using Dapper;

    internal sealed class GetBookingQueryHandler : IQueryHandler<GetBookingQuery, BookingResponse>
    {
        private readonly ISqlConnectionFactory sqlConnectionFactory;
        private readonly IUserContext userContext;

        public GetBookingQueryHandler(ISqlConnectionFactory sqlConnectionFactory, IUserContext userContext)
        {
            this.sqlConnectionFactory = sqlConnectionFactory;
            this.userContext = userContext;
        }

        public async Task<Result<BookingResponse>> Handle(GetBookingQuery request, CancellationToken cancellationToken)
        {
            using IDbConnection connection = sqlConnectionFactory.CreateConnection();

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
                    amenities_up_change_amount AS AmenitiesUpChargeAmount,
                    amenities_up_change_currency AS AmenitiesUpChargeCurrency,
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
                new
                {
                    request.BookingId,
                });

            if (booking is null || booking.UserId != userContext.UserId)
            {
                return Result.Failure<BookingResponse>(BookingErrors.NotFound);
            }

            return booking;
        }
    }
}