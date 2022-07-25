using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dazinator.Extensions.Http.Handlers.Authentication
{
    public class AuthorizationHeaderHttpMessageHandler : AuthorizationHeaderHttpMessageHandler<AuthorizationHeaderHttpMessageHandlerOptions>
    {
        public AuthorizationHeaderHttpMessageHandler(ILogger<AuthorizationHeaderHttpMessageHandler<AuthorizationHeaderHttpMessageHandlerOptions>> logger, IOptionsMonitor<AuthorizationHeaderHttpMessageHandlerOptions> authOptions, string optionsName, Func<string, int, CancellationToken, AuthorizationHeaderHttpMessageHandlerOptions, Task<string>> getAuthHeaderCredential) : base(logger, authOptions, optionsName, getAuthHeaderCredential)
        {
        }
    }

    public class AuthorizationHeaderHttpMessageHandler<TOptions> : DelegatingHandler
        where TOptions : class, IAuthorizationHeaderHandlerOptions
    {
        private const string Scheme = "Basic";
        private const string HttpsProtocolSchemeName = "https";
        private readonly ILogger<AuthorizationHeaderHttpMessageHandler<TOptions>> _logger;
        private readonly IOptionsMonitor<TOptions> _authOptions;
        private readonly string _clientName;

        public AuthorizationHeaderHttpMessageHandler(
            ILogger<AuthorizationHeaderHttpMessageHandler<TOptions>> logger,
            IOptionsMonitor<TOptions> authOptions,
            string clientName,
            Func<string, int, CancellationToken, TOptions, Task<string>> getAuthHeaderCredential)
        {
            _logger = logger;
            _authOptions = authOptions;
            _clientName = clientName;
            GetAuthHeaderCredential = getAuthHeaderCredential;
        }

        /// <summary>
        /// A function that must return the credential to be transmitted in the Authorization header with the request. 
        /// The credential returned is used in the auth header of the http request like so:
        ///    Authorization: {SchemeName} {credential}
        /// </summary>
        Func<string, int, CancellationToken, TOptions, Task<string>> GetAuthHeaderCredential { get; set; }


        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            TOptions options = null;
            if (string.IsNullOrWhiteSpace(_clientName))
            {
                options = _authOptions.CurrentValue;
            }
            else
            {
                options = _authOptions.Get(_clientName);
            }

            var scheme = options?.SchemeName ?? Scheme;
            var allowHttp = options.AllowHttp;
            if (!allowHttp && request.RequestUri.Scheme != HttpsProtocolSchemeName)
            {
                throw new Exception($"{scheme} authentication is not allowed to transmit credentials over unsecure protocol {request.RequestUri.Scheme}");
            }

            int? maxRetryCount = (options.MaxRetryUnauthorizedRequestCount ?? _authOptions.CurrentValue.MaxRetryUnauthorizedRequestCount) ?? 0;
            var upperLimit = maxRetryCount.GetValueOrDefault() + 1;

            HttpResponseMessage response = null;
            for (var i = 1; i <= upperLimit; i++)
            {
                var creds = await GetAuthHeaderCredential(_clientName, i, cancellationToken, options);
                cancellationToken.ThrowIfCancellationRequested();

                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(scheme, creds);

                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                if (response.StatusCode != System.Net.HttpStatusCode.Unauthorized)
                {
                    break;
                }

                _logger.LogTrace("Authorization header on request was rejected, attempt count {attempt} of {maxAttempts}, creds: {creds}, scheme: {scheme}", i, upperLimit, creds, scheme);
            }

            return response;

        }
    }

}
