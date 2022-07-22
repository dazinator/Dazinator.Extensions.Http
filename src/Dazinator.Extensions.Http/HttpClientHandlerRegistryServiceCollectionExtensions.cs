namespace Dazinator.Extensions.Http
{
    using Dazinator.Extensions.Options;
    using Microsoft.Extensions.DependencyInjection;

    public static class HttpClientHandlerRegistryServiceCollectionExtensions
    {
        public static HttpClientHandlerRegistry ConfigureHttpClientHandlerRegistry(this IServiceCollection services, Action<HttpClientHandlerRegistry> registerHandlers)
        {
            var registry = new HttpClientHandlerRegistry();
            registerHandlers(registry);
            services.AddSingleton(registry);
            return registry;
        }
    }
}
