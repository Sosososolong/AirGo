using IdentityModel;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.Common.Extensions;
using Sylas.RemoteTasks.Database;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Dtos;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Reflection;
using System.Security.Claims;

namespace Sylas.RemoteTasks.App.Helpers
{
    public static class StartupHelper
    {
        public static void AddConfiguration(this WebApplicationBuilder builder)
        {
            // 我实际用的真实的配置文件, 这个文件不会上传到git; 也可以在项目上右键 -> "管理用户机密" -> 添加真实的配置覆盖掉appsettings.json
            builder.Configuration.AddJsonFile("TaskConfig.log.json", optional: true, reloadOnChange: true);
            builder.Configuration.AddUserSecrets<Program>();
            builder.Configuration.AddJsonFile("TaskImportantSettings.json", optional: true, reloadOnChange: true);
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

        public static void GetAppStatus(IConfiguration configuration)
        {
            AppStatus.ProcessId = Environment.ProcessId;

            string centerServer = configuration.GetValue<string>("CenterServer") ?? string.Empty;
            AppStatus.CenterServer = centerServer;

            AppStatus.IsCenterServer = string.IsNullOrWhiteSpace(centerServer);

            AppStatus.CenterWebServer = configuration.GetValue<string>("CenterWebServer");
            if (!AppStatus.IsCenterServer && string.IsNullOrWhiteSpace(AppStatus.CenterWebServer))
            {
                throw new Exception("未配置中心服务器的web服务地址");
            }

            AppStatus.Domain = Dns.GetHostName();

            AppStatus.InstancePath = AppDomain.CurrentDomain.BaseDirectory.ToBase64();
        }

        public static void AddAiConfig(this IServiceCollection services, IConfiguration configuration)
        {
            var aiConfig = new AiConfig();
            configuration.GetSection("AiConfig").Bind(aiConfig);
            services.AddSingleton(aiConfig);
            RemoteHelpers.AiConfig = aiConfig;
        }
        /// <summary>
        /// 注册Executor(通过ExecutorAttribute只注册依赖DI容器中其他对象的)
        /// </summary>
        /// <param name="services"></param>
        public static void AddExecutor(this IServiceCollection services)
        {
            var types = ReflectionHelper.GetTypes(typeof(ICommandExecutor));
            foreach (var type in types)
            {
                var attr = type.GetCustomAttribute<ExecutorAttribute>(true);
                if (attr is not null)
                {
                    services.Add(new ServiceDescriptor(typeof(ICommandExecutor), type.Name, type, ServiceLifetime.Scoped));
                }
            }
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
            string apiName = configuration["IdentityServerConfiguration:ApiName"] ?? throw new Exception("api name不能为空");
            string apiSecret = configuration["IdentityServerConfiguration:ApiSecret"] ?? throw new Exception("api secret不能为空");
            bool requireHttpsMetadata = configuration.GetValue<bool>("IdentityServerConfiguration:RequireHttpsMetadata");
            List<string> scopes = [];
            configuration.GetSection("IdentityServerConfiguration:Scopes").Bind(scopes);

            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            // 1.添加cookie身份认证方案;
            // 2.AddOpenIdConnect: oidc远程认证方案(基于cookie的对接IdentityServer4认证中心进行登录);
            // 3.AddIdentityServerAuthentication: IdentityServer4认证中心的API认证方案(Bearer token身份认证)
            // 4.控制器中使用[Authorize(AuthenticationSchemes = "Bearer,Cookies")]特性进行授权验证, 优先使用Bearer token进行验证, 如果没有Bearer token则使用Cookies进行验证
            services.AddAuthentication(options =>
            {
                ////默认验证方案
                //options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                ////默认token验证失败后的确认验证结果方案
                //options.DefaultChallengeScheme = "oidc";

                //options.DefaultAuthenticateScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                //options.DefaultForbidScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                //options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                //options.DefaultSignOutScheme = CookieAuthenticationDefaults.AuthenticationScheme;

                //默认验证方案
                options.DefaultScheme = "Bearer"; //CookieAuthenticationDefaults.AuthenticationScheme;
                //默认token验证失败后的确认验证结果方案
                options.DefaultChallengeScheme = "Bearer";

                options.DefaultAuthenticateScheme = "Bearer";
                options.DefaultForbidScheme = "Bearer";
                options.DefaultSignInScheme = "Bearer";
                options.DefaultSignOutScheme = "Bearer";
            })
            //.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme,
            //    options =>
            //    {
            //        options.Cookie.Name = "RemoteTasksCookies";
            //        options.Cookie.SameSite = (SameSiteMode)(-1);
            //        options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
            //        //options.TicketDataFormat = new TicketDataFormat(new CookieTicketDataFormat());
            //        //options.TicketDataFormat = new TicketDataFormat(new CustomCookieDataProtector());
            //    })
            //.AddOpenIdConnect("oidc", options =>
            //{
            //    //指定远程认证方案的本地登录处理方案
            //    options.SignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            //    //远程认证地址
            //    options.Authority = identityServerBaseUrl;
            //    //Https强制要求标识
            //    options.RequireHttpsMetadata = requireHttpsMetadata;
            //    //客户端ID（支持隐藏模式和授权码模式，密码模式和客户端模式不需要用户登录）
            //    options.ClientId = clientId;
            //    options.ClientSecret = clientSecret;
            //    options.ResponseType = oidcResponseType;
            //    options.MapInboundClaims = false;
            //    options.Scope.Clear();
            //    foreach (var scope in scopes)
            //    {
            //        options.Scope.Add(scope);
            //    }
            //    //options.ClaimActions.MapAll(); // 用户信息全部属性添加至Claims中

            //    //令牌保存标识
            //    options.SaveTokens = true;

            //    options.GetClaimsFromUserInfoEndpoint = true;

            //    options.TokenValidationParameters = new TokenValidationParameters
            //    {
            //        NameClaimType = JwtClaimTypes.Name,
            //        RoleClaimType = JwtClaimTypes.Role,
            //    };

            //    options.Events = new OpenIdConnectEvents
            //    {
            //        OnTokenResponseReceived = n => OnTokenResponseReceived(n),
            //        OnUserInformationReceived = n => OnUserInformationReceived(n),
            //        OnMessageReceived = OnMessageReceived,
            //        OnRedirectToIdentityProvider = n => OnRedirectToIdentityProvider(n)
            //    };
            //    options.CallbackPath = "/signin-oidc";
            //})
            .AddIdentityServerAuthentication(options =>
            {
                options.Authority = identityServerBaseUrl;
                // 即api resource (获取token时参数scope关联的api resource)
                options.ApiName = apiName;
                // 即api resource secret
                options.ApiSecret = apiSecret;
                options.RequireHttpsMetadata = requireHttpsMetadata;
                options.JwtBearerEvents.OnTokenValidated = async context =>
                {
                    var claimsIdentity = context.Principal?.Identity as ClaimsIdentity; // 即控制器的属性User的属性Identity

                    if (claimsIdentity is not null)
                    {
                        // 将 nameidentifier 改为 sub
                        var nameIdentifierClaim = claimsIdentity.FindFirst(ClaimTypes.NameIdentifier);
                        if (nameIdentifierClaim != null)
                        {
                            claimsIdentity.RemoveClaim(nameIdentifierClaim);
                            claimsIdentity.AddClaim(new Claim("sub", nameIdentifierClaim.Value));
                        }

                        // 添加 role 声明, 某些授权的Policy是根据role判断的
                        var roleClaims = claimsIdentity.FindAll(ClaimTypes.Role).ToList();
                        foreach (var roleClaim in roleClaims)
                        {
                            claimsIdentity.AddClaim(new Claim(JwtClaimTypes.Role, roleClaim.Value));
                        }
                    }

                    await Task.CompletedTask;
                };

                //options.EnableCaching = true;
                //options.CacheDuration = TimeSpan.FromMinutes(3);
                //options.SupportedTokens = SupportedTokens.Both;
            });
        }
        private static Task OnRedirectToIdentityProvider(RedirectContext redirectContext)
        {
            if (redirectContext.Request.Headers.XRequestedWith == "XMLHttpRequest")
            {
                redirectContext.Response.StatusCode = 401;
                redirectContext.HandleResponse();
            }
            else
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
            }
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
                // 从user数据拿属性添加到Claims中, "要添加的Claim Type" => "Claim Value取用户信息中的xx属性"
                context.Options.ClaimActions.MapJsonKey("name", "name");
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
