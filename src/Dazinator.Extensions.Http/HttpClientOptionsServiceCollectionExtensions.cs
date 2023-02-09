namespace Dazinator.Extensions.Http
{
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public static class HttpClientOptionsServiceCollectionExtensions
    {
        public static IServiceCollection ConfigureHttpClient(this IServiceCollection services,
            Action<IServiceProvider, string, HttpClientOptions> configure)
        {
            services.Configure<HttpClientOptions>(configure);
            // Configures HttpClientFactoryOptions on demand when a distinct httpClientName is requested.
            services.ConfigureHttpClientFactory(SetupHttpClientFactoryOptions);
            return services;
        }


        public static IServiceCollection ConfigureHttpClientFactory(this IServiceCollection services,
           Action<IServiceProvider, string, HttpClientFactoryOptions> configure)
        {
            // Configures HttpClientFactoryOptions on demand when a distinct httpClientName is requested.
            services.Configure<HttpClientFactoryOptions>(configure);
            return services;
        }

        /// <summary>
        /// Uses <see cref="HttpClientOptions"/> that have been configured with the same name as this http client, in order to configure the http client and handlers etc.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder SetupFromHttpClientOptions(this IHttpClientBuilder builder)
        {
            var httpClientName = builder.Name;
            var services = builder.Services;
            services.AddOptions<HttpClientFactoryOptions>(httpClientName)
                    .Configure<IServiceProvider>((o, sp) => SetupHttpClientFactoryOptions(sp, httpClientName, o));

            // Configures HttpClientFactoryOptions on demand when a distinct httpClientName is requested.
            //  builder.Services.ConfigureHttpClientFactory(SetupHttpClientFactoryOptions);
            return builder;
        }

        private static void SetupHttpClientFactoryOptions(IServiceProvider serviceProvider, string httpClientName, HttpClientFactoryOptions httpClientFactoryOptions)
        {

            var logger = serviceProvider.GetRequiredService<ILogger<HttpMessageHandlerBuilder>>();
            // options.HandlerLifetime = handlerLifetime;
            var httpClientOptionsFactory = serviceProvider.GetRequiredService<IOptionsMonitor<HttpClientOptions>>();
            var httpClientOptions = httpClientOptionsFactory.Get(httpClientName);

            //  options.ConfigureFromOptions(sp, name);
            httpClientFactoryOptions.HttpClientActions.Add((httpClient) => httpClientOptions.Apply(httpClient));

            // configure primary handler.
            httpClientFactoryOptions.HttpMessageHandlerBuilderActions.Add(a =>
            {
                if ((a.PrimaryHandler ?? new HttpClientHandler()) is not HttpClientHandler primaryHandler)
                {
                    logger.LogWarning("Configured Primary Handler for Http Client {HttpClientName} is not a HttpClientHandler and therefore DangerousAcceptAnyServerCertificateValidator and UseCookies cannot be set.", httpClientName);
                    return;
                }

                primaryHandler.UseCookies = httpClientOptions.UseCookies;

                if (httpClientOptions.EnableBypassInvalidCertificate)
                {
                    logger.LogWarning("Http Client {HttpClientName} configured to accept any server certificate.", httpClientName);
                    primaryHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                    a.PrimaryHandler = primaryHandler;
                }

            });


            if (httpClientOptions.Handlers?.Any() ?? false)
            {
                httpClientFactoryOptions.HttpMessageHandlerBuilderActions.Add(a =>
                {
                    var registry = serviceProvider.GetRequiredService<HttpClientHandlerRegistry>();

                    foreach (var handlerName in httpClientOptions.Handlers)
                    {
                        logger.LogDebug("Creating handler named: {HandlerName} for HttpClient: {HttpClientName}.", handlerName, httpClientName);

                        var handler = registry.GetHandlerInstance(handlerName, serviceProvider, httpClientName);
                        if (handler == null)
                        {
                            throw new Exception($"Handler named: {handlerName} was not found, for http client named: {httpClientName}");
                        }

                        a.AdditionalHandlers.Add(handler);
                    }
                });

            }
            else
            {
                logger.LogWarning("No handlers configured for HttpClient: {HttpClientName}.", httpClientName);
            }

        }

        /// <summary>
        /// Uses <see cref="HttpClientOptions"/> that have been configured with the same name as this http client, in order to configure the http client and handlers etc.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        public static IHttpClientBuilder SetupFromHttpClientOptions(this IHttpClientBuilder builder, Action<string, HttpClientOptions> configure)
        {
            var httpClientName = builder.Name;
            var services = builder.Services;

            services.Configure<HttpClientOptions>(configure);
            return SetupFromHttpClientOptions(builder);
        }


        public static IHttpClientBuilder SetupFromHttpClientOptions(this IHttpClientBuilder builder, Action<HttpClientOptions> configure)
        {
            var httpClientName = builder.Name;
            var services = builder.Services;
            services.Configure<HttpClientOptions>(httpClientName, configure);
            return SetupFromHttpClientOptions(builder);
        }

        /// <summary>
        /// Fluent helper method for configuring builder without breaking method chaining.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="configure"></param>
        /// <returns></returns>
        public static IHttpClientBuilder WithBuilder(this IHttpClientBuilder builder, Action<IHttpClientBuilder> configure)
        {
            configure?.Invoke(builder);
            return SetupFromHttpClientOptions(builder);
        }

    }
}
