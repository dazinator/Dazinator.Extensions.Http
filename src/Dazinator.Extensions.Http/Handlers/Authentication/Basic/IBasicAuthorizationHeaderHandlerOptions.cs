namespace Dazinator.Extensions.Http.Handlers.Authentication.Basic
{
    public interface IBasicAuthorizationHeaderHandlerOptions : IAuthorizationHeaderHandlerOptions
    {
        string Username { get; }
        string GetPassword();
    }
}
