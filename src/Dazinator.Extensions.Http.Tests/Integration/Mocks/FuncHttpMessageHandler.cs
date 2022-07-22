namespace Dazinator.Extensions.Http.Tests.Integration.Fakes
{
    using System.Net.Http;
    using System.Threading;
    using Microsoft.Extensions.Options;

    public class FuncDelegatingHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public FuncDelegatingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) => _sendAsync = sendAsync;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => await _sendAsync.Invoke(request, cancellationToken);

    }

    public class DelegatingHandlerWithOptions<TOptions> : DelegatingHandler
    {
        private readonly string _handlerOptionsName;
        private readonly IOptionsMonitor<TOptions> _optionsMontitor;
        private readonly Func<HttpRequestMessage, TOptions, CancellationToken, Task<HttpResponseMessage>> _sendAsync;


        public DelegatingHandlerWithOptions(string handlerOptionsName, IOptionsMonitor<TOptions> optionsMontitor, Func<HttpRequestMessage, TOptions, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _handlerOptionsName = handlerOptionsName;
            _optionsMontitor = optionsMontitor;
            _sendAsync = sendAsync;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var handlerOptonsForNamedHttpClient = _optionsMontitor.Get(_handlerOptionsName);
            return await _sendAsync.Invoke(request, handlerOptonsForNamedHttpClient, cancellationToken);

        }

    }
}
