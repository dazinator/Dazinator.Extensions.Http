namespace Dazinator.Extensions.Http.Handlers.Authentication.Basic
{
    using System.Threading.Tasks;
    using Dazinator.Extensions.Http;

    public static class BasicAuthHttpClientHandlerRegistryExtensions
    {
#pragma warning disable IDE1006 // Naming Styles
        public static string Name = "BasicAuth";
#pragma warning restore IDE1006 // Naming Styles

        public static HttpClientHandlerRegistry RegisterBasicAuthHandler(this HttpClientHandlerRegistry registry)
        {
            return registry.RegisterBasicAuthHandler<BasicAuthorizationHeaderHandlerOptions>();
        }

        public static HttpClientHandlerRegistry RegisterBasicAuthHandler<TOptions>(this HttpClientHandlerRegistry registry)
            where TOptions : class, IBasicAuthorizationHeaderHandlerOptions
        {
            return registry.RegisterAuthorizationHeaderHandler<TOptions>(Name, (sp, clientName, attemptCount, opts, ct) =>
            {
                var creds = BasicAuthenticationUtils.GetAuthenticationHeaderValue(opts.Username, opts.GetPassword());
                return Task.FromResult(creds);
            });
        }
    }
}
