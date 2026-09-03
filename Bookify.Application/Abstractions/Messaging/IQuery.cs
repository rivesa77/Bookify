namespace Bookify.Application.Abstractions.Messaging
{
    using Bookify.Domain.Abstractions;
    using MediatR;

    public interface IQuery<TResponse> : IRequest<Result<TResponse>>
    {
    }
}