namespace Bookify.Application.Abstractions.Messaging
{
    using Bookify.Domain.Abstractions;
    using MediatR;

    public interface ICommand : IRequest<Result>, IBaseCommand
    {
    }

    public interface ICommand<TResponse> : IRequest<Result<TResponse>>, IBaseCommand
    {
    }
}