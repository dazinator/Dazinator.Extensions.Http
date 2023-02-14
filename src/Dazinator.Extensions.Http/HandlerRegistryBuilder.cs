namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.DependencyInjection;

    public class HandlerRegistryBuilder
    {

        public HandlerRegistryBuilder(IServiceCollection services, HttpClientHandlerRegistry registry)
        {
            Services = services;
            Registry = registry;
        }

        public IServiceCollection Services { get; }
        internal HttpClientHandlerRegistry Registry { get; }

        /// <summary>
        /// Register handler with custom factory. Use this to control how the instance of the handler is created, and create different instances based on the 
        /// named http client being configured.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="name"></param>
        /// <param name="factory"></param>
        public HandlerRegistryBuilder RegisterHandler<THandler>(string handlerName, Action<HttpClientHandlerRegistration> configure)
            where THandler : DelegatingHandler
        {
            var registration = new HttpClientHandlerRegistration(Services);
            // registration.Factory = (sp, httpClientName) => ActivatorUtilities.CreateInstance<THandler>(sp, httpClientName); // sp.GetRequiredService<THandler>();
            configure(registration);
            registration.EnsureIsValid();
            Registry.RegisteredHandlers.Add(handlerName, registration);
            return this;
        }

    }

}