namespace Bookify.Api.Controllers.Apartment
{
    using Bookify.Application.Apartments.SearchApartments;
    using Bookify.Domain.Abstractions;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/apartments")]
    public class ApartmentsController : ControllerBase
    {
        private readonly ISender sender;

        public ApartmentsController(ISender sender)
        {
            this.sender = sender;
        }

        [HttpGet]
        public async Task<IActionResult> SearchApartments(
            DateOnly starDate,
            DateOnly endDate,
            CancellationToken cancellationToken)
        {
            SearchApartmentsQuery query = new(starDate, endDate);

            Result<IReadOnlyList<ApartmentResponse>> result = await sender.Send(query, cancellationToken);

            return Ok(result.Value);
        }
    }
}