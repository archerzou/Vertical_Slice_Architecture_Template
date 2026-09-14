using Web.Api.Features.Users;

namespace Web.Api.Authentication;

public interface ITokenProvider
{
    string Create(User user);

    string GenerateRefreshToken();
}
