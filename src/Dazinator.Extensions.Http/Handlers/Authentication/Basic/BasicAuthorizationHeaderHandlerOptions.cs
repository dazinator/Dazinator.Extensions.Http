namespace Dazinator.Extensions.Http.Handlers.Authentication.Basic
{
    public class BasicAuthorizationHeaderHandlerOptions : AuthorizationHeaderHandlerOptions, IBasicAuthorizationHeaderHandlerOptions
    {
        public BasicAuthorizationHeaderHandlerOptions()
        {
            SchemeName = "Basic";
        }
        public string Username { get; set; }
        public string Password { get; set; }
        public virtual string GetPassword() => Password;
    }
}
