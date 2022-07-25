using System;
using System.Threading;
using System.Threading.Tasks;
using Dazinator.Extensions.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dazinator.Extensions.Http.Handlers.Authentication
{
    public static class AuthorizationHeaderHttpClientHandlerRegistryExtensions
    {
        public static HttpClientHandlerRegistry RegisterAuthorizationHeaderHandler<TOptions>(this HttpClientHandlerRegistry registry,
            string handlerName,
             Func<IServiceProvider, string, int, TOptions, CancellationToken, Task<string>> getAuthHeaderCredential)
            where TOptions : class, IAuthorizationHeaderHandlerOptions
        {
            registry.RegisterHandler<AuthorizationHeaderHttpMessageHandler<TOptions>>(handlerName, (r) =>
            {
                r.Factory = (sp, clientName) =>
                {
                    var optionsMonitor = sp.GetRequiredService<IOptionsMonitor<TOptions>>();
                    var logger = sp.GetRequiredService<ILogger<AuthorizationHeaderHttpMessageHandler<TOptions>>>();
                    return new AuthorizationHeaderHttpMessageHandler<TOptions>(logger, optionsMonitor, clientName,
                        (clientName, attemptCount, ct, opts) => getAuthHeaderCredential?.Invoke(sp, clientName, attemptCount, opts, ct));
                };
            });

            return registry;
        }
    }
}
