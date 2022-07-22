namespace Dazinator.Extensions.Http.Tests.Integration.Fakes
{
    using System.Net.Http;
    using System.Threading;

    public class FuncDelegatingHandler : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public FuncDelegatingHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync) => _sendAsync = sendAsync;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => await _sendAsync.Invoke(request, cancellationToken);

    }
}
