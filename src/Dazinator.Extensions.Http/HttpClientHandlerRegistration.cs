namespace Dazinator.Extensions.Http
{
    using System.Net.Http;

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

        internal void EnsureIsValid()
        {
            if(Factory == null)
            {
                throw new Exception("Invalid handler registration: Factory cannot be NULL");
            }
        }
    }
}
