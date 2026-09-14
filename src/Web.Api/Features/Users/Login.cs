using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Web.Api.Authentication;
using Web.Api.Common;
using Web.Api.Common.Endpoints;
using Web.Api.Common.Extensions;
using Web.Api.Common.Messaging;
using Web.Api.Database;

namespace Web.Api.Features.Users;

public static class Login
{
    public sealed record Command(string Email, string Password) : ICommand<AccessTokensResponse>;

    public sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(c => c.Email).NotEmpty().EmailAddress();
            RuleFor(c => c.Password).NotEmpty();
        }
    }

    internal sealed class Handler(
        ApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ITokenProvider tokenProvider,
        IDateTimeProvider dateTimeProvider) : ICommandHandler<Command, AccessTokensResponse>
    {
        public async Task<Result<AccessTokensResponse>> Handle(Command command, CancellationToken cancellationToken)
        {
            User? user = await context.Users
                .AsNoTracking()
                .SingleOrDefaultAsync(u => u.Email == command.Email, cancellationToken);

            if (user is null)
            {
                return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
            }

            bool verified = passwordHasher.Verify(command.Password, user.PasswordHash);

            if (!verified)
            {
                return Result.Failure<AccessTokensResponse>(UserErrors.NotFoundByEmail);
            }

            string accessToken = tokenProvider.Create(user);
            string refreshToken = tokenProvider.GenerateRefreshToken();

            var refreshTokenEntity = new RefreshToken
            {
                Id = Guid.NewGuid(),
                Token = refreshToken,
                UserId = user.Id,
                ExpiresOnUtc = dateTimeProvider.UtcNow.AddDays(RefreshTokenExpirationInDays)
            };

            context.RefreshTokens.Add(refreshTokenEntity);

            await context.SaveChangesAsync(cancellationToken);

            return new AccessTokensResponse(accessToken, refreshToken);
        }

        private const int RefreshTokenExpirationInDays = 7;
    }

    public sealed class Endpoint : IEndpoint
    {
        public sealed record Request(string Email, string Password);

        public void MapEndpoint(IEndpointRouteBuilder app)
        {
            app.MapPost("users/login", async (
                Request request,
                ICommandHandler<Command, AccessTokensResponse> handler,
                CancellationToken cancellationToken) =>
            {
                var command = new Command(request.Email, request.Password);

                Result<AccessTokensResponse> result = await handler.Handle(command, cancellationToken);

                return result.Match(Results.Ok, CustomResults.Problem);
            })
            .WithTags(Tags.Users)
            .RequireRateLimiting(RateLimitingPolicies.Authentication);
        }
    }
}
