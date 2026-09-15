namespace Bookify.Api.Controllers.Apartments
{
    using Bookify.Application.Apartments.CreateApartment;
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

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateApartment(
            CreateApartmentRequest request,
            CancellationToken cancellationToken)
        {
            CreateApartmentCommand command = new(
                request.Name,
                request.Description,
                request.Country,
                request.State,
                request.ZipCode,
                request.City,
                request.Street,
                request.PriceAmount,
                request.CleaningFeeAmount,
                request.Currency,
                request.Amenities);

            Result<Guid> result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return BadRequest(result.Error);
            }

            return StatusCode(StatusCodes.Status201Created, result.Value);
        }

        [HttpGet]
        public async Task<IActionResult> SearchApartments(
            DateOnly startDate,
            DateOnly endDate,
            CancellationToken cancellationToken)
        {
            SearchApartmentsQuery query = new(startDate, endDate);

            Result<IReadOnlyList<ApartmentResponse>> result = await sender.Send(query, cancellationToken);

            return Ok(result.Value);
        }
    }
}