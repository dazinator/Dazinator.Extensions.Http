namespace Dazinator.Extensions.Http.Tests.Integration.Fakes
{
    using System.Net.Http;
    using System.Threading;

    public class NotImlementedExceptionHttpMessageHandler : HttpMessageHandler
    {

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
