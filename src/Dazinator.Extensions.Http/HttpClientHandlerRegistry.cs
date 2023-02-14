namespace Dazinator.Extensions.Http
{
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;


    // This should be registered as a singleton, and all the handlers registered with a name.
    public class HttpClientHandlerRegistry
    {
        internal Dictionary<string, HttpClientHandlerRegistration> RegisteredHandlers { get; set; } = new Dictionary<string, HttpClientHandlerRegistration>();

        internal DelegatingHandler? GetHandlerInstance(string handlerName, IServiceProvider serviceProvider, string httpClientName)
        {
            var reg = RegisteredHandlers[handlerName];
            //reg.
            //  var factory = reg.Factory ?? (sp, name)=>serviceProvider.GetRequiredService<>;

            if (reg.Factory == null)
            {
                // return serviceProvider.GetRequiredService(typeof(reg.))
                throw new InvalidOperationException("Handler is registered in registry without a factory method set.");
            }
            return reg.Factory?.Invoke(serviceProvider, httpClientName);
        }

        //public HttpClientHandlerRegistration GetHandlerRegistration(string handlerName) => RegisteredHandlers[handlerName];

    }
}
