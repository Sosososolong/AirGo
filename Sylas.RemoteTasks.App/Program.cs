using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.DataHandlers;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.RequestProcessor;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.Limits.MaxRequestBodySize = null; // 上传文件无限制, 默认28.6M
});

// 我实际用的真实的配置文件, 这个文件不会上传到git; 也可以在项目上右键 -> "管理用户机密" -> 添加真实的配置覆盖掉appsettings.json
builder.Configuration.AddJsonFile("TaskConfig.log.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// 添加远程主机服务对象
builder.Services.AddRemoteHostManager(builder.Configuration);
// 添加"网络请求任务"工厂
builder.Services.AddSingleton<RequestProcessorDataTable>();
builder.Services.AddSingleton<RequestProcessorWithQueryString>();
// 添加仓储
builder.Services.AddScoped<HttpRequestProcessorRepository>();

// TODO: 动态注册所有DataHandler服务
builder.Services.AddTransient<DataHandlerSyncDataToDb>();
builder.Services.AddTransient<DataHandlerCreateTable>();

// 添加帮助类
builder.Services.AddSingleton<DatabaseProvider>();

builder.Services.AddDatabaseUtils();

// 添加服务
builder.Services.AddTransient<HostService>();
builder.Services.AddTransient<RequestProcessorService>();


// 后台任务
builder.Services.AddHostedService<PublishService>();

var app = builder.Build();

// 服务已经全部注册, 暴露给全局使用
// 1. 这是一种反模式
// 1. 那么使用到的地方相当于获取对象的方式又耦合了DI容器对象了(依赖注入的方式, 我们只关心我们要获取的服务对象类型, 其他我们看不见也不关心, 也就是和其他的背后实现的对象耦合度很低, 我们可以任意地替换其他的DI实现)
// 2. 全局的唯一对象是一种不够安全的设计, 经常会带来线程安全问题(虽然这里它是一个只读对象)
//ServiceLocator.Instance = app.Services;

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

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


app.Run();
