namespace Bookify.Api.Controllers.Apartments
{
    using Bookify.Application.Apartments.CreateApartment;
    using Bookify.Application.Apartments.SearchApartments;
    using Bookify.Application.Apartments.UpdateApartment;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Apartments;
    using MediatR;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    [Authorize]
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

        [HttpPut("{id:guid}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> UpdateApartment(
            Guid id,
            UpdateApartmentRequest request,
            CancellationToken cancellationToken)
        {
            UpdateApartmentCommand command = new(
                id,
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

            Result result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                if (result.Error == ApartmentErrors.NotFound)
                {
                    return NotFound(result.Error);
                }

                if (result.Error == ApartmentErrors.Conflict)
                {
                    return Conflict(result.Error);
                }

                return BadRequest(result.Error);
            }

            return NoContent();
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
