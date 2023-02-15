namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.Configuration;
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
        /// <param name="handlerName"></param>
        /// <param name="configure"></param>
        public HandlerRegistryBuilder RegisterHandler<THandler>(string handlerName, Action<HttpClientHandlerRegistration> configure)
            where THandler : DelegatingHandler
        {
            var registration = new HttpClientHandlerRegistration();
            // registration.Factory = (sp, httpClientName) => ActivatorUtilities.CreateInstance<THandler>(sp, httpClientName); // sp.GetRequiredService<THandler>();
            configure(registration);
            registration.EnsureIsValid();
            Registry.RegisteredHandlers.Add(handlerName, registration);
            return this;
        }

        /// <summary>
        /// Register handler with custom factory. Use this to control how the instance of the handler is created, and create different instances based on the 
        /// named http client being configured.
        /// </summary>
        /// <typeparam name="THandler"></typeparam>
        /// <param name="handlerName"></param>
        /// <param name="configure"></param>
        public HandlerRegistryBuilder RegisterHandler<THandler>(string handlerName, Action<IServiceCollection, HttpClientHandlerRegistration> configure)
            where THandler : DelegatingHandler
        {
            var registration = new HttpClientHandlerRegistration();
            // registration.Factory = (sp, httpClientName) => ActivatorUtilities.CreateInstance<THandler>(sp, httpClientName); // sp.GetRequiredService<THandler>();
            configure(Services, registration);
            registration.EnsureIsValid();
            Registry.RegisteredHandlers.Add(handlerName, registration);
            return this;
        }

        /// <summary>
        /// Add http clients that are configured upon first request, as opposed to at service registration time. This allows your application to request http clients with new names at runtime and have a chnace to configure them dynamically here.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns></returns>
        public HandlerRegistryBuilder AddDynamicNamedHttpClients(Action<IServiceProvider, string, HttpClientOptions> configure)
        {
            Services.AddDynamicNamedHttpClients(configure);
            return this;
        }

        /// <summary>
        /// Add http clients that are configured upon first request, as opposed to at service registration time. This allows your application to request http clients with new names at runtime and have a chnace to configure them dynamically here.
        /// </summary>
        /// <param name="configure"></param>
        /// <returns>This method allows you to select an IConfiguration to be used to bind the <see cref="HttpClientOptions"/> that will be used to configure the client.</returns>

        public HandlerRegistryBuilder AddDynamicNamedHttpClients(Func<string, IConfiguration> getConfiguration)
        {
            Services.AddDynamicNamedHttpClients(getConfiguration);
            return this;
        }

    }

}
