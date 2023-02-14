namespace Dazinator.Extensions.Http.Tests.Integration.HttpClientFactory
{
    using System.Net.Http;
    using System.Threading;
    using Dazinator.Extensions.Http;
    using Dazinator.Extensions.Http.Tests.Integration.Fakes;
    using Dazinator.Extensions.Http.Tests.Utils;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public class HttpClientFactory_IntegrationTests
    {
        /// <summary>
        /// Verifies that when a http client with a name is requested, we can configure it's <see cref="HttpClientFactoryOptions"/> and this configuration happens once per name.
        /// </summary>
        [Theory]
        [InlineData("foo", "foo-v2", "bar", "bar-v2")] // each 
        [InlineData("foo", "foo", "foo", "foo", "foo")]
        public void Can_ConfigureHttpClientFactory_LazilyForName(params string[] names)
        {
            var configureOptionsInvocationCount = 0;
            var httpClientActionsInvocationCount = 0;
            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
              {
                  services.AddHttpClient();
                  // Add named options configuration AFTER other configuration
                  services.ConfigureHttpClientFactory((sp, name, options) =>
                  {
                      // We expect this to be invoked lazily with each IHttpClientFactory.Create(name) call - but only once per distinct name.
                      Interlocked.Increment(ref configureOptionsInvocationCount);
                      options.HttpClientActions.Add(a =>
                      {
                          Interlocked.Increment(ref httpClientActionsInvocationCount);
                          a.BaseAddress = new Uri($"http://{name}.localhost/");
                      });
                  });
              });

            for (var i = 0; i < names.Length; i++)
            {
                var name = names[i];
                using var httpClient = sut.CreateClient(name);
                Assert.NotNull(httpClient);
                Assert.Equal($"http://{name}.localhost/", httpClient.BaseAddress.ToString());
                // We expect http client actions to be invoked each time we get a http client.
                Assert.Equal(i + 1, httpClientActionsInvocationCount);
            }

            // We expect option configuration to be invoked only once per named http client.
            var distinctNamedCount = names.Distinct().Count();
            Assert.Equal(distinctNamedCount, configureOptionsInvocationCount);
        }

        /// <summary>
        /// Verifies that when a named http client is requested, the HttpMessageHandlerBuilderActions we have configured for it are invoked only once for the HandlerLiftime duration. After the handler lifetime expires, the next time we request the same named http client, new handlers are built again, and so the HttpMessageHandlerBuilderActions should be invoked again to build the new handlers.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Can_ConfigureHttpClientFactory_HttpMessageHandlerBuilderActions()
        {
            var invocationCount = 0;
            var handlerLifetime = TimeSpan.FromSeconds(2);

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration
                services.ConfigureHttpClientFactory((sp, name, options) =>
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
            await Task.Delay(TimeSpan.FromSeconds(2));
            for (var i = 1; i <= max; i++)
            {
                using var httpClient = sut.CreateClient("foo");
                Assert.NotNull(httpClient);
                Assert.Equal(2, invocationCount);
            }
        }


        /// <summary>
        /// Verifies that we can configure the <see cref="HttpClientFactoryOptions"/> using our options API which allows http client to be defined in terms of <see cref="HttpClientOptions"/> plus handlers registered with a <see cref="HttpClientHandlerRegistry"/>.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Can_ConfigureHttpClientFactory_FromHttpClientOptionsAndHandlerRegistry()
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration

                // register some mock handlers in the handler registry.
                var statusOkHandlerName = "statusOkHandler";
                var statusNotFoundHandlerName = "statusNotFoundhandler";
                var handlerRegistry = services.ConfigureHttpClients((registry) => registry.RegisterHandler<FuncDelegatingHandler>(statusOkHandlerName, (r) =>

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

                // Configures HttpClientOptions on demand when a distinct name is requested.
                services.ConfigureHttpClient((sp, name, options) =>
                {
                    // load settings from some store using unique http client name (which can version)]
                    // "[app-code]-[system-name]-[type]-[purpose]-0283928923928392";
                    // "JNL-Integrity-Send-Cashflows-0283928923928392
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
            });

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);
        }


        /// <summary>
        /// Verifies that we can configure the <see cref="HttpClientFactoryOptions"/> using our options API which allows http client to be defined in terms of <see cref="HttpClientOptions"/> plus handlers registered with a <see cref="HttpClientHandlerRegistry"/>.
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task Can_ConfigureHandlersWithDifferentOptions()
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                // services.AddHttpClient();
                // Add named options configuration AFTER other configuration              

                // Rgister a handler, that uses named options to configure itself to behave differently per named http client.
                services.ConfigureHttpClients((registry) =>
                {
                    registry.RegisterHandler<DelegatingHandlerWithOptions<StatusHandlerOptions>>("status-handler", (r) =>
                    {
                        r.Factory = (sp, httpClientName) =>
                        {
                            var optionsMontior = sp.GetRequiredService<IOptionsMonitor<StatusHandlerOptions>>();
                            return new DelegatingHandlerWithOptions<StatusHandlerOptions>(httpClientName, optionsMontior, (request, handlerOptions, cancelToken) =>
                            {
                                var result = new HttpResponseMessage(handlerOptions.StatusCode);
                                return Task.FromResult(result);
                            });
                        };
                        r.Services.Configure<StatusHandlerOptions>((sp, name, options) =>
                        {
                            if (name.StartsWith("foo-"))
                            {
                                options.StatusCode = System.Net.HttpStatusCode.OK;
                            }
                            if (name.StartsWith("bar-"))
                            {
                                options.StatusCode = System.Net.HttpStatusCode.NotFound;
                            }
                        });
                    });

                    // configure two named http clients, to use the above status handler.
                    registry.Services.ConfigureHttpClient((sp, name, options) =>
                    {

                        if (name.StartsWith("foo-"))
                        {
                            options.BaseAddress = $"http://{name}.localhost";
                            options.EnableBypassInvalidCertificate = true;
                            options.MaxResponseContentBufferSize = 2000;
                            options.Timeout = TimeSpan.FromMinutes(2);
                            // Both clients have the same handler "status-handler" added.
                            // But as the handler has different named options (named after the http client name) the same
                            // handler ends up configured specific for each http client.
                            options.Handlers.Add("status-handler");
                        }
                        if (name.StartsWith("bar-"))
                        {
                            options.BaseAddress = $"http://{name}.localhost";
                            options.EnableBypassInvalidCertificate = true;
                            options.MaxResponseContentBufferSize = 2000;
                            options.Timeout = TimeSpan.FromMinutes(2);
                            // Both clients have the same handler "status-handler" added.
                            // But as the handler has different named options configured (named after each http client name) the same
                            // handler ends up configured specific for each http client.
                            options.Handlers.Add("status-handler");
                        }
                    });
                });
            });

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);
        }

        [Fact]
        public async Task Can_Configure_Using_HttpClientBuilder()
        {
            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {

                // register some mock handlers in the handler registry.
                var statusOkHandlerName = "statusOkHandler";
                var statusNotFoundHandlerName = "statusNotFoundhandler";
                var handlerRegistry = services.ConfigureHttpClients((registry) => registry.RegisterHandler<FuncDelegatingHandler>(statusOkHandlerName, (r) =>

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

                // Configures HttpClientOptions on demand when a distinct name is requested.
                services.Configure<HttpClientOptions>((sp, name, options) =>
                {
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

                services.AddHttpClient("foo-v1")
                        .SetupFromHttpClientOptions();

                services.AddHttpClient("bar-v1")
                        .SetupFromHttpClientOptions();
            });

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);
        }


    }



    public class StatusHandlerOptions
    {
        public System.Net.HttpStatusCode StatusCode { get; set; }
    }
}
