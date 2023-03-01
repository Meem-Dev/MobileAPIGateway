using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using System.Collections.Concurrent;
using System.Globalization;

namespace MobileAPIGateway.Yarp
{
    internal class TokenProvider
    {
        private readonly IHttpContextAccessor _accessor;
        private readonly ILogger<TokenProvider> _logger;
        private readonly TokenClient _tokenClient;
        private readonly ConcurrentDictionary<string, Lazy<Task<string>>> _tokens = new();

        public TokenProvider(IHttpContextAccessor accessor, ILogger<TokenProvider> logger, TokenClient tokenClient)
        {
            _accessor = accessor;
            _logger = logger;
            _tokenClient = tokenClient;
        }

        public async Task<string?> GetUserTokenAsync(CancellationToken cancellationToken)
        {
            var context = _accessor.HttpContext;
            if (context is null)
            {
                _logger.LogDebug("Can't get httpContext while authenticating user");
                return null;
            }

            var result = await context.AuthenticateAsync();
            if (!result.Succeeded)
            {
                _logger.LogDebug("Can't get token while authenticating user");
                return null;
            }

            var sub = result.Principal?.FindFirst("sub")?.Value ?? "unknown";
            var token = (await context.GetTokenAsync("access_token"))?.Trim();
            var refresh = (await context.GetTokenAsync("refresh_token"))?.Trim();
            var expires = (await context.GetTokenAsync("expires_at"))?.Trim();
            var expiresAt = DateTimeOffset.Parse(expires ?? DateTime.MinValue.ToString(CultureInfo.InvariantCulture),
                CultureInfo.InvariantCulture);

            _logger.LogDebug("For user {0},Refresh token is {1} present and Access Token is {2}",
                sub,
                string.IsNullOrEmpty(refresh) ? "not" : string.Empty,
                string.IsNullOrEmpty(token) ? "not present,a new token will be pulled using refresh token" : "present");

            if (string.IsNullOrEmpty(refresh))
                return null;

            if (expiresAt < DateTime.UtcNow)
            {
                _logger.LogDebug("Starting to refresh user token");
                return await SyncTokenRenewAccess(refresh, async () =>
                {
                    var response = (await _tokenClient.RenewUserTokenAsync(refresh, cancellationToken))!;
                    await StoreUserTokenAsync(response);
                    return response.AccessToken;
                });
            }

            return token;
        }

        private async Task StoreUserTokenAsync(TokenResponse tokenResponse)
        {
            if (tokenResponse is null)
                throw new ArgumentNullException(nameof(tokenResponse));

            var result = await _accessor.HttpContext!.AuthenticateAsync().ConfigureAwait(false);

            if (result.Properties is null)
            {
                _logger.LogDebug($"Can't get token while authenticating user");
                return;
            }

            result.Properties.UpdateTokenValue("access_token", tokenResponse.AccessToken);
            result.Properties.UpdateTokenValue("refresh_token", tokenResponse.RefreshToken);
            result.Properties.UpdateTokenValue("expires_at",
                DateTimeOffset.UtcNow.AddSeconds(tokenResponse.ExpiresIn).ToString("o", CultureInfo.InvariantCulture));

            result.Properties.ExpiresUtc = null;
            result.Properties.IssuedUtc = null;
            await _accessor.HttpContext!.SignInAsync(_accessor.HttpContext!.User, result.Properties).ConfigureAwait(false);
        }

        private async Task<string> SyncTokenRenewAccess(string tokenKey, Func<Task<string>> factory) =>
            await _tokens.GetOrAdd(tokenKey, _ =>
                new Lazy<Task<string>>(async () =>
                {
                    try
                    {
                        return await factory();
                    }
                    finally
                    {
                        _tokens.TryRemove(tokenKey, out var __);
                    }
                })).Value;
    }
}
