using Sylas.RemoteTasks.App.BackgroundServices;
using Sylas.RemoteTasks.App.Utils;

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

// ��̨����
builder.Services.AddHostedService<PublishService>();

var app = builder.Build();

// �����Ѿ�ȫ��ע��, ��¶��ȫ��ʹ��
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
