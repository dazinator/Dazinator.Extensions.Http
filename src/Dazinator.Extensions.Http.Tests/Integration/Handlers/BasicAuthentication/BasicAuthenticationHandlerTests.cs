namespace Dazinator.Extensions.Http.Tests.Integration.Handlers.BasicAuthentication
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using Dazinator.Extensions.Http.Handlers.Authentication.Basic;
    using Dazinator.Extensions.Http.Tests.Integration.Fakes;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;

    public partial class BasicAuthenticationHandlerTests
    {

        [Fact]
        public async Task BasicAuth_Handler_Can_Be_Configured()
        {
            var services = new ServiceCollection();
            HttpRequestMessage lastRequest = null;

            // Used to capture a reference to the last request so our test can assert against it.
            services.AddTransient(sp =>
            {
                return new FuncDelegatingHandler((request, cancelToken) =>
                {
                    lastRequest = request;
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Content = new StringContent("OK FROM MOCK")
                    });
                });
            });

            var settings = new Dictionary<string, string>
            {
                // Configure basic auth for FooClient
                [$"HttpClient:{nameof(FooClient)}:Handlers:[0]"] = BasicAuthHttpClientHandlerRegistryExtensions.Name,
                [$"HttpClient:{nameof(FooClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Username)}"] = "foo@foo.com",
                [$"HttpClient:{nameof(FooClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Password)}"] = "p@ss",

                // Configure basic auth for BarClient
                [$"HttpClient:{nameof(BarClient)}:Handlers:[0]"] = BasicAuthHttpClientHandlerRegistryExtensions.Name,
                [$"HttpClient:{nameof(BarClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Username)}"] = "bar@bar.com",
                [$"HttpClient:{nameof(BarClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Password)}"] = "p@ss",

            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            // configure foo client       
            services.Configure<HttpClientOptions>(nameof(FooClient), config.GetSection($"HttpClient:{nameof(FooClient)}"));
            services.Configure<BasicAuthorizationHeaderHandlerOptions>(nameof(FooClient), config.GetSection($"HttpClient:{nameof(FooClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}"));

            // configure bar client   
            services.Configure<HttpClientOptions>(nameof(BarClient), config.GetSection($"HttpClient:{nameof(BarClient)}"));
            services.Configure<BasicAuthorizationHeaderHandlerOptions>(nameof(BarClient), config.GetSection($"HttpClient:{nameof(BarClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}"));

            var registry = services.ConfigureHttpClientHandlerRegistry((reg) =>
            {
                reg.RegisterBasicAuthHandler();
            });

            // Now register with HttpClientFactory in the normal way, but rather than adding common handlers, use ConfigureHttpClientFromOptions().
            services.AddHttpClient<FooClient>()
                    .SetupFromHttpClientOptions() // uses the options we registered above to perform rest of the configuration of common handlers etc. 
                    .AddHttpMessageHandler<FuncDelegatingHandler>();

            services.AddHttpClient<BarClient>()
                    .SetupFromHttpClientOptions() // uses the options we registered above to perform rest of the configuration of common handlers etc. 
                    .AddHttpMessageHandler<FuncDelegatingHandler>();

            var sp = services.BuildServiceProvider();

            var fooClient = sp.GetRequiredService<FooClient>();
            var barClient = sp.GetRequiredService<BarClient>();


            await fooClient.Get();
            Assert.NotNull(lastRequest);
            Assert.NotNull(lastRequest.Headers.Authorization);
            var authHeader = lastRequest.Headers.Authorization;

            AssertBasicAuthHeader(lastRequest.Headers.Authorization, "foo@foo.com", "p@ss");

            await barClient.Get();

            Assert.NotNull(lastRequest);
            Assert.NotNull(lastRequest.Headers.Authorization);
            AssertBasicAuthHeader(lastRequest.Headers.Authorization, "bar@bar.com", "p@ss");



        }

        [Fact]
        public async Task BasicAuth_Handler_Can_Use_CustomOptions()
        {
            var services = new ServiceCollection();
            HttpRequestMessage lastRequest = null;

            // Used to capture a reference to the last request so our test can assert against it.
            services.AddTransient(sp =>
            {
                return new FuncDelegatingHandler((request, cancelToken) =>
                {
                    lastRequest = request;
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Content = new StringContent("OK FROM MOCK")
                    });
                });
            });

            var settings = new Dictionary<string, string>
            {
                // Configure basic auth for FooClient
                [$"HttpClient:{nameof(FooClient)}:Handlers:[0]"] = BasicAuthHttpClientHandlerRegistryExtensions.Name,
                [$"HttpClient:{nameof(FooClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(CustomOptions.Username)}"] = "foo@foo.com",
                [$"HttpClient:{nameof(FooClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(CustomOptions.ProtectedPassword)}"] = "ABC",

            };

            var config = new ConfigurationBuilder()
              .AddInMemoryCollection(settings)
              .Build();

            var registry = services.ConfigureHttpClientHandlerRegistry((reg) =>
            {
                reg.RegisterBasicAuthHandler<CustomOptions>();
            });

            services.Configure<HttpClientOptions>(nameof(FooClient), config.GetSection($"HttpClient:{nameof(FooClient)}"));
            services.Configure<CustomOptions>(nameof(FooClient), config.GetSection($"HttpClient:{nameof(FooClient)}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}"));


            services.AddHttpClient<FooClient>()
                    .SetupFromHttpClientOptions()
                    .AddHttpMessageHandler<FuncDelegatingHandler>();

            var sp = services.BuildServiceProvider();

            var fooClient = sp.GetRequiredService<FooClient>();

            await fooClient.Get();
            Assert.NotNull(lastRequest);
            Assert.NotNull(lastRequest.Headers.Authorization);
            var authHeader = lastRequest.Headers.Authorization;

            AssertBasicAuthHeader(lastRequest.Headers.Authorization, "foo@foo.com", "CBA");

        }

        [Fact]
        public async Task BasicAuth_Handler_Can_Prevent_SendingCredentialsUnsecurely()
        {
            var services = new ServiceCollection();
            HttpRequestMessage lastRequest = null;

            // Used to capture a reference to the last request so our test can assert against it.
            services.AddTransient(sp =>
            {
                return new FuncDelegatingHandler((request, cancelToken) =>
                {
                    lastRequest = request;
                    return Task.FromResult(new HttpResponseMessage()
                    {
                        StatusCode = System.Net.HttpStatusCode.OK,
                        Content = new StringContent("OK FROM MOCK")
                    });
                });
            });

            var unsecureClientName = "UnsecureClient";
            var allowedUnsecureClientName = unsecureClientName + "allowed";

            var settings = new Dictionary<string, string>
            {
                // Unsecure prevented by default
                [$"HttpClient:{unsecureClientName}:Handlers:[0]"] = BasicAuthHttpClientHandlerRegistryExtensions.Name,
                [$"HttpClient:{unsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Username)}"] = "foo@foo.com",
                [$"HttpClient:{unsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Password)}"] = "p@ss",

                // Unsecure but allowed
                [$"HttpClient:{allowedUnsecureClientName}:Handlers:[0]"] = BasicAuthHttpClientHandlerRegistryExtensions.Name,
                [$"HttpClient:{allowedUnsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Username)}"] = "foo@foo.com",
                [$"HttpClient:{allowedUnsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.Password)}"] = "p@ss",
                [$"HttpClient:{allowedUnsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}:{nameof(BasicAuthorizationHeaderHandlerOptions.AllowHttp)}"] = "true",

            };

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();

            var registry = services.ConfigureHttpClientHandlerRegistry((reg) =>
            {
                reg.RegisterBasicAuthHandler();
            });

            services.Configure<HttpClientOptions>(unsecureClientName, config.GetSection($"HttpClient:{unsecureClientName}"));
            services.Configure<BasicAuthorizationHeaderHandlerOptions>(unsecureClientName, config.GetSection($"HttpClient:{unsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}"));

            services.Configure<HttpClientOptions>(allowedUnsecureClientName, config.GetSection($"HttpClient:{allowedUnsecureClientName}"));
            services.Configure<BasicAuthorizationHeaderHandlerOptions>(allowedUnsecureClientName, config.GetSection($"HttpClient:{allowedUnsecureClientName}:{BasicAuthHttpClientHandlerRegistryExtensions.Name}"));

            // Now register with HttpClientFactory in the normal way, but rather than adding common handlers, use ConfigureHttpClientFromOptions().
            services.AddHttpClient(unsecureClientName)
                    .SetupFromHttpClientOptions()
                    .AddHttpMessageHandler<FuncDelegatingHandler>();

            services.AddHttpClient(allowedUnsecureClientName)
                    .SetupFromHttpClientOptions() // uses the options we registered above to perform rest of the configuration of common handlers etc. 
                    .AddHttpMessageHandler<FuncDelegatingHandler>();

            var sp = services.BuildServiceProvider();

            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var unsecureClient = httpClientFactory.CreateClient(unsecureClientName);
            var allowedUnsecureClient = httpClientFactory.CreateClient(allowedUnsecureClientName);

            await Assert.ThrowsAsync<Exception>(async () => await unsecureClient.GetAsync("http://www.unsecure.com"));

            await allowedUnsecureClient.GetAsync("http://www.unsecure-but-allowed.com");
            Assert.NotNull(lastRequest);
            Assert.NotNull(lastRequest.Headers.Authorization);
            AssertBasicAuthHeader(lastRequest.Headers.Authorization, "foo@foo.com", "p@ss");



        }

        [Theory()]
        [InlineData("Foo", @"]PA\SW0RD!", "Rm9vOl1QQVxTVzBSRCE=")]
        public void EncodedUserNamePasswordMatchesExpected(string username, string password, string expectedEncodedValue)
        {
            string credentials = BasicAuthenticationUtils.GetAuthenticationHeaderValue(username, password);
            Assert.Equal(expectedEncodedValue, credentials);
        }

        private void AssertBasicAuthHeader(System.Net.Http.Headers.AuthenticationHeaderValue authHeader, string expectedUsername, string expectedPassword)
        {
            var base64 = authHeader.Parameter;
            var bytes = Convert.FromBase64String(base64);
            var decoded = Encoding.ASCII.GetString(bytes);
            Assert.Contains(":", decoded);

            var split = decoded.Split(":", StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(split[0], expectedUsername);
            Assert.Equal(split[1], expectedPassword);

            //var headerValue = $"{options.Username}:{options.Password}";
            //var byteArray = Encoding.ASCII.GetBytes(headerValue);
            //request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(Scheme, Convert.ToBase64String(byteArray));
        }

        public class FooClient
        {
            private readonly HttpClient _httpClient;

            public FooClient(HttpClient httpClient)
            {
                _httpClient = httpClient;
            }

            public async Task Get()
            {
                var response = await _httpClient.GetAsync("https://www.foo.com");
            }
        }

        public class BarClient
        {
            private readonly HttpClient _httpClient;

            public BarClient(HttpClient httpClient)
            {
                _httpClient = httpClient;
            }

            public async Task Get()
            {
                await _httpClient.GetAsync("https://www.bar.com");
            }
        }

        public class CustomOptions : BasicAuthorizationHeaderHandlerOptions
        {
            public string ProtectedPassword { get; set; }
            public override string GetPassword()
            {
                var unprotected = String.Concat(ProtectedPassword.Reverse());
                return unprotected;
            }
        }
    }

}
