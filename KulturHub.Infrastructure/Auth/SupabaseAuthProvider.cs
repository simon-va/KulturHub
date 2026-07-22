using ErrorOr;
using KulturHub.Application.Errors;
using KulturHub.Application.Ports;

namespace KulturHub.Infrastructure.Auth;

public class SupabaseAuthProvider(Supabase.Client supabaseClient) : IAuthProvider
{
    public async Task<ErrorOr<AuthProviderSession>> SignUpAsync(string email, string password)
    {
        Supabase.Gotrue.Session? session;
        try
        {
            session = await supabaseClient.Auth.SignUp(email, password);
        }
        catch (Exception ex) when (ex.Message.Contains("already registered", StringComparison.OrdinalIgnoreCase))
        {
            return AuthErrors.AlreadyRegistered;
        }

        if (session?.User?.Id is null || session.AccessToken is null || session.RefreshToken is null)
            return AuthErrors.SignUpFailed;

        return new AuthProviderSession(session.AccessToken, session.RefreshToken, Guid.Parse(session.User.Id));
    }
}
