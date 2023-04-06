using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.RemoteHostModule;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel((context, serverOptions) =>
{
    serverOptions.Limits.MaxRequestBodySize = null; // �ϴ��ļ�������, Ĭ��28.6M
});

// ��ʵ���õ���ʵ�������ļ�, ����ļ������ϴ���git; Ҳ��������Ŀ���Ҽ� -> "�����û�����" -> �����ʵ�����ø��ǵ�appsettings.json
builder.Configuration.AddJsonFile("TaskConfig.log.json", optional: true, reloadOnChange: true);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddHttpClient();

// ���Զ�������������
builder.Services.AddRemoteHostManager(builder.Configuration);

builder.Services.AddTransient<HostService>();

// ��̨����
builder.Services.AddHostedService<PublishService>();

var app = builder.Build();

// �����Ѿ�ȫ��ע��, ��¶��ȫ��ʹ��
// 1. ��ôʹ�õ��ĵط��൱�ڻ�ȡ����ķ�ʽ�������DI����������(����ע��ķ�ʽ, ����ֻ��������Ҫ��ȡ�ķ����������, �������ǿ�����Ҳ������, Ҳ���Ǻ������ı���ʵ�ֵĶ�����϶Ⱥܵ�, ���ǿ���������滻������DIʵ��)
// 2. ȫ�ֵ�Ψһ������һ�ֲ�����ȫ�����, ����������̰߳�ȫ����(��Ȼ��������һ��ֻ������)
//ServiceLocator.Instance = app.Services;
Console.WriteLine("001");

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
    pattern: "{controller=Hosts}/{action=Index}/{id?}");

app.Run();
