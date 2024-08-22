using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using System.Configuration;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Sylas.RemoteTasks.App.Helpers
{
    public static class StartupHelper
    {
        public static void AddRemoteHostManager(this IServiceCollection services, IConfiguration configuration)
        {
            var remoteHosts = configuration.GetSection("Hosts").Get<List<RemoteHost>>() ?? [];

            services.AddSingleton(remoteHosts);

            services.AddSingleton(serviceProvider =>
            {
                var result = new List<RemoteHostInfoProvider>();
                foreach (var remoteHost in remoteHosts)
                {
                    var dockerContainerProvider = new DockerContainerProvider(remoteHost);
                    result.Add(dockerContainerProvider);
                }
                return result;
            });
        }

        public static void AddCache(this IServiceCollection services)
        {
            services.AddDistributedMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30);
                options.Cookie.HttpOnly = false;
                options.Cookie.IsEssential = true;
            });
        }

        public static void AddDatabaseUtils(this IServiceCollection services)
        {
            // DatabaseInfo每调用一次方法都会创建新的数据库连接, 所以单例也可以实现多线程操作数据库
            // 但是不同连接交给不同的对象, 有助于做状态存储管理, 所以可以考虑使用Transient;
            // Scoped有个好处是, 一次请求处理多个HttpReqeustProcessorSteps的时候会处理多个HttpHandler,
            //   只要第一个HttpHandler的参数设置了连接字符串就会被存入到DatabaseInfo实例中, 一次请求中其他的HttpHandler都会使用此DatabaseInfo, 都不用再重新设置连接字符串参数了
            //   如果一次请求中也需要使用多个DatabaseInfo, 那么可以使用DatabaseInfoFactory创建一个新的对象, 并且可以继承线程内的DatabaseInfo的配置
            // 从这里看, Scoped是比较灵活的方式, 特定情况下它也可以实现Transient的效果
            services.AddScoped<DatabaseInfo>();

            // 即时创建新的DatabaseInfo, 单例即可
            services.AddSingleton<DatabaseInfoFactory>();

            services.AddScoped<IDatabaseProvider, DatabaseProvider>();
            services.AddScoped<IDatabaseProvider, DatabaseInfo>();
        }

        public static void AddGlobalHotKeys(this IServiceCollection services, IConfiguration configuration)
        {
            if (Environment.OSVersion.VersionString.Contains("Windows"))
            {
                var globalHotKeys = configuration.GetChildren().Where(x => x.Key == "GlobalHotKeys").First().GetChildren();
                int hotKeyId = 1;
                List<GlobalHotKey> globalHotKeyList = [];
                foreach (var globalHotKey in globalHotKeys)
                {
                    if (globalHotKey.Value is null)
                    {
                        break;
                    }
                    var keys = globalHotKey.Value.Split('+').ToList();
                    var lastKey = keys.Last();
                    keys.Remove(lastKey);
                    globalHotKeyList.Add(new GlobalHotKey(hotKeyId++, keys, lastKey));
                }
                SystemHelper.RegisterGlobalHotKey(globalHotKeyList);
            }
        }

        #region 鉴权
        public static void AddAuthenticationService(this IServiceCollection services, IConfiguration configuration)
        {
            // 鉴权服务(配置Identity Server服务器)
            //builder.Services.AddAuthentication(options =>
            //{
            //    options.DefaultScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultAuthenticateScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultChallengeScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultSignInScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
            //    options.DefaultForbidScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
            //}).AddIdentityServerAuthentication(options =>
            //{
            //    options.Authority = builder.Configuration["IdentityServerConfiguration:Authority"];
            //    // 即api resource (获取token时参数scope关联的api resource)
            //    options.ApiName = builder.Configuration["IdentityServerConfiguration:ApiName"];
            //    // 即api resource secret
            //    options.ApiSecret = builder.Configuration["IdentityServerConfiguration:ApiSecret"];
            //    options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("IdentityServerConfiguration:RequireHttpsMetadata");
            //    options.EnableCaching = builder.Configuration.GetValue<bool>("IdentityServerConfiguration:EnableCaching");
            //    options.CacheDuration = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("IdentityServerConfiguration:CacheDuration"));
            //    options.SupportedTokens = SupportedTokens.Both;
            //});

            string identityServerBaseUrl = configuration["IdentityServerConfiguration:Authority"] ?? throw new Exception("Authority不能为空");
            string clientId = configuration["IdentityServerConfiguration:ClientId"] ?? throw new Exception("client id不能为空");
            string clientSecret = configuration["IdentityServerConfiguration:ClientSecret"] ?? throw new Exception("client secret不能为空");
            string oidcResponseType = configuration["IdentityServerConfiguration:OidcResponseType"] ?? throw new Exception("oidc response type不能为空");
            bool requireHttpsMetadata = configuration.GetValue<bool>("IdentityServerConfiguration:RequireHttpsMetadata");
            List<string> scopes = [];
            configuration.GetSection("IdentityServerConfiguration:Scopes").Bind(scopes);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            services.AddAuthentication(options =>
            {
                //默认验证方案
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                //默认token验证失败后的确认验证结果方案
                options.DefaultChallengeScheme = "oidc";

                options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(CookieAuthenticationDefaults.AuthenticationScheme,
                options =>
                {
                    options.Cookie.Name = "RemoteTasksCookies";
                    options.Cookie.SameSite = (SameSiteMode)(-1);
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
                    //options.TicketDataFormat = new TicketDataFormat(new CookieTicketDataFormat());
                    //options.TicketDataFormat = new TicketDataFormat(new CustomCookieDataProtector());
                })
            .AddOpenIdConnect("oidc", options =>
            {
                //指定远程认证方案的本地登录处理方案
                options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                //远程认证地址
                options.Authority = identityServerBaseUrl;
                //Https强制要求标识
                options.RequireHttpsMetadata = requireHttpsMetadata;
                //客户端ID（支持隐藏模式和授权码模式，密码模式和客户端模式不需要用户登录）
                options.ClientId = clientId;
                options.ClientSecret = clientSecret;
                options.ResponseType = oidcResponseType;
                options.MapInboundClaims = false;
                options.Scope.Clear();
                foreach (var scope in scopes)
                {
                    options.Scope.Add(scope);
                }
                //options.ClaimActions.MapAll(); // 用户信息全部属性添加至Claims中

                //令牌保存标识
                options.SaveTokens = true;

                options.GetClaimsFromUserInfoEndpoint = true;

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = JwtClaimTypes.Name,
                    RoleClaimType = JwtClaimTypes.Role,
                };

                options.Events = new OpenIdConnectEvents
                {
                    OnMessageReceived = OnMessageReceived,
                    OnRedirectToIdentityProvider = n => OnRedirectToIdentityProvider(n),
                    OnTokenResponseReceived = n => OnTokenResponseReceived(n),
                    OnUserInformationReceived = n => OnUserInformationReceived(n)
                };
                options.CallbackPath = "/signin-oidc";
            });
        }
        private static Task OnRedirectToIdentityProvider(RedirectContext redirectContext)
        {
            // X-Scheme
            string scheme = redirectContext.Request.Headers["X-Scheme"].ToString() ?? redirectContext.Request.Scheme;
            if (string.IsNullOrEmpty(scheme))
            {
                scheme = redirectContext.Request.Scheme;
            }
            var host = $"{scheme}://{redirectContext.Request.Host.Value}";
            redirectContext.ProtocolMessage.RedirectUri = $"{host}/signin-oidc";
            redirectContext.ProtocolMessage.PostLogoutRedirectUri = $"{host}/signout-callback-oidc";
            return Task.CompletedTask;
        }
        private static Task OnMessageReceived(MessageReceivedContext context)
        {
            if (context.Properties is not null)
            {
                context.Properties.IsPersistent = false;
            }
            return Task.CompletedTask;
        }
        /// <summary>
        /// 成功获取用户信息
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private static Task OnUserInformationReceived(UserInformationReceivedContext context)
        {
            if (context.User != null)
            {
                context.Options.ClaimActions.Clear();
                // 从user数据拿属性添加到Claims中
                context.Options.ClaimActions.MapJsonKey("name", "UserAccount"); //给name单独映射
                context.Options.ClaimActions.MapJsonKey("scope", "scope");
                context.Options.ClaimActions.MapJsonKey("role", "role");
                context.Options.ClaimActions.MapCustomJson("userInfo", JsonClaimValueTypes.Json, user =>
                {
                    string userInfoJson = user.GetRawText();
                    return userInfoJson;
                });
            }
            return Task.CompletedTask;
        }

        private static Task OnTokenResponseReceived(TokenResponseReceivedContext context)
        {
            var accessToken = context.TokenEndpointResponse.AccessToken;
            // 检查是否成功获取到访问令牌
            if (!string.IsNullOrEmpty(accessToken))
            {
                context.HttpContext.Session.SetString("token", accessToken);

                // 将scope声明添加到用户的Claims中
                var scopes = context.TokenEndpointResponse.Scope.Split(' ');
                var claims = scopes.Select(x => new Claim("scope", x)).ToList();
                var identity = new ClaimsIdentity(claims, context.Principal?.Identity?.AuthenticationType);
                context.Principal?.AddIdentity(identity);
            }
            return Task.CompletedTask;
        }

        private static void SetSameSite(HttpContext httpContext, CookieOptions options)
        {
            if (options.SameSite == SameSiteMode.None)
            {
                if (httpContext.Request.Scheme != "https")
                {
                    options.SameSite = SameSiteMode.Unspecified;
                }
            }
        }
        #endregion
    }
}
