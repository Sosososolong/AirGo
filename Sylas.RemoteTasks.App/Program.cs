using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.Utils;

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

// 后台任务
builder.Services.AddHostedService<PublishService>();

var app = builder.Build();

// 服务已经全部注册, 暴露给全局使用
ServiceLocator.Instance = app.Services;

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

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
