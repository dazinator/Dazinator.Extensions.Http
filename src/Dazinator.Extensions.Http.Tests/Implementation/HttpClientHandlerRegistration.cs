namespace Dazinator.Extensions.Http.Tests.Implementation
{
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class HttpClientHandlerRegistration
    {
        /// <summary>
        /// Factory to be used to construct an instance of this handler for the specified named client.
        /// </summary>
        public Func<IServiceProvider, string, DelegatingHandler> Factory { get; set; }

        /// <summary>
        /// Action to run to configure the options for a specific named client using this handler. Runs once per named client for a handler.
        /// </summary>
        public Action<string> OnConfigureNamedClient { get; set; }

        /// <summary>
        /// Action to run to configure the default options for this handler. Runs once per handler.
        /// </summary>
        public Action<IServiceCollection> OnConfigure { get; set; }
    }
}
