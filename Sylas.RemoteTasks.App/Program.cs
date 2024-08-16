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
    serverOptions.Limits.MaxRequestBodySize = null; // �ϴ��ļ�������, Ĭ��28.6M
});

// ��ʵ���õ���ʵ�������ļ�, ����ļ������ϴ���git; Ҳ��������Ŀ���Ҽ� -> "�����û�����" -> �����ʵ�����ø��ǵ�appsettings.json
builder.Configuration.AddJsonFile("TaskConfig.log.json", optional: true, reloadOnChange: true);
builder.Configuration.AddUserSecrets<Program>();

// ע��ȫ���ȼ�
builder.Services.AddGlobalHotKeys(builder.Configuration);

// ��ӻ���
builder.Services.AddCache();

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSignalR();

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

// BOOKMARK: Action������
builder.Services.AddScoped<CustomActionFilter>();
builder.Services.AddScoped<MvcParameterFilter>();

// BOOKMARK: ��Ӽ�Ȩ(�����֤)
builder.Services.AddAuthenticationService(builder.Configuration);
// BOOKMARK: �����Ȩ����
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

// �����Ѿ�ȫ��ע��, ��¶��ȫ��ʹ��
//    ����һ�ַ�ģʽ
// 1. ��ôʹ�õ��ĵط��൱�ڻ�ȡ����ķ�ʽ�������DI����������(����ע��ķ�ʽ, ����ֻ��������Ҫ��ȡ�ķ����������, �������ǿ�����Ҳ������, Ҳ���Ǻ������ı���ʵ�ֵĶ�����϶Ⱥܵ�, ���ǿ���������滻������DIʵ��)
// 2. ȫ�ֵ�Ψһ������һ�ֲ�����ȫ�����, ����������̰߳�ȫ����(��Ȼ��������һ��ֻ������)
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
