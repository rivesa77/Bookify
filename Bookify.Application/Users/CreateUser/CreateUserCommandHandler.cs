namespace Bookify.Application.Users.CreateUser
{
    using System;
    using Bookify.Application.Abstractions.Authentication;
    using Bookify.Application.Abstractions.Messaging;
    using Bookify.Domain.Abstractions;
    using Bookify.Domain.Users;

    internal sealed class CreateApartmentCommandHandler : ICommandHandler<CreateUserCommand, Guid>
    {
        private readonly IAuthenticationService authenticationService;

        private readonly IUserRepository userRepository;
        private readonly IUnitOfWork unitOfWork;

        public CreateApartmentCommandHandler(
            IUserRepository userRepository,
            IUnitOfWork unitOfWork,
            IAuthenticationService authenticationService)
        {
            this.userRepository = userRepository;
            this.unitOfWork = unitOfWork;
            this.authenticationService = authenticationService;
        }

        public async Task<Result<Guid>> Handle(CreateUserCommand request, CancellationToken cancellationToken)
        {
            User user = User.Create(
                new FirstName(request.FirtsName),
                new LastName(request.LastName),
                new Email(request.Email));

            string identity = await authenticationService.RegisterAsync(
                user,
                request.Password,
                cancellationToken);

            user.SetIdentityId(identity);

            userRepository.Add(user);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return user.Id;
        }
    }
}