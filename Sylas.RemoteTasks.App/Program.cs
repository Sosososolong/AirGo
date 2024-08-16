using IdentityModel;
using IdentityServer4.AccessTokenValidation;
using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.DatabaseManager;
using Sylas.RemoteTasks.App.DataHandlers;
using Sylas.RemoteTasks.App.ExceptionHandlers;
using Sylas.RemoteTasks.App.Helpers;
using Sylas.RemoteTasks.App.Hubs;
using Sylas.RemoteTasks.App.Infrastructure;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.App.RequestProcessor;
using Sylas.RemoteTasks.Utils.Constants;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.Limits.MaxRequestBodySize = null; // 上传文件无限制, 默认28.6M
});

// 我实际用的真实的配置文件, 这个文件不会上传到git; 也可以在项目上右键 -> "管理用户机密" -> 添加真实的配置覆盖掉appsettings.json
builder.Configuration.AddJsonFile("TaskConfig.log.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>();

// 注册全局热键
builder.Services.AddGlobalHotKeys(builder.Configuration);

// 添加缓存
builder.Services.AddCache();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();

builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// 添加远程主机服务对象
builder.Services.AddRemoteHostManager(builder.Configuration);

// 添加"网络请求任务"工厂
builder.Services.AddSingleton<RequestProcessorBase>();

// 添加仓储 - 泛型仓储
builder.Services.AddScoped(typeof(RepositoryBase<>), typeof(RepositoryBase<>));
builder.Services.AddScoped<HttpRequestProcessorRepository>();
builder.Services.AddScoped<DbConnectionInfoRepository>();

// TODO: 动态注册所有DataHandler服务
builder.Services.AddTransient<DataHandlerSyncDataToDb>();
builder.Services.AddTransient<DataHandlerCreateTable>();
builder.Services.AddTransient<DataHandlerAnonymization>();

// 添加帮助类
builder.Services.AddSingleton<DatabaseProvider>();

builder.Services.AddDatabaseUtils();

// 添加服务
builder.Services.AddTransient<HostService>();
builder.Services.AddTransient<AnythingService>();
builder.Services.AddTransient<RequestProcessorService>();

// 后台任务
builder.Services.AddHostedService<PublishService>();

// BOOKMARK: Action过滤器
builder.Services.AddScoped<CustomActionFilter>();
builder.Services.AddScoped<MvcParameterFilter>();

// BOOKMARK: 添加鉴权(身份认证)
builder.Services.AddAuthenticationService(builder.Configuration);
// BOOKMARK: 添加授权策略
builder.Services.AddAuthorization(options =>
{
    var adminApiConfiguration = new { AdministrationRole = builder.Configuration["IdentityServerConfiguration:AdministrationRole"], OidcApiName = builder.Configuration["IdentityServerConfiguration:ApiName"] };
    options.AddPolicy(AuthorizationConstants.AdministrationPolicy,
        policy =>
            policy.RequireAssertion(context => context.User.HasClaim(c =>
                    ((c.Type == JwtClaimTypes.Role && c.Value == adminApiConfiguration.AdministrationRole) ||
                    (c.Type == $"client_{JwtClaimTypes.Role}" && c.Value == adminApiConfiguration.AdministrationRole))
                ) && context.User.HasClaim(c => c.Type == JwtClaimTypes.Scope && c.Value == adminApiConfiguration.OidcApiName)
            ));
});

var app = builder.Build();

// 服务已经全部注册, 暴露给全局使用
//    这是一种反模式
// 1. 那么使用到的地方相当于获取对象的方式又耦合了DI容器对象了(依赖注入的方式, 我们只关心我们要获取的服务对象类型, 其他我们看不见也不关心, 也就是和其他的背后实现的对象耦合度很低, 我们可以任意地替换其他的DI实现)
// 2. 全局的唯一对象是一种不够安全的设计, 经常会带来线程安全问题(虽然这里它是一个只读对象)
//ServiceLocator.Instance = app.Services;

app.UseSession();

app.UseExceptionHandler(LambdaHandler.ReturnOperationResultAction);

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    //app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();


if (app.Environment.IsDevelopment())
{
    var defaultController = app.Configuration["DefaultController"] ?? "Sync";
    var defaultAction = app.Configuration["DefaultAction"] ?? "Index";
    app.MapControllerRoute(
        name: "default",
        //pattern: "{controller=Hosts}/{action=Index}/{id?}");
        pattern: $"{{controller={defaultController}}}/{{action={defaultAction}}}/{{id?}}");
}
else
{
    app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
}

app.MapHub<InformationHub>("informationHub");

app.Run();
