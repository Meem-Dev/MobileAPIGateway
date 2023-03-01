using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Yarp.ReverseProxy.Forwarder;

namespace MobileAPIGateway.Yarp
{
    public static class Yarp
    {
        private static bool _useAntiForgery;
        public static void AddSpaGateway(this IServiceCollection services, IConfiguration configuration)
        {
            // Internal Config from Microsoft Yarp
            var yarp = configuration.GetRequiredSection("Yarp");
            services.AddReverseProxy().LoadFromConfig(yarp);
            services.AddSingleton<IForwarderHttpClientFactory, HttpClientWithBearerFactory>();

            // Can be changed in the future
            // services.AddAntiForgeryForYarp();
            services.AddLogin(configuration);
            services.AddCookies();
        }

        private static void AddLogin(this IServiceCollection services, IConfiguration configuration)
        {
            var openIdOptions = configuration.GetRequiredSection(OpenIdOptions.SectionName).Get<OpenIdOptions>();
            openIdOptions!.Validate();

            services.AddHttpClient().AddHttpContextAccessor();
            services.TryAddSingleton<TokenProvider>();
            services.TryAddSingleton<TokenClient>();

            services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            }).AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
            {
                options.Events = new CookieAuthenticationEvents
                {
                    OnRedirectToLogin = context =>
                    {
                        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                            context.Response.StatusCode = 401;
                        return Task.CompletedTask;
                    }
                };
            })
            .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
            {
                options.Authority = openIdOptions.Authority;
                options.ResponseType = "code";
                options.UsePkce = true;
                options.ClientId = openIdOptions.ClientId;
                options.ClientSecret = openIdOptions.ClientSecret;
                options.GetClaimsFromUserInfoEndpoint = true;
                options.RequireHttpsMetadata = false;
                options.SaveTokens = true;
                options.ClaimActions.MapAll();

                options.Scope.Clear();
                foreach (var scope in openIdOptions.Scopes)
                    options.Scope.Add(scope);

                if (!options.Scope.Contains("openid"))
                    options.Scope.Add("openid");

                if (!options.Scope.Contains("profile"))
                    options.Scope.Add("profile");

                if (openIdOptions.EnableRefreshToken == true)
                    options.Scope.Add("offline_access");

                options.SignedOutRedirectUri = "/Account/Signedout";
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = "name",
                    RoleClaimType = "role"
                };
                options.Events = new OpenIdConnectEvents
                {
                    OnRedirectToIdentityProvider = context =>
                    {
                        if (context.Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                        {
                            context.Response.StatusCode = 401;
                            context.HandleResponse();
                        }
                        return Task.CompletedTask;
                    }
                };
            });
        }

        private static void AddCookies(this IServiceCollection services)
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.MinimumSameSitePolicy = SameSiteMode.Unspecified;
                options.Secure = CookieSecurePolicy.SameAsRequest;
                options.OnAppendCookie = cookie => CheckSameSite(cookie.CookieOptions, cookie.Context);
                options.OnDeleteCookie = cookie => CheckSameSite(cookie.CookieOptions, cookie.Context);

                void CheckSameSite(CookieOptions op, HttpContext httpContext)
                {
                    if (op.SameSite != SameSiteMode.None) return;

                    var userAgent = httpContext.Request.Headers["User-Agent"].ToString();
                    var skipSameSite = userAgent!.Contains("CPU iPhone OS 12")
                                       || userAgent.Contains("iPad; CPU OS 12")
                                       || (userAgent.Contains("Macintosh; Intel Mac OS X 10_14") &&
                                           userAgent.Contains("Version/") && userAgent.Contains("Safari"))
                                       || userAgent.Contains("Chrome/5")
                                       || userAgent.Contains("Chrome/6");

                    if (!httpContext.Request.IsHttps || skipSameSite)
                        op.SameSite = SameSiteMode.Unspecified;
                }
            });
        }

        private static void AddAntiForgeryForYarp(this IServiceCollection services)
        {
            services.AddAntiforgery(options =>
            {
                options.HeaderName = "X-XSRF-TOKEN";
                options.SuppressXFrameOptionsHeader = false;
            });
            _useAntiForgery = true;
        }

        public static void MapYarpGateway(this WebApplication app)
        {
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();
            app.UseCookiePolicy();

            if (_useAntiForgery)
                app.Use(async (context, nextMiddleware) =>
                {
                    var path = context.Request.Path.Value ?? string.Empty;
                    if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                    {
                        var antiForgery = context.RequestServices.GetService(typeof(IAntiforgery)) as IAntiforgery;
                        var token = antiForgery?.GetAndStoreTokens(context);
                        if (token is { RequestToken: { } })
                            context.Response.Cookies.Append("XSRF-TOKEN", token.RequestToken,
                                new CookieOptions { HttpOnly = false });
                    }

                    await nextMiddleware();
                });

            app.Map("Account/Challenged", () => Results.Challenge(new AuthenticationProperties { RedirectUri = "/" }));
            app.Map("Account/Authenticate", () => Results.Ok()).RequireAuthorization();
            app.MapPost("Account/Signout", () =>
                Results.SignOut(authenticationSchemes: new[] { CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme }))
                .RequireAuthorization();

            app.MapReverseProxy();
            app.MapFallbackToFile("index.html");
        }
    }

}
