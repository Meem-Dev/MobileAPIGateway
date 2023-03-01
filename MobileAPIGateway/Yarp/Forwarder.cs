using System.Net.Http.Headers;
using Yarp.ReverseProxy.Forwarder;

namespace MobileAPIGateway.Yarp
{
    internal class HttpClientWithBearerFactory : ForwarderHttpClientFactory
    {
        private readonly TokenProvider _tokenProvider;
        public HttpClientWithBearerFactory(TokenProvider tokenProvider) => _tokenProvider = tokenProvider;

        protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler) =>
            new CustomMessageHandler(_tokenProvider) { InnerHandler = handler };
    }

    internal class CustomMessageHandler : DelegatingHandler
    {
        private readonly TokenProvider _tokenService;
        public CustomMessageHandler(TokenProvider tokenService) => _tokenService = tokenService;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = await _tokenService.GetUserTokenAsync(cancellationToken);
            if (token is { })
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await base.SendAsync(request, cancellationToken);
            return response;
        }

    }
}
