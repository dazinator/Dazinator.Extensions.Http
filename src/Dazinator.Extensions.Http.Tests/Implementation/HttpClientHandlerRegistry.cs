namespace Dazinator.Extensions.Http.Tests.Implementation
{
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;


    // This should be registered as a singleton, and all the handlers registered with a name.
    public class HttpClientHandlerRegistry
    {
        protected Dictionary<string, HttpClientHandlerRegistration> RegisteredHandlers { get; set; } = new Dictionary<string, HttpClientHandlerRegistration>();


        /// <summary>
        /// Register handler with custom factory. Use this to control how the instance of the handler is created, and create different instances based on the 
        /// named http client being configured.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="name"></param>
        /// <param name="factory"></param>
        public HttpClientHandlerRegistry RegisterHandler<THandler>(string handlerName, Action<HttpClientHandlerRegistration> configure)
            where THandler : DelegatingHandler
        {
            var registration = new HttpClientHandlerRegistration();
            configure(registration);
            RegisteredHandlers.Add(handlerName, registration);
            return this;
        }

        public DelegatingHandler? GetHandlerInstance(string handlerName, IServiceProvider serviceProvider, string httpClientName)
        {
            var reg = RegisteredHandlers[handlerName];

            //  var factory = reg.Factory ?? (sp, name)=>serviceProvider.GetRequiredService<>;
            if (reg.Factory == null)
            {
                throw new InvalidOperationException("Factory null");
            }
            return reg.Factory?.Invoke(serviceProvider, httpClientName);
        }

        public HttpClientHandlerRegistration GetHandlerRegistration(string handlerName) => RegisteredHandlers[handlerName];

        public void ConfigureHandlerDefaults(IServiceCollection services)
        {
            foreach (var item in RegisteredHandlers)
            {
                item.Value.OnConfigure?.Invoke(services);
            }
        }

        public void ConfigureHandlerForNamedClient(string handlerName, string clientName)
        {
            var handler = RegisteredHandlers[handlerName];
            handler.OnConfigureNamedClient?.Invoke(clientName);
        }
    }
}
