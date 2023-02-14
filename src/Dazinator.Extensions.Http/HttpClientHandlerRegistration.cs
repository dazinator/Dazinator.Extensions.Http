namespace Dazinator.Extensions.Http
{
    using System.Net.Http;
    using Microsoft.Extensions.DependencyInjection;

    public class HttpClientHandlerRegistration
    {
        public HttpClientHandlerRegistration(IServiceCollection services)
        {
            Services = services;
        }

        /// <summary>
        /// Factory to be used to construct an instance of this handler for the specified named client.
        /// </summary>
        public Func<IServiceProvider, string, DelegatingHandler> Factory { get; set; }

        /// <summary>
        /// Register any services your handlers need for DI etc.
        /// </summary>
        public IServiceCollection Services { get; }

        internal void EnsureIsValid()
        {
            if (Factory == null)
            {
                throw new Exception("Invalid handler registration: Factory cannot be NULL");
            }
        }
    }
}
