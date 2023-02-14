namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.DependencyInjection;

    public static class HttpClientHandlerRegistryServiceCollectionExtensions
    {
        public static HttpClientHandlerRegistry ConfigureHttpClients(this IServiceCollection services, Action<HandlerRegistryBuilder> registerHandlers)
        {
            services.AddHttpClient();
            var registry = new HttpClientHandlerRegistry();
            var builder = new HandlerRegistryBuilder(services, registry);
            registerHandlers(builder);
            var registery = builder.Registry;
            services.AddSingleton(registry);
            return registry;
        }

    }

}
