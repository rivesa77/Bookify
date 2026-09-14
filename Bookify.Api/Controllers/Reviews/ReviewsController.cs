namespace Bookify.Api.Controllers.Reviews
{
    using Bookify.Application.Reviews.CreateReview;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Bookings;
    using MediatR;
    using Microsoft.AspNetCore.Mvc;

    [ApiController]
    [Route("api/reviews")]
    public sealed class ReviewsController : ControllerBase
    {
        private readonly ISender sender;

        public ReviewsController(ISender sender)
        {
            this.sender = sender;
        }

        [HttpPost]
        [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CreateReview(CreateReviewRequest request, CancellationToken cancellationToken)
        {
            CreateReviewCommand command = new(request.BookingId, request.Rating, request.Comment);
            Result<Guid> result = await sender.Send(command, cancellationToken);

            if (result.IsFailure)
            {
                return result.Error == BookingErrors.NotFound
                    ? NotFound(result.Error)
                    : BadRequest(result.Error);
            }

            return StatusCode(StatusCodes.Status201Created, result.Value);
        }
    }
}
