namespace Dazinator.Extensions.Http.Tests.Impl
{
    using Microsoft.Extensions.Http;
    using Microsoft.Extensions.Options;

    public class ConfigureNamedHttpClientFactoryOptions : IConfigureNamedOptions<HttpClientFactoryOptions>
    {
        public void Configure(string name, HttpClientFactoryOptions options) => throw new NotImplementedException();

        // This won't be called, but is required for the interface
        public void Configure(HttpClientFactoryOptions options) => Configure(Options.DefaultName, options);
    }

}
