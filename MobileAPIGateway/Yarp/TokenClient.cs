using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;

namespace MobileAPIGateway.Yarp
{
    internal class TokenClient
    {
        private readonly IOptionsMonitor<OpenIdConnectOptions> _oidc;
        private readonly IAuthenticationSchemeProvider _provider;
        private readonly IHttpClientFactory _clientFactory;
        private readonly ILogger<TokenClient> _logger;

        public TokenClient(IOptionsMonitor<OpenIdConnectOptions> oidc, IAuthenticationSchemeProvider provider, IHttpClientFactory clientFactory, ILogger<TokenClient> logger)
        {
            _oidc = oidc;
            _provider = provider;
            _clientFactory = clientFactory;
            _logger = logger;
        }

        public async Task<TokenResponse?> RenewUserTokenAsync(string refreshToken, CancellationToken cancellationToken)
        {
            if (refreshToken is null)
                throw new ArgumentNullException(nameof(refreshToken));

            var scheme = await _provider.GetDefaultChallengeSchemeAsync()
                .ConfigureAwait(false);

            if (scheme == null)
                throw new Exception("There is no default challenge scheme for renew token");

            var options = _oidc.Get(scheme.Name);
            var config = await options.ConfigurationManager!.GetConfigurationAsync(cancellationToken)
                .ConfigureAwait(false);

            var client = _clientFactory.CreateClient();
            var response = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
            {
                RefreshToken = refreshToken,
                ClientId = options.ClientId,
                ClientSecret = options.ClientSecret,
                Address = config.TokenEndpoint
            }, cancellationToken).ConfigureAwait(false);

            if (response.IsError)
                _logger.LogError(
                        "Renew token response has error : [{0}],ErrorDescription : [{1}],ErrorType : [{2}],HttpErrorReason : [{3}]",
                        response.Error, response.ErrorDescription, response.ErrorType, response.HttpErrorReason);
            return !response.IsError ? response : null;
        }
    }
}
