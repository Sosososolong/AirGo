using IdentityServer4.AccessTokenValidation;
using Microsoft.AspNetCore.Diagnostics;
using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.Database;
using Sylas.RemoteTasks.App.DataHandlers;
using Sylas.RemoteTasks.App.ExceptionHandlers;
using Sylas.RemoteTasks.App.Helpers;
using Sylas.RemoteTasks.App.RemoteHostModule;
using Sylas.RemoteTasks.App.RemoteHostModule.Anything;
using Sylas.RemoteTasks.App.Repositories;
using Sylas.RemoteTasks.App.RequestProcessor;
using Sylas.RemoteTasks.Utils.Dto;
using static System.Net.Mime.MediaTypeNames;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.Limits.MaxRequestBodySize = null; // �ϴ��ļ�������, Ĭ��28.6M
});

// ��ʵ���õ���ʵ�������ļ�, ����ļ������ϴ���git; Ҳ��������Ŀ���Ҽ� -> "�����û�����" -> �����ʵ�����ø��ǵ�appsettings.json
builder.Configuration.AddJsonFile("TaskConfig.log.json", optional: true, reloadOnChange: true);

// ע��ȫ���ȼ�
builder.Services.AddGlobalHotKeys(builder.Configuration);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();

// ���Զ�������������
builder.Services.AddRemoteHostManager(builder.Configuration);

// ���"������������"����
builder.Services.AddSingleton<RequestProcessorBase>();

// ��Ӳִ� - ���Ͳִ�
builder.Services.AddScoped(typeof(RepositoryBase<>), typeof(RepositoryBase<>));
builder.Services.AddScoped<HttpRequestProcessorRepository>();
builder.Services.AddScoped<DbConnectionInfoRepository>();

// TODO: ��̬ע������DataHandler����
builder.Services.AddTransient<DataHandlerSyncDataToDb>();
builder.Services.AddTransient<DataHandlerCreateTable>();
builder.Services.AddTransient<DataHandlerAnonymization>();

// ��Ӱ�����
builder.Services.AddSingleton<DatabaseProvider>();

builder.Services.AddDatabaseUtils();

// ��ӷ���
builder.Services.AddTransient<HostService>();
builder.Services.AddTransient<AnythingService>();
builder.Services.AddTransient<RequestProcessorService>();

// ��̨����
builder.Services.AddHostedService<PublishService>();

// ��Ȩ����(����Identity Server������)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
    options.DefaultAuthenticateScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
    options.DefaultForbidScheme = IdentityServerAuthenticationDefaults.AuthenticationScheme;
}).AddIdentityServerAuthentication(options =>
{
    options.Authority = builder.Configuration["IdentityServerConfiguration:Authority"];
    // ��api resource (��ȡtokenʱ����scope������api resource)
    options.ApiName = builder.Configuration["IdentityServerConfiguration:ApiName"];
    // ��api resource secret
    options.ApiSecret = builder.Configuration["IdentityServerConfiguration:ApiSecret"];
    options.RequireHttpsMetadata = builder.Configuration.GetValue<bool>("IdentityServerConfiguration:RequireHttpsMetadata");
    options.EnableCaching = builder.Configuration.GetValue<bool>("IdentityServerConfiguration:EnableCaching");
    options.CacheDuration = TimeSpan.FromMinutes(builder.Configuration.GetValue<int>("IdentityServerConfiguration:CacheDuration"));
});

builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("sfapi.2", policyBuilder =>
    {
        //policyBuilder.RequireScope("sfapi2");
        policyBuilder.RequireAssertion(context => context.User.Claims.Any(c => c.Type == "scope" && c.Value == "sfapi2"));
    });
    config.AddPolicy("sfapi.3", policyBuilder =>
    {
        //policyBuilder.RequireScope("sfapi3");
        policyBuilder.RequireAssertion(context => context.User.Claims.Any(c => c.Type == "scope" && c.Value == "sfapi3"));
    });
});

var app = builder.Build();

// �����Ѿ�ȫ��ע��, ��¶��ȫ��ʹ��
//    ����һ�ַ�ģʽ
// 1. ��ôʹ�õ��ĵط��൱�ڻ�ȡ����ķ�ʽ�������DI����������(����ע��ķ�ʽ, ����ֻ��������Ҫ��ȡ�ķ����������, �������ǿ�����Ҳ������, Ҳ���Ǻ������ı���ʵ�ֵĶ�����϶Ⱥܵ�, ���ǿ���������滻������DIʵ��)
// 2. ȫ�ֵ�Ψһ������һ�ֲ�����ȫ�����, ����������̰߳�ȫ����(��Ȼ��������һ��ֻ������)
//ServiceLocator.Instance = app.Services;

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


app.Run();
