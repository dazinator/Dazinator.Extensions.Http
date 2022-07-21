namespace Dazinator.Extensions.Http.Tests.Subjects.HttpClientFactory
{
    using System.Net.Http;
    using System.Threading;
    using Dazinator.Extensions.Http.Tests.Implementation;
    using Dazinator.Extensions.Http.Tests.Utils;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class HttpClientFactoryIntegrationTests
    {
        /// <summary>
        /// Verifies that when a http client with a name is requested, we can configure it's <see cref="HttpClientFactoryOptions"/> and this configuration happens once per name.
        /// </summary>
        [Theory]
        [InlineData("foo", "foo-v2", "bar", "bar-v2")] // each 
        [InlineData("foo", "foo", "foo", "foo", "foo")]
        public void CreateClient_WithName_ConfiguresHttpClientFactoryOptionsOncePerName(params string[] names)
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
              {
                  services.AddHttpClient();
                  // Add named options configuration AFTER other configuration
                  services.Configure<HttpClientFactoryOptions>((sp, name, options) =>
                  {
                      Interlocked.Increment(ref invocationCount);
                      options.HttpClientActions.Add(a => a.BaseAddress = new Uri($"http://{name}.localhost/"));
                  });

              });

            // var names = new List<string>() { "foo", "foo-v2", "bar", "bar-v2" };
            foreach (var name in names)
            {
                using var httpClient = sut.CreateClient(name);
                Assert.NotNull(httpClient);
                Assert.Equal($"http://{name}.localhost/", httpClient.BaseAddress.ToString());
            }

            var distinctNamedCount = names.Distinct().Count();
            Assert.Equal(distinctNamedCount, invocationCount);
        }

        /// <summary>
        /// Verifies that when a named http client is requested, the HttpClientActions we have configured for it are invoked.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CreateClient_WithName_InvokesConfiguredHttpClientActions()
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration
                services.Configure<HttpClientFactoryOptions>((sp, name, options) => options.HttpClientActions.Add(a =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        a.BaseAddress = new Uri($"http://{name}.localhost/");
                    }));

            });

            var max = 10;
            for (var i = 1; i <= max; i++)
            {
                using var httpClient = sut.CreateClient("foo");
                Assert.NotNull(httpClient);
                Assert.Equal($"http://foo.localhost/", httpClient.BaseAddress.ToString());
                Assert.Equal(i, invocationCount);
            }

        }

        /// <summary>
        /// Verifies that when a named http client is requested, the HttpMessageHandlerBuilderActions we have configured for it are invoked only once for the HandlerLiftime duration. After the handler lifetime expires, the next time we request the same named http client, new handlers are built and so the HttpMessageHandlerBuilderActions should be invoked again to build the new handlers.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CreateClient_WithName_HttpMessageHandlerBuilderActions_OncePerNameAndLifetime()
        {
            var invocationCount = 0;
            var handlerLifetime = TimeSpan.FromSeconds(2);

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration
                services.Configure<HttpClientFactoryOptions>((sp, name, options) =>
                {
                    options.HandlerLifetime = handlerLifetime;

                    options.HttpMessageHandlerBuilderActions.Add(a =>
                    {
                        Interlocked.Increment(ref invocationCount);
                        a.PrimaryHandler = new NotImlementedExceptionHttpMessageHandler();
                        // a.BaseAddress = new Uri($"http://{name}.localhost/");
                    });
                });

            });

            var max = 10;
            for (var i = 1; i <= max; i++)
            {
                using var httpClient = sut.CreateClient("foo");
                Assert.NotNull(httpClient);
                Assert.Equal(1, invocationCount);
            }

            await Task.Delay(handlerLifetime);
            for (var i = 1; i <= max; i++)
            {
                using var httpClient = sut.CreateClient("foo");
                Assert.NotNull(httpClient);
                Assert.Equal(2, invocationCount);
            }
        }


        /// <summary>
        /// Verifies that when a named http client is requested, the HttpMessageHandlerBuilderActions we have configured for it are invoked only once for the HandlerLiftime duration. After the handler lifetime expires, the next time we request the same named http client, new handlers are built and so the HttpMessageHandlerBuilderActions should be invoked again to build the new handlers.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task CreateClient_WithName_CanBeConfiguredViaHttpClientOptions()
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration

                var statusOkHandlerName = "statusOkHandler";
                var statusNotFoundHandlerName = "statusNotFoundhandler";
                var handlerRegistry = services.ConfigureHttpClientHandlerRegistry((registry) => registry.RegisterHandler<FuncDelegatingHandler>(statusOkHandlerName, (r) =>

                        // var f = new DelegatingHandler();
                        r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                            {
                                var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                                return Task.FromResult(result);
                            }))
                    .RegisterHandler<FuncDelegatingHandler>(statusNotFoundHandlerName, (r) =>
                         // var f = new DelegatingHandler();
                         r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                             {
                                 var result = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                                 return Task.FromResult(result);
                             })));
                services.Configure<HttpClientOptions>((sp, name, options) =>
                {
                    // load settings from some store using unique http client name (which can version)
                    if (name.StartsWith("foo-"))
                    {
                        options.BaseAddress = $"http://{name}.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add(statusOkHandlerName);
                    }
                    if (name.StartsWith("bar-"))
                    {
                        options.BaseAddress = $"http://{name}.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add(statusNotFoundHandlerName);
                    }
                });
                services.Configure<HttpClientFactoryOptions>((sp, httpClientName, options) =>
                {
                    var logger = sp.GetRequiredService<ILogger<HttpMessageHandlerBuilder>>();
                    // options.HandlerLifetime = handlerLifetime;
                    var httpClientOptionsFactory = sp.GetRequiredService<IOptionsMonitor<HttpClientOptions>>();
                    var httpClientOptions = httpClientOptionsFactory.Get(httpClientName);

                    //  options.ConfigureFromOptions(sp, name);
                    options.HttpClientActions.Add((httpClient) => httpClientOptions.Apply(httpClient));

                    if (httpClientOptions.EnableBypassInvalidCertificate)
                    {
                        logger.LogWarning("Http Client {HttpClientName} configured to accept any server certificate.", httpClientName);

                        options.HttpMessageHandlerBuilderActions.Add(a =>
                        {
                            if ((a.PrimaryHandler ?? new HttpClientHandler()) is not HttpClientHandler primaryHandler)
                            {
                                logger.LogWarning("Configured Primary Handler for Http Client {HttpClientName} is not a HttpClientHandler and therefore DangerousAcceptAnyServerCertificateValidator cannot be set.", httpClientName);
                            }
                            else
                            {
                                primaryHandler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
                                a.PrimaryHandler = primaryHandler;
                            }

                            if (httpClientOptions.Handlers?.Any() ?? false)
                            {
                                var registry = sp.GetRequiredService<HttpClientHandlerRegistry>();
                                foreach (var handlerName in httpClientOptions.Handlers)
                                {
                                    var handler = registry.GetHandlerInstance(handlerName, sp, httpClientName);
                                    a.AdditionalHandlers.Add(handler);
                                }
                            }
                            else
                            {
                                logger.LogWarning("ConfigureHttpClientFromOptions called on HttpClient: {HttpClientName} but no handlers were configured.", httpClientName);
                            }
                        });
                    }
                });
            });

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);
        }

    }

    public class NotImlementedExceptionHttpMessageHandler : HttpMessageHandler
    {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
