namespace Dazinator.Extensions.Http.Tests
{
    using System.Net.Http;
    using Dazinator.Extensions.Http.Tests.Impl;
    using Dazinator.Extensions.Http.Tests.Utils;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Options;

    public class IntegrationTests
    {
        [Fact]
        public void Can_Get_NamedClient()
        {
            // arrange

            var settings = new Dictionary<string, string>();
            var httpClientName = "v1-BaseAddress";
            var initialBaseAddress = "http://foo.localhost";
            settings.Add(httpClientName, initialBaseAddress);

            var sut = TestHelper.CreateTestSubject<IHttpClientFactory>(out var testServices, (services) =>
              {
                  services.AddHttpClient();
                  // Add named options configuration AFTER other configuration
                  services.AddSingleton<IConfigureOptions<HttpClientFactoryOptions>, ConfigureNamedHttpClientFactoryOptions>();
              });

            // dynamicall add httpclient.
            settings.Add("foo", initialBaseAddress);
            using var httpClient = sut.CreateClient("foo");
            Assert.NotNull(httpClient);
            Assert.Equal(initialBaseAddress, httpClient?.BaseAddress?.ToString());

        }


    }

}
