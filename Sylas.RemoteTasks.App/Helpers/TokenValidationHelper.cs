using IdentityModel;
using IdentityModel.AspNetCore.OAuth2Introspection;
using IdentityModel.Client;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Encodings.Web;

namespace Sylas.RemoteTasks.App.Helpers
{
    public class TokenValidationHelper
    {
    }
    #region AuthorizationPolicyExtensions
    /// <summary>
    /// Extensions for creating scope related authorization policies
    /// </summary>
    public static class AuthorizationPolicyBuilderExtensions
    {
        /// <summary>
        /// Adds a policy to check for required scopes.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="scope">List of any required scopes. The token must contain at least one of the listed scopes.</param>
        /// <returns></returns>
        public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder builder, params string[] scope)
        {
            return builder.RequireClaim(JwtClaimTypes.Scope, scope);
        }
    }

    /// <summary>
    /// Helper for creating scope-related policies
    /// </summary>
    public static class ScopePolicy
    {
        /// <summary>
        /// Creates a policy to check for required scopes.
        /// </summary>
        /// <param name="scopes">List of any required scopes. The token must contain at least one of the listed scopes.</param>
        /// <returns></returns>
        public static AuthorizationPolicy Create(params string[] scopes)
        {
            return new AuthorizationPolicyBuilder()
                .RequireScope(scopes)
                .Build();
        }
    }
    #endregion

    #region ConfigureInternalOptions
    internal class ConfigureInternalOptions :
        IConfigureNamedOptions<JwtBearerOptions>,
        IConfigureNamedOptions<OAuth2IntrospectionOptions>
    {
        private readonly IdentityServerAuthenticationOptions _identityServerOptions;
        private string _scheme;

        public ConfigureInternalOptions(IdentityServerAuthenticationOptions identityServerOptions, string scheme)
        {
            _identityServerOptions = identityServerOptions;
            _scheme = scheme;
        }

        public void Configure(string name, JwtBearerOptions options)
        {
            if (name == _scheme + IdentityServerAuthenticationDefaults.JwtAuthenticationScheme &&
                _identityServerOptions.SupportsJwt)
            {
                _identityServerOptions.ConfigureJwtBearer(options);
            }
        }

        public void Configure(string name, OAuth2IntrospectionOptions options)
        {
            if (name == _scheme + IdentityServerAuthenticationDefaults.IntrospectionAuthenticationScheme &&
                _identityServerOptions.SupportsIntrospection)
            {
                _identityServerOptions.ConfigureIntrospection(options);
            }
        }

        public void Configure(JwtBearerOptions options)
        { }

        public void Configure(OAuth2IntrospectionOptions options)
        { }
    }
    #endregion

    #region IdentityServerAuthenticationDefaults
    /// <summary>
    /// Constants for IdentityServer authentication.
    /// </summary>
    public class IdentityServerAuthenticationDefaults
    {
        /// <summary>
        /// The authentication scheme
        /// </summary>
        public const string AuthenticationScheme = "Bearer";

        /// <summary>
        /// Value of the JWT typ header (IdentityServer4 v3+ sets this by default)
        /// </summary>
        public const string JwtAccessTokenTyp = "at+jwt";

        internal const string IntrospectionAuthenticationScheme = "IdentityServerAuthenticationIntrospection";
        internal const string JwtAuthenticationScheme = "IdentityServerAuthenticationJwt";
        internal const string TokenItemsKey = "idsrv4:tokenvalidation:token";
        internal const string EffectiveSchemeKey = "idsrv4:tokenvalidation:effective:";
    }
    #endregion
    #region IdentityServerAuthenticationExtensions
    /// <summary>
    /// Extensions for registering the IdentityServer authentication handler
    /// </summary>
    public static class IdentityServerAuthenticationExtensions
    {
        /// <summary>
        /// Registers the IdentityServer authentication handler.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddIdentityServerAuthentication(this AuthenticationBuilder builder)
            => builder.AddIdentityServerAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme);

        /// <summary>
        /// Registers the IdentityServer authentication handler.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddIdentityServerAuthentication(this AuthenticationBuilder builder, string authenticationScheme)
            => builder.AddIdentityServerAuthentication(authenticationScheme, configureOptions: null);

        /// <summary>
        /// Registers the IdentityServer authentication handler.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="configureOptions">The configure options.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddIdentityServerAuthentication(this AuthenticationBuilder builder, Action<IdentityServerAuthenticationOptions> configureOptions) =>
            builder.AddIdentityServerAuthentication(IdentityServerAuthenticationDefaults.AuthenticationScheme, configureOptions);

        /// <summary>
        /// Registers the IdentityServer authentication handler.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="configureOptions">The configure options.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddIdentityServerAuthentication(this AuthenticationBuilder builder, string authenticationScheme, Action<IdentityServerAuthenticationOptions> configureOptions)
        {
            builder.AddJwtBearer(authenticationScheme + IdentityServerAuthenticationDefaults.JwtAuthenticationScheme, configureOptions: null);
            builder.AddOAuth2Introspection(authenticationScheme + IdentityServerAuthenticationDefaults.IntrospectionAuthenticationScheme, configureOptions: null);

            builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>>(services =>
            {
                var monitor = services.GetRequiredService<IOptionsMonitor<IdentityServerAuthenticationOptions>>();
                return new ConfigureInternalOptions(monitor.Get(authenticationScheme), authenticationScheme);
            });

            builder.Services.AddSingleton<IConfigureOptions<OAuth2IntrospectionOptions>>(services =>
            {
                var monitor = services.GetRequiredService<IOptionsMonitor<IdentityServerAuthenticationOptions>>();
                return new ConfigureInternalOptions(monitor.Get(authenticationScheme), authenticationScheme);
            });

            return builder.AddScheme<IdentityServerAuthenticationOptions, IdentityServerAuthenticationHandler>(authenticationScheme, configureOptions);
        }

        /// <summary>
        /// Registers the IdentityServer authentication handler.
        /// </summary>
        /// <param name="builder">The builder.</param>
        /// <param name="authenticationScheme">The authentication scheme.</param>
        /// <param name="jwtBearerOptions">The JWT bearer options.</param>
        /// <param name="introspectionOptions">The introspection options.</param>
        /// <returns></returns>
        public static AuthenticationBuilder AddIdentityServerAuthentication(this AuthenticationBuilder builder, string authenticationScheme,
            Action<JwtBearerOptions> jwtBearerOptions,
            Action<OAuth2IntrospectionOptions> introspectionOptions)
        {
            if (jwtBearerOptions != null)
            {
                builder.AddJwtBearer(authenticationScheme + IdentityServerAuthenticationDefaults.JwtAuthenticationScheme, jwtBearerOptions);
            }

            if (introspectionOptions != null)
            {
                builder.AddOAuth2Introspection(authenticationScheme + IdentityServerAuthenticationDefaults.IntrospectionAuthenticationScheme, introspectionOptions);
            }

            return builder.AddScheme<IdentityServerAuthenticationOptions, IdentityServerAuthenticationHandler>(authenticationScheme, (o) => { });
        }
    }
    #endregion

    #region dentityServerAuthenticationHandler
    /// <summary>
    /// Authentication handler for validating both JWT and reference tokens
    /// </summary>
    public class IdentityServerAuthenticationHandler : AuthenticationHandler<IdentityServerAuthenticationOptions>
    {
        private readonly ILogger _logger;

        /// <inheritdoc />
        public IdentityServerAuthenticationHandler(
            IOptionsMonitor<IdentityServerAuthenticationOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock)
            : base(options, logger, encoder, clock)
        {
            _logger = logger.CreateLogger<IdentityServerAuthenticationHandler>();
        }

        /// <summary>
        /// Tries to validate a token on the current request
        /// </summary>
        /// <returns></returns>
        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            _logger.LogTrace("HandleAuthenticateAsync called");

            var jwtScheme = Scheme.Name + IdentityServerAuthenticationDefaults.JwtAuthenticationScheme;
            var introspectionScheme = Scheme.Name + IdentityServerAuthenticationDefaults.IntrospectionAuthenticationScheme;

            var token = Options.TokenRetriever(Context.Request);
            bool removeToken = false;

            try
            {
                if (token != null)
                {
                    _logger.LogTrace("Token found: {token}", token);

                    removeToken = true;
                    Context.Items.Add(IdentityServerAuthenticationDefaults.TokenItemsKey, token);

                    // seems to be a JWT
                    if (token.Contains('.') && Options.SupportsJwt)
                    {
                        _logger.LogTrace("Token is a JWT and is supported.");


                        Context.Items.Add(IdentityServerAuthenticationDefaults.EffectiveSchemeKey + Scheme.Name,
                            jwtScheme);
                        return await Context.AuthenticateAsync(jwtScheme);
                    }
                    else if (Options.SupportsIntrospection)
                    {
                        _logger.LogTrace("Token is a reference token and is supported.");

                        Context.Items.Add(IdentityServerAuthenticationDefaults.EffectiveSchemeKey + Scheme.Name,
                            introspectionScheme);
                        return await Context.AuthenticateAsync(introspectionScheme);
                    }
                    else
                    {
                        _logger.LogTrace(
                            "Neither JWT nor reference tokens seem to be correctly configured for incoming token.");
                    }
                }

                // set the default challenge handler to JwtBearer if supported
                if (Options.SupportsJwt)
                {
                    Context.Items.Add(IdentityServerAuthenticationDefaults.EffectiveSchemeKey + Scheme.Name, jwtScheme);
                }

                return AuthenticateResult.NoResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);
                return AuthenticateResult.Fail(ex);
            }
            finally
            {
                if (removeToken)
                {
                    Context.Items.Remove(IdentityServerAuthenticationDefaults.TokenItemsKey);
                }
            }
        }

        /// <summary>
        /// Override this method to deal with 401 challenge concerns, if an authentication scheme in question
        /// deals an authentication interaction as part of it's request flow. (like adding a response header, or
        /// changing the 401 result to 302 of a login page or external sign-in location.)
        /// </summary>
        /// <param name="properties"></param>
        /// <returns>
        /// A Task.
        /// </returns>
        protected override async Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            if (Context.Items.TryGetValue(IdentityServerAuthenticationDefaults.EffectiveSchemeKey + Scheme.Name, out object value))
            {
                if (value is string scheme)
                {
                    _logger.LogTrace("Forwarding challenge to scheme: {scheme}", scheme);
                    await Context.ChallengeAsync(scheme);
                }
            }
            else
            {
                await base.HandleChallengeAsync(properties);
            }
        }
    }
    #endregion

    #region IdentityServerAuthenticationOptions
    /// <summary>
    /// Options for IdentityServer authentication
    /// </summary>
    /// <seealso cref="Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions" />
    public class IdentityServerAuthenticationOptions : AuthenticationSchemeOptions
    {
        static readonly Func<HttpRequest, string> InternalTokenRetriever = request => request.HttpContext.Items[IdentityServerAuthenticationDefaults.TokenItemsKey] as string;

        /// <summary>
        /// Base-address of the token issuer
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// Specifies whether HTTPS is required for the discovery endpoint
        /// </summary>
        public bool RequireHttpsMetadata { get; set; } = true;

        /// <summary>
        /// Specifies which token types are supported (JWT, reference or both)
        /// </summary>
        public SupportedTokens SupportedTokens { get; set; } = SupportedTokens.Both;

        /// <summary>
        /// Callback to retrieve token from incoming request
        /// </summary>
        public Func<HttpRequest, string> TokenRetriever { get; set; } = request => request.Headers["Authorization"].FirstOrDefault()?.Replace("Bearer ", "") ?? string.Empty;

        /// <summary>
        /// Name of the API resource used for authentication against introspection endpoint
        /// </summary>
        public string ApiName { get; set; } = string.Empty;

        /// <summary>
        /// Secret used for authentication against introspection endpoint
        /// </summary>
        public string ApiSecret { get; set; } = string.Empty;

        /// <summary>
        /// Enable if this API is being secured by IdentityServer3, and if you need to support both JWTs and reference tokens.
        /// If you enable this, you should add scope validation for incoming JWTs.
        /// </summary>
        public bool LegacyAudienceValidation { get; set; } = false;

        /// <summary>
        /// Claim type for name
        /// </summary>
        public string NameClaimType { get; set; } = "name";

        /// <summary>
        /// Claim type for role
        /// </summary>
        public string RoleClaimType { get; set; } = "role";

        /// <summary>
        /// Specifies whether caching is enabled for introspection responses (requires a distributed cache implementation)
        /// </summary>
        public bool EnableCaching { get; set; } = false;

        /// <summary>
        /// Specifies ttl for introspection response caches
        /// </summary>
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Specifies the prefix of the cache key (token).
        /// </summary>
        public string CacheKeyPrefix { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the policy for the introspection discovery document.
        /// </summary>
        /// <value>
        /// The introspection discovery policy.
        /// </value>
        public DiscoveryPolicy IntrospectionDiscoveryPolicy { get; set; } = new DiscoveryPolicy();

        /// <summary>
        /// specifies whether the token should be saved in the authentication properties
        /// </summary>
        public bool SaveToken { get; set; } = true;

        /// <summary>
        /// specifies the allowed clock skew when validating JWT tokens
        /// </summary>
        public TimeSpan? JwtValidationClockSkew { get; set; }

        // todo: switch to factory approach
        /// <summary>
        /// back-channel handler for JWT middleware
        /// </summary>
        public HttpMessageHandler JwtBackChannelHandler { get; set; } = new HttpClientHandler();

        /// <summary>
        /// timeout for back-channel operations
        /// </summary>
        public TimeSpan BackChannelTimeouts { get; set; } = TimeSpan.FromSeconds(60);

        // todo
        /// <summary>
        /// events for JWT middleware
        /// </summary>
        public JwtBearerEvents JwtBearerEvents { get; set; } = new JwtBearerEvents();

        /// <summary>
        /// events for introspection endpoint
        /// </summary>
        public OAuth2IntrospectionEvents OAuth2IntrospectionEvents { get; set; } = new OAuth2IntrospectionEvents();

        /// <summary>
        /// Specifies how often the cached copy of the discovery document should be refreshed.
        /// If not set, it defaults to the default value of Microsoft's underlying configuration manager (which right now is 24h).
        /// If you need more fine grained control, provide your own configuration manager on the JWT options.
        /// </summary>
        public TimeSpan? DiscoveryDocumentRefreshInterval { get; set; }

        /// <summary>
        /// Gets a value indicating whether JWTs are supported.
        /// </summary>
        public bool SupportsJwt => SupportedTokens == SupportedTokens.Jwt || SupportedTokens == SupportedTokens.Both;

        /// <summary>
        /// Gets a value indicating whether reference tokens are supported.
        /// </summary>
        public bool SupportsIntrospection => SupportedTokens == SupportedTokens.Reference || SupportedTokens == SupportedTokens.Both;

        internal void ConfigureJwtBearer(JwtBearerOptions jwtOptions)
        {
            jwtOptions.Authority = Authority;
            jwtOptions.RequireHttpsMetadata = RequireHttpsMetadata;
            jwtOptions.BackchannelTimeout = BackChannelTimeouts;
            jwtOptions.RefreshOnIssuerKeyNotFound = true;
            jwtOptions.SaveToken = SaveToken;

            jwtOptions.Events = new JwtBearerEvents
            {
                OnMessageReceived = e =>
                {
                    e.Token = InternalTokenRetriever(e.Request);
                    return JwtBearerEvents.MessageReceived(e);
                },

                OnTokenValidated = e => JwtBearerEvents.TokenValidated(e),
                OnAuthenticationFailed = e => JwtBearerEvents.AuthenticationFailed(e),
                OnChallenge = e => JwtBearerEvents.Challenge(e)
            };

            if (DiscoveryDocumentRefreshInterval.HasValue)
            {
                var parsedUrl = DiscoveryEndpoint.ParseUrl(Authority);

                var httpClient = new HttpClient(JwtBackChannelHandler ?? new HttpClientHandler())
                {
                    Timeout = BackChannelTimeouts,
                    MaxResponseContentBufferSize = 1024 * 1024 * 10 // 10 MB
                };

                var manager = new ConfigurationManager<OpenIdConnectConfiguration>(
                    parsedUrl.Url,
                    new OpenIdConnectConfigurationRetriever(),
                    new HttpDocumentRetriever(httpClient) { RequireHttps = RequireHttpsMetadata })
                {
                    AutomaticRefreshInterval = DiscoveryDocumentRefreshInterval.Value
                };

                jwtOptions.ConfigurationManager = manager;
            }

            if (JwtBackChannelHandler != null)
            {
                jwtOptions.BackchannelHttpHandler = JwtBackChannelHandler;
            }

            // if API name is set, do a strict audience check for
            if (!string.IsNullOrWhiteSpace(ApiName) && !LegacyAudienceValidation)
            {
                jwtOptions.Audience = ApiName;
            }
            else
            {
                // no audience validation, rely on scope checks only
                jwtOptions.TokenValidationParameters.ValidateAudience = false;
            }

            jwtOptions.TokenValidationParameters.NameClaimType = NameClaimType;
            jwtOptions.TokenValidationParameters.RoleClaimType = RoleClaimType;

            if (JwtValidationClockSkew.HasValue)
            {
                jwtOptions.TokenValidationParameters.ClockSkew = JwtValidationClockSkew.Value;
            }

            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };

            // Use TokenHandlers instead
            //jwtOptions.SecurityTokenValidators.Clear();
            //jwtOptions.SecurityTokenValidators.Add(handler);
            jwtOptions.TokenHandlers.Clear();
            jwtOptions.TokenHandlers.Add(handler);

        }

        internal void ConfigureIntrospection(OAuth2IntrospectionOptions introspectionOptions)
        {
            if (String.IsNullOrWhiteSpace(ApiSecret))
            {
                return;
            }

            if (String.IsNullOrWhiteSpace(ApiName))
            {
                throw new ArgumentException("ApiName must be configured if ApiSecret is set.");
            }

            introspectionOptions.Authority = Authority;
            introspectionOptions.ClientId = ApiName;
            introspectionOptions.ClientSecret = ApiSecret;
            introspectionOptions.NameClaimType = NameClaimType;
            introspectionOptions.RoleClaimType = RoleClaimType;
            introspectionOptions.TokenRetriever = InternalTokenRetriever;
            introspectionOptions.SaveToken = SaveToken;
            introspectionOptions.DiscoveryPolicy = IntrospectionDiscoveryPolicy;

            introspectionOptions.EnableCaching = EnableCaching;
            introspectionOptions.CacheDuration = CacheDuration;
            introspectionOptions.CacheKeyPrefix = CacheKeyPrefix;

            introspectionOptions.DiscoveryPolicy.RequireHttps = RequireHttpsMetadata;

            introspectionOptions.Events = new OAuth2IntrospectionEvents
            {
                OnAuthenticationFailed = e => OAuth2IntrospectionEvents.AuthenticationFailed(e),
                OnTokenValidated = e => OAuth2IntrospectionEvents.OnTokenValidated(e),
            };
        }
    }
    public enum SupportedTokens
    {
        //
        // 摘要:
        //     JWTs and reference tokens
        Both,
        //
        // 摘要:
        //     JWTs only
        Jwt,
        //
        // 摘要:
        //     Reference tokens only
        Reference
    }
    #endregion
}
