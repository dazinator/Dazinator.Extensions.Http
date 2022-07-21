namespace Dazinator.Extensions.Http.Tests.Implementation
{
    using Microsoft.Extensions.DependencyInjection;

    public static class HttpClientHandlerRegistryServiceCollectionExtensions
    {
        public static HttpClientHandlerRegistry ConfigureHttpClientHandlerRegistry(this IServiceCollection services, Action<HttpClientHandlerRegistry> registerHandlers)
        {
            var registry = new HttpClientHandlerRegistry();
            registerHandlers(registry);
            services.AddSingleton(registry);
            registry.ConfigureHandlerDefaults(services);
            return registry;
        }
    }
}
