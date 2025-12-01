namespace MahjongStats.Services;

public interface IAuthService
{
    bool IsUserAuthorized(string? email);
}

public class AuthService : IAuthService
{
    private readonly IConfiguration _configuration;

    public AuthService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public bool IsUserAuthorized(string? email)
    {
        if (string.IsNullOrEmpty(email))
            return false;

        var allowedEmails = _configuration.GetSection("Auth:AllowedEmails").Get<List<string>>() ?? new();
        return allowedEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
    }
}
