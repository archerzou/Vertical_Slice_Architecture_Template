using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Web.Api.Authentication;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;
using Web.Api.Notifications;

namespace Web.Api.Features.Users;

public static class Register
{
    public sealed record Command(string Email, string FirstName, string LastName, string Password)
        : ICommand<Guid>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.FirstName).NotEmpty();
            RuleFor(c => c.LastName).NotEmpty();
            RuleFor(c => c.Email).NotEmpty().EmailAddress();
            RuleFor(c => c.Password).NotEmpty().MinimumLength(8);
        }
    }

    internal sealed class Handler(ApplicationDbContext context, IPasswordHasher passwordHasher)
        : ICommandHandler<Command, Guid>
    {
        public async Task<Result<Guid>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (await context.Users.AnyAsync(u => u.Email == command.Email, cancellationToken))
            {
                return Result.Failure<Guid>(UserErrors.EmailNotUnique);
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                Email = command.Email,
                FirstName = command.FirstName,
                LastName = command.LastName,
                PasswordHash = passwordHasher.Hash(command.Password)
            };

            user.Raise(new UserRegisteredDomainEvent(user.Id, user.Email));

            context.Users.Add(user);

            await context.SaveChangesAsync(cancellationToken);

            return user.Id;
        }
    }

    internal sealed class UserRegisteredDomainEventHandler(IEmailSender emailSender)
        : IDomainEventHandler<UserRegisteredDomainEvent>
    {
        public async Task Handle(UserRegisteredDomainEvent domainEvent, CancellationToken cancellationToken)
        {
            await emailSender.SendAsync(
                domainEvent.Email,
                "Welcome!",
                "Thanks for registering. We're glad to have you on board.",
                cancellationToken);
        }
    }

    public sealed class Endpoint : IEndpoint
    {
        public sealed record Request(string Email, string FirstName, string LastName, string Password);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("users/register", async (
                Request request,
                ICommandHandler<Command, Guid> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(
                    request.Email,
                    request.FirstName,
                    request.LastName,
                    request.Password);

                Result<Guid> result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .RequireRateLimiting(RateLimitingPolicies.Authentication);
        }
    }
}
