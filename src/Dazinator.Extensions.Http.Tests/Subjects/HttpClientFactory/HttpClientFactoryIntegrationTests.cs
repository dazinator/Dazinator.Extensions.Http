namespace Dazinator.Extensions.Http.Tests.Subjects.HttpClientFactory
{
    using System.Net.Http;
    using System.Threading;
    using Dazinator.Extensions.Http.Tests.Impl;
    using Dazinator.Extensions.Http.Tests.Utils;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
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
    }

    public class NotImlementedExceptionHttpMessageHandler : HttpMessageHandler
    {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
