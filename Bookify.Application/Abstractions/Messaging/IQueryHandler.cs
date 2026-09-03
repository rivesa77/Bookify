namespace Bookify.Application.Abstractions.Messaging
{
    using Bookify.Domain.Abstractions;
    using MediatR;

    public interface IQueryHandler<TQuery, TResponse> : IRequestHandler<TQuery, Result<TResponse>>
        where TQuery : IQuery<TResponse>
    {
    }
}