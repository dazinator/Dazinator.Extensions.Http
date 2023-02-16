namespace Dazinator.Extensions.Http.Tests.Integration.HttpClientFactory
{
    using System.Net.Http;
    using System.Threading;
    using Dazinator.Extensions.Http;
    using Dazinator.Extensions.Http.Tests.Integration.Fakes;
    using Dazinator.Extensions.Http.Tests.Utils;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Options;

    public class HttpClientFactory_IntegrationTests
    {
        /// <summary>
        /// Verifies that when a http client with a name is requested, we can configure it's <see cref="HttpClientFactoryOptions"/> and this configuration happens once per name.
        /// </summary>
        [Theory]
        [InlineData("foo", "foo-v2", "bar", "bar-v2")] // each 
        [InlineData("foo", "foo", "foo", "foo", "foo")]
        public void Can_ConfigureHttpClientFactoryOptions(params string[] names)
        {
            var configureOptionsInvocationCount = 0;
            var httpClientActionsInvocationCount = 0;
            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
              {
                  services.AddHttpClient();
                  // Add named options configuration AFTER other configuration
                  services.ConfigureHttpClientFactoryOptions((sp, name, options) =>
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
        public async Task Can_ConfigureHttpClientFactoryOptions_HandlerLifetime()
        {
            var invocationCount = 0;
            var handlerLifetime = TimeSpan.FromSeconds(2);

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration
                services.ConfigureHttpClientFactoryOptions((sp, name, options) =>
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
        public async Task Can_ConfigureHttpClientOptions_UsingDelegate_SingleHandlerTypeVaryingHandlerOptions()
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                // services.AddHttpClient();
                // Add named options configuration AFTER other configuration              

                // Rgister a handler, that uses named options to configure itself to behave differently per named http client.
                services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<DelegatingHandlerWithOptions<StatusHandlerOptions>>("status-handler", (services, r) =>
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
                    });
                })
                .ConfigureHttpClientOptions((sp, name, options) =>
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
                })
                .ConfigureUponRequest<StatusHandlerOptions>((sp, name, options) =>
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

            var fooClient = sut.CreateClient("foo-v1");
            var barClient = sut.CreateClient("bar-v1");

            var fooResponse = await fooClient.GetAsync("/foo");
            var barResponse = await barClient.GetAsync("/bar");

            Assert.Equal(System.Net.HttpStatusCode.OK, fooResponse.StatusCode);
            Assert.Equal(System.Net.HttpStatusCode.NotFound, barResponse.StatusCode);
        }

        [Fact]
        public async Task Can_ConfigureHttpClientOptions_AndConfigureUsingDelegate_MultipleHandlerTypes()
        {
            var invocationCount = 0;

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {
                services.AddHttpClient();
                // Add named options configuration AFTER other configuration

                // register some mock handlers in the handler registry.
                var statusOkHandlerName = "statusOkHandler";
                var statusNotFoundHandlerName = "statusNotFoundhandler";
                var handlerRegistry = services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<FuncDelegatingHandler>(statusOkHandlerName, (r) =>
                        r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                        {
                            var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                            return Task.FromResult(result);
                        }))
                    .Register<FuncDelegatingHandler>(statusNotFoundHandlerName, (r) =>
                        r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                        {
                            var result = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                            return Task.FromResult(result);
                        }));

                }).ConfigureHttpClientOptions((sp, name, options) =>
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


        [Fact]
        public async Task Can_AddHttpClientOptionsFactory_AndConfigureUsingIConfiguration()
        {

            // arrange
            // set up an IConfiguration that configures a section for each named http client's `HttpClientOptions` and configure the first to use a different handler.
            const string statusOkHandlerName = "statusOkHandler";
            const string statusNotFoundHandlerName = "statusNotFoundhandler";

            var configBuilder = new ConfigurationBuilder();

            var httpClientNames = new List<string>() { "foo-v1", "bar-v1" };

            var inMemoryConfigValues = new Dictionary<string, string>();
            foreach (var name in httpClientNames)
            {
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.BaseAddress)}", $"http://{name}.localhost");
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.EnableBypassInvalidCertificate)}", true.ToString());
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.MaxResponseContentBufferSize)}", "2000");
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.Timeout)}", $"00:02:00");
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.UseCookies)}", true.ToString());

                if (name == "foo-v1")
                {
                    inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.Handlers)}:0", statusOkHandlerName);
                }
                else
                {
                    inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.Handlers)}:0", statusNotFoundHandlerName);
                }
            }

            configBuilder.AddInMemoryCollection(inMemoryConfigValues);
            IConfiguration config = configBuilder.Build();


            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {

                // register some mock handlers in the handler registry.


                services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<FuncDelegatingHandler>(statusOkHandlerName, (r) =>
                        // var f = new DelegatingHandler();
                        r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                        {
                            var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                            return Task.FromResult(result);
                        }))
                       .Register<FuncDelegatingHandler>(statusNotFoundHandlerName, (r) =>
                         // var f = new DelegatingHandler();
                         r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                         {
                             var result = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                             return Task.FromResult(result);
                         }));
                }).ConfigureHttpClientOptions((name) =>
                {
                    return config.GetSection(name);
                });

            });


            foreach (var name in httpClientNames)
            {
                var httpClient = sut.CreateClient(name);
                var response = await httpClient.GetAsync($"/{name}");

                if (name == "foo-v1")
                {
                    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
                }
            }

            var unconfiguredClient = sut.CreateClient("no-such-client-configured");
            Assert.NotNull(unconfiguredClient);
            Assert.ThrowsAsync<InvalidOperationException>(async () => await unconfiguredClient.GetAsync($"/awdwad"));

        }

        [Fact]
        public async Task Can_AddHttpClient_AndConfigureOptions_UsingConfigureDelegate()
        {
            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {

                // register some mock handlers in the handler registry.
                var statusOkHandlerName = "statusOkHandler";
                var statusNotFoundHandlerName = "statusNotFoundhandler";
                var handlerRegistry = services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<FuncDelegatingHandler>(statusOkHandlerName, (r) =>
                        // var f = new DelegatingHandler();
                        r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                        {
                            var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                            return Task.FromResult(result);
                        }))
                       .Register<FuncDelegatingHandler>(statusNotFoundHandlerName, (r) =>
                         // var f = new DelegatingHandler();
                         r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                         {
                             var result = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                             return Task.FromResult(result);
                         }));
                })
                .AddHttpClient("foo-v1")
                    .ConfigureOptions((options) =>
                    {
                        options.BaseAddress = $"http://foo-v1.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add(statusOkHandlerName);
                    })
                .Services
                .AddHttpClient("bar-v1")
                    .ConfigureOptions((options) =>
                     {
                         options.BaseAddress = $"http://bar-v1.localhost";
                         options.EnableBypassInvalidCertificate = true;
                         options.MaxResponseContentBufferSize = 2000;
                         options.Timeout = TimeSpan.FromMinutes(2);
                         options.Handlers.Add(statusNotFoundHandlerName);
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
        public async Task Can_AddHttpClient_AndConfigureOptions_UsingIConfiguration()
        {

            // arrange
            // set up an IConfiguration that configures a section for each named http client's `HttpClientOptions` and configure the first to use a different handler.
            const string statusOkHandlerName = "statusOkHandler";
            const string statusNotFoundHandlerName = "statusNotFoundhandler";

            var configBuilder = new ConfigurationBuilder();

            var httpClientNames = new List<string>() { "foo-v1", "bar-v1" };

            var inMemoryConfigValues = new Dictionary<string, string>();
            foreach (var name in httpClientNames)
            {
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.BaseAddress)}", $"http://{name}.localhost");
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.EnableBypassInvalidCertificate)}", true.ToString());
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.MaxResponseContentBufferSize)}", "2000");
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.Timeout)}", $"00:02:00");
                inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.UseCookies)}", true.ToString());

                if (name == "foo-v1")
                {
                    inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.Handlers)}:0", statusOkHandlerName);
                }
                else
                {
                    inMemoryConfigValues.TryAdd($"{name}:{nameof(HttpClientOptions.Handlers)}:0", statusNotFoundHandlerName);
                }
            }

            configBuilder.AddInMemoryCollection(inMemoryConfigValues);
            IConfiguration config = configBuilder.Build();


            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {

                // register some mock handlers in the handler registry.


                var handlerRegistry = services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<FuncDelegatingHandler>(statusOkHandlerName, (r) =>
                        // var f = new DelegatingHandler();
                        r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                        {
                            var result = new HttpResponseMessage(System.Net.HttpStatusCode.OK);
                            return Task.FromResult(result);
                        }))
                       .Register<FuncDelegatingHandler>(statusNotFoundHandlerName, (r) =>
                         // var f = new DelegatingHandler();
                         r.Factory = (sp, httpClientName) => new FuncDelegatingHandler((request, cancelToken) =>
                         {
                             var result = new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                             return Task.FromResult(result);
                         }));


                    var services = registry.Services;

                    foreach (var name in httpClientNames)
                    {
                        services.AddHttpClient(name)
                        .ConfigureOptions(config.GetSection(name));
                    }
                });
            });


            foreach (var name in httpClientNames)
            {
                var httpClient = sut.CreateClient(name);
                var response = await httpClient.GetAsync($"/{name}");

                if (name == "foo-v1")
                {
                    Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
                }
                else
                {
                    Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
                }
            }

        }

        [Fact]
        public async Task Can_AddHttpClient_AndConfigureHandlerOptions()
        {
            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
            {

                // register some mock handlers in the handler registry.
                services.AddHttpClientHandlerRegistry((registry) =>
                {
                    registry.Register<DelegatingHandlerWithOptions<StatusHandlerOptions>>("status-handler", (r) =>
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
                    });
                })
                .AddHttpClient("foo-v1")
                    .ConfigureOptions((options) =>
                    {
                        options.BaseAddress = $"http://foo-v1.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add("status-handler");
                    })
                    .ConfigureOptions<StatusHandlerOptions>((a) => a.StatusCode = System.Net.HttpStatusCode.OK)
                .Services
                .AddHttpClient("bar-v1")
                    .ConfigureOptions((options) =>
                    {
                        options.BaseAddress = $"http://bar-v1.localhost";
                        options.EnableBypassInvalidCertificate = true;
                        options.MaxResponseContentBufferSize = 2000;
                        options.Timeout = TimeSpan.FromMinutes(2);
                        options.Handlers.Add("status-handler");
                    }).ConfigureOptions<StatusHandlerOptions>((a) => a.StatusCode = System.Net.HttpStatusCode.NotFound);
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
