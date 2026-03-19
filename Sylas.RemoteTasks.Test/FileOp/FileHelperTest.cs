using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.Database.SyncBase;
using Sylas.RemoteTasks.Utils.CommandExecutor;
using Sylas.RemoteTasks.Utils.Constants;
using Sylas.RemoteTasks.Utils.Template;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.FileOp
{
    public class FileHelperTest(ITestOutputHelper outputHelper, TestFixture fixture) : IClassFixture<TestFixture>
    {
        private readonly DatabaseInfo _databaseInfo = fixture.ServiceProvider.GetRequiredService<DatabaseInfo>();
        private readonly IConfiguration _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();

        /// <summary>
        /// 文件执行器功能测试 - 更新文件
        /// </summary>
        [Fact]
        public async Task FileHeExecutor_UpdateFileTest()
        {
            #region 准备阶段
            string testDir = $"unit-test-temp-dir-{DateTime.Now:yyyyMMdd}";
            if (Directory.Exists(testDir))
            {
                throw new Exception("我希望测试目录不存在, 我将会创建它并且测试完成我会再删除它");
            }
            Directory.CreateDirectory(testDir);
            if (!Directory.Exists(testDir))
            {
                Directory.CreateDirectory(testDir);
            }

            #region Shared.csproj
            string sharedOriginTxt = """
                <Project Sdk="Microsoft.NET.Sdk.Razor">

                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>

                  <ItemGroup>
                    <SupportedPlatform Include="browser" />
                  </ItemGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="9.0.0" />
                  </ItemGroup>

                </Project>
                """;
            string file = $"{testDir.TrimEnd('/')}/MBHW.Shared.csproj";
            await File.WriteAllTextAsync(file, sharedOriginTxt);
            string sharedExpectedTxt = """
                <Project Sdk="Microsoft.NET.Sdk.Razor">

                  <PropertyGroup>
                    <TargetFramework>net9.0</TargetFramework>
                    <Nullable>enable</Nullable>
                    <ImplicitUsings>enable</ImplicitUsings>
                  </PropertyGroup>

                  <ItemGroup>
                    <SupportedPlatform Include="browser" />
                  </ItemGroup>

                  <ItemGroup>
                    <PackageReference Include="Microsoft.AspNetCore.Components.Web" Version="9.0.0" />
                    <PackageReference Include="MudBlazor" Version="7.15.0" />
                  </ItemGroup>

                </Project>
                """;
            #endregion

            #region _Imports.razor
            string importsOriginTxt = """
                @using System.Net.Http
                @using System.Net.Http.Json
                @using Microsoft.AspNetCore.Components.Forms
                @using Microsoft.AspNetCore.Components.Routing
                @using Microsoft.AspNetCore.Components.Web
                @using static Microsoft.AspNetCore.Components.Web.RenderMode
                @using Microsoft.AspNetCore.Components.Web.Virtualization
                @using Microsoft.JSInterop
                
                """;
            string importsFile = $"{testDir.TrimEnd('/')}/_Imports.razor";
            await File.WriteAllTextAsync(importsFile, importsOriginTxt);
            string importsExpectedTxt = """
                @using System.Net.Http
                @using System.Net.Http.Json
                @using Microsoft.AspNetCore.Components.Forms
                @using Microsoft.AspNetCore.Components.Routing
                @using Microsoft.AspNetCore.Components.Web
                @using static Microsoft.AspNetCore.Components.Web.RenderMode
                @using Microsoft.AspNetCore.Components.Web.Virtualization
                @using Microsoft.JSInterop

                @using MudBlazor
                
                """;
            #endregion

            #region Web/Program.cs
            string webProgramOriginTxt = """
                using MBHW.Shared.Services;
                using MBHW.Web.Components;
                using MBHW.Web.Services;

                var builder = WebApplication.CreateBuilder(args);

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveWebAssemblyComponents();

                // Add device-specific services used by the MBHW.Shared project
                builder.Services.AddSingleton<IFormFactor, FormFactor>();

                var app = builder.Build();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseWebAssemblyDebugging();
                }
                else
                {
                    app.UseExceptionHandler("/Error", createScopeForErrors: true);
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseHttpsRedirection();

                app.UseStaticFiles();
                app.UseAntiforgery();

                app.MapRazorComponents<App>()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(
                        typeof(MBHW.Shared._Imports).Assembly,
                        typeof(MBHW.Web.Client._Imports).Assembly);

                app.Run();
                
                """;
            string webProgramFile = $"{testDir.TrimEnd('/')}/MBHW.Web.Program.cs";
            await File.WriteAllTextAsync(webProgramFile, webProgramOriginTxt);
            string webProgramExpectedTxt = """
                using MBHW.Shared.Services;
                using MBHW.Web.Components;
                using MBHW.Web.Services;
                using MudBlazor.Services;

                var builder = WebApplication.CreateBuilder(args);

                // Add services to the container.
                builder.Services.AddRazorComponents()
                    .AddInteractiveWebAssemblyComponents();

                // Add device-specific services used by the MBHW.Shared project
                builder.Services.AddSingleton<IFormFactor, FormFactor>();

                builder.Services.AddMudServices();
                var app = builder.Build();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseWebAssemblyDebugging();
                }
                else
                {
                    app.UseExceptionHandler("/Error", createScopeForErrors: true);
                    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                    app.UseHsts();
                }

                app.UseHttpsRedirection();

                app.UseStaticFiles();
                app.UseAntiforgery();

                app.MapRazorComponents<App>()
                    .AddInteractiveWebAssemblyRenderMode()
                    .AddAdditionalAssemblies(
                        typeof(MBHW.Shared._Imports).Assembly,
                        typeof(MBHW.Web.Client._Imports).Assembly);

                app.Run();
                
                """;

            string mauiProgramOriginTxt = """
                using MBHW.Services;
                using MBHW.Shared.Services;
                using Microsoft.Extensions.Logging;

                namespace MBHW
                {
                    public static class MauiProgram
                    {
                        public static MauiApp CreateMauiApp()
                        {
                            var builder = MauiApp.CreateBuilder();
                            builder
                                .UseMauiApp<App>()
                                .ConfigureFonts(fonts =>
                                {
                                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                                });

                            // Add device-specific services used by the MBHW.Shared project
                            builder.Services.AddSingleton<IFormFactor, FormFactor>();

                            builder.Services.AddMauiBlazorWebView();

                #if DEBUG
                            builder.Services.AddBlazorWebViewDeveloperTools();
                            builder.Logging.AddDebug();
                #endif

                            return builder.Build();
                        }
                    }
                }
                
                """;
            string mauiProgramFile = $"{testDir.TrimEnd('/')}/MauiProgram.cs";
            await File.WriteAllTextAsync(mauiProgramFile, mauiProgramOriginTxt);
            string mauiProgramExpectedTxt = """
                using MBHW.Services;
                using MBHW.Shared.Services;
                using Microsoft.Extensions.Logging;
                using MudBlazor.Services;

                namespace MBHW
                {
                    public static class MauiProgram
                    {
                        public static MauiApp CreateMauiApp()
                        {
                            var builder = MauiApp.CreateBuilder();
                            builder
                                .UseMauiApp<App>()
                                .ConfigureFonts(fonts =>
                                {
                                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                                });

                            // Add device-specific services used by the MBHW.Shared project
                            builder.Services.AddSingleton<IFormFactor, FormFactor>();

                            builder.Services.AddMauiBlazorWebView();

                #if DEBUG
                            builder.Services.AddBlazorWebViewDeveloperTools();
                            builder.Logging.AddDebug();
                #endif

                builder.Services.AddMudServices();
                            return builder.Build();
                        }
                    }
                }
                
                """;
            #endregion

            #region index.html
            string indexTxt = """
                <!DOCTYPE html>
                <html lang="en">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
                    <title>MBHW</title>
                    <base href="/" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/bootstrap/bootstrap.min.css" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/app.css" />
                    <link rel="stylesheet" href="app.css" />
                    <link rel="stylesheet" href="MBHW.styles.css" />
                    <link rel="icon" href="data:,">
                </head>

                <body>

                    <div class="status-bar-safe-area"></div>

                    <div id="app">Loading...</div>

                    <script src="_framework/blazor.webview.js" autostart="false"></script>

                </body>

                </html>
                """;
            string indexFile = $"{testDir.TrimEnd('/')}/index.html";
            await File.WriteAllTextAsync(indexFile, indexTxt);
            string indexExpectedTxt = """
                <!DOCTYPE html>
                <html lang="zh-cn">
                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no, viewport-fit=cover" />
                    <title>MBHW</title>
                    <base href="/" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/bootstrap/bootstrap.min.css" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/app.css" />
                    <link rel="stylesheet" href="app.css" />
                    <link rel="stylesheet" href="MBHW.styles.css" />
                    <link rel="icon" href="data:,">
                    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
                    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
                </head>

                <body>

                    <div class="status-bar-safe-area"></div>

                    <div id="app">Loading...</div>

                    <script src="_framework/blazor.webview.js" autostart="false"></script>
                    <script src="_content/MudBlazor/MudBlazor.min.js"></script>

                </body>

                </html>
                """;
            #endregion

            #region App.Razor
            string appRazor = """
                <!DOCTYPE html>
                <html lang="en">

                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <base href="/" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/bootstrap/bootstrap.min.css" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/app.css" />
                    <link rel="stylesheet" href="MBHW.Web.styles.css" />
                    <link rel="icon" type="image/png" href="_content/MBHW.Shared/favicon.png" />
                    <HeadOutlet @rendermode="InteractiveWebAssembly" />
                </head>

                <body>
                    <Routes @rendermode="InteractiveWebAssembly" />
                    <script src="_framework/blazor.web.js"></script>
                </body>

                </html>
                
                """;
            string appRazorFile = $"{testDir.TrimEnd('/')}/App.Razor";
            await File.WriteAllTextAsync(appRazorFile, appRazor);
            string appRazorExpectedTxt = """
                <!DOCTYPE html>
                <html lang="zh-cn">

                <head>
                    <meta charset="utf-8" />
                    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                    <base href="/" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/bootstrap/bootstrap.min.css" />
                    <link rel="stylesheet" href="_content/MBHW.Shared/app.css" />
                    <link rel="stylesheet" href="MBHW.Web.styles.css" />
                    <link rel="icon" type="image/png" href="_content/MBHW.Shared/favicon.png" />
                    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
                    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />
                    <HeadOutlet @rendermode="InteractiveWebAssembly" />
                </head>

                <body>
                    <Routes @rendermode="InteractiveWebAssembly" />
                    <script src="_framework/blazor.web.js"></script>
                    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
                </body>

                </html>
                
                """;
            #endregion

            #endregion

            #region 执行命令
            string opCmd = """
                ## MudBlazor - 1.添加包(${SlnDir})
                ### 添加MudBlazor包
                TargetFilePattern: Shared.csproj$$
                Value: {sp:4}<PackageReference Include="MudBlazor" Version="7.15.0" />
                OperationType: Append
                LinePattern: PackageReference\s+Include

                ### 给Blazor组件添加MudBlazor的全局引用
                TargetFilePattern: _Imports.razor$$
                Value: @using MudBlazor
                OperationType: Append
                LinePattern: 

                ### 客户端添加MudBlazor服务
                TargetFilePattern: (\.web.+program.cs)|(mauiprogram.cs)
                Value: using MudBlazor.Services;|||builder.Services.AddMudServices();
                OperationType: Append|||Prepend
                LinePattern: using |||builder.Build()

                ### 添加MudBlazor的css,js文件
                TargetFilePattern: (index\.html)|(App\.razor)
                Value: {sp:4}<link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
                {sp:4}<link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />|||    <script src="_content/MudBlazor/MudBlazor.min.js"></script>
                OperationType: Append|||Append
                LinePattern: link\s+rel|||<script

                ### index.html/App.Razor将lang=en替换为lang=zh-cn
                TargetFilePattern: (index\.html)|(App\.razor)
                OperationType: Replace
                LinePattern: lang="en"
                value: lang="zh-cn"
                """;
            // 注意：这里的模板解析需要支持多行文本的解析，并且在Value中使用|||分隔不同文件的内容
            //opCmd = TmplHelper.ResolveTemplate
            opCmd = TmplHelper.ResolveExpressionValue(opCmd, new Dictionary<string, string> { { "SlnDir", testDir } })?.ToString() ?? throw new Exception("模板解析结果为空");

            var serviceScopeFactory = fixture.ServiceProvider.GetService<IServiceScopeFactory>();
            var result = ICommandExecutor.Create("FileHelper", [], serviceScopeFactory);
            if (result.Code != 1)
            {
                throw new Exception(result.ErrMsg);
            }
            var executor = result.Data ?? throw new Exception("FileHelper执行器为空");
            var commandResults = executor([opCmd]);
            await foreach (var commandResult in commandResults)
            {
                outputHelper.WriteLine($"操作完毕: {commandResult.Message}");
            }
            #endregion

            #region 断言
            string sharedResultTxt = await File.ReadAllTextAsync(file);
            Assert.Equal(sharedResultTxt, sharedExpectedTxt);

            string importsResultTxt = await File.ReadAllTextAsync(importsFile);
            Assert.Equal(importsResultTxt, importsExpectedTxt);

            string webProgramResultTxt = await File.ReadAllTextAsync(webProgramFile);
            Assert.Equal(webProgramResultTxt, webProgramExpectedTxt);
            string mauiProgramResultTxt = await File.ReadAllTextAsync(mauiProgramFile);
            Assert.Equal(mauiProgramResultTxt, mauiProgramExpectedTxt);

            string indexResultTxt = await File.ReadAllTextAsync(indexFile);
            Assert.Equal(indexResultTxt, indexExpectedTxt);
            string appRazorResultTxt = await File.ReadAllTextAsync(appRazorFile);
            Assert.Equal(appRazorResultTxt, appRazorExpectedTxt);
            #endregion

            #region 准备阶段 - 命令2
            
            #region Mainlayout.razor
            string mainLayoutOriginTxt = """
                @inherits LayoutComponentBase

                <div class="page">
                    <div class="sidebar">
                        <NavMenu />
                    </div>

                    <main>
                        <div class="top-row px-4">
                            <a href="https://learn.microsoft.com/aspnet/core/" target="_blank">About</a>
                        </div>

                        <article class="content px-4">
                            @Body
                        </article>
                    </main>
                </div>

                <div id="blazor-error-ui" data-nosnippet>
                    An unhandled error has occurred.
                    <a href="." class="reload">Reload</a>
                    <span class="dismiss">🗙</span>
                </div>
                
                """;
            string mainLayoutFile = $"{testDir.TrimEnd('/')}/Mainlayout.razor";
            await File.WriteAllTextAsync(mainLayoutFile, mainLayoutOriginTxt);
            string mainLayoutExpectedTxt = """
                @inherits LayoutComponentBase

                <MudThemeProvider @ref="_mudThemeProvider" @bind-IsDarkMode="_isDarkMode" />
                <MudPopoverProvider />
                <MudDialogProvider />
                <MudSnackbarProvider />

                <MudLayout>
                    <MudAppBar>
                        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start" OnClick="@((e) => DrawerToggle())" />
                        <MudText Typo="Typo.h5" Class="ml-3">My Application</MudText>
                        <MudSpacer />
                        <MudIconButton Icon="@Icons.Material.Filled.MoreVert" Color="Color.Inherit" Edge="Edge.End"/>
                    </MudAppBar>
                    <MudDrawer @bind-Open="@_drawerOpen" Variant="@DrawerVariant.Mini" OpenMiniOnHover="true">
                        <NavMenu/>
                    </MudDrawer>
                    <MudMainContent>
                        <MudContainer MaxWidth="MaxWidth.Medium">
                            @Body
                        </MudContainer>
                    </MudMainContent>
                </MudLayout>
                @code {
                    bool _drawerOpen = true;

                    void DrawerToggle()
                    {
                        _drawerOpen = !_drawerOpen;
                    }
                    private bool _isDarkMode;
                    private MudThemeProvider _mudThemeProvider;

                    protected override async Task OnAfterRenderAsync(bool firstRender)
                    {
                        if (firstRender)
                        {
                            await _mudThemeProvider.WatchSystemPreference(OnSystemDarkModeChanged);
                            StateHasChanged();
                        }
                    }

                    private Task OnSystemDarkModeChanged(bool newValue)
                    {
                        _isDarkMode = newValue;
                        StateHasChanged();
                        return Task.CompletedTask;
                    }
                }
                """;
            #endregion

            #region NavMenu.razor
            string navMenuOriginTxt = """
                <div class="top-row ps-3 navbar navbar-dark">
                    <div class="container-fluid">
                        <a class="navbar-brand" href="">MBHW</a>
                    </div>
                </div>

                <input type="checkbox" title="Navigation menu" class="navbar-toggler" />

                <div class="nav-scrollable" onclick="document.querySelector('.navbar-toggler').click()">
                    <nav class="flex-column">
                        <div class="nav-item px-3">
                            <NavLink class="nav-link" href="" Match="NavLinkMatch.All">
                                <span class="bi bi-house-door-fill-nav-menu" aria-hidden="true"></span> Home
                            </NavLink>
                        </div>

                        <div class="nav-item px-3">
                            <NavLink class="nav-link" href="counter">
                                <span class="bi bi-plus-square-fill-nav-menu" aria-hidden="true"></span> Counter
                            </NavLink>
                        </div>

                        <div class="nav-item px-3">
                            <NavLink class="nav-link" href="weather">
                                <span class="bi bi-list-nested-nav-menu" aria-hidden="true"></span> Weather
                            </NavLink>
                        </div>
                    </nav>
                </div>

                
                """;
            string navMenuFile = $"{testDir.TrimEnd('/')}/NavMenu.razor";
            await File.WriteAllTextAsync(navMenuFile, navMenuOriginTxt);
            string navMenuExpectedTxt = """
                <MudDrawerHeader>
                    <MudText Typo="Typo.h6">My Application</MudText>
                </MudDrawerHeader>
                <MudNavMenu>
                    <MudNavLink Href="/" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Dashboard">Dashboard</MudNavLink>
                    <MudNavLink Href="counter" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.ExposurePlus1">Counter</MudNavLink>
                    <MudNavLink Href="weather" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Cloud">Weather</MudNavLink>
                    <MudNavLink Href="/servers" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Storage">Servers</MudNavLink>
                    <MudNavGroup Title="Settings" Expanded="true" Icon="@Icons.Material.Filled.Settings">
                        <MudNavLink Href="/users" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.People">Users</MudNavLink>
                        <MudNavLink Href="/security" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Security">Security</MudNavLink>
                    </MudNavGroup>
                    <MudNavLink Href="/about" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Info">About</MudNavLink>
                </MudNavMenu>
                
                """;
            #endregion

            #endregion

            #region 执行命令
            opCmd = """
                ## MudBlazor - 2.初始化菜单(${SlnDir})
                ### 2.1MainLayout初始化布局
                TargetFilePattern: MainLayout.razor$$
                OperationType: Override
                LinePattern:
                Value: @inherits LayoutComponentBase

                <MudThemeProvider @ref="_mudThemeProvider" @bind-IsDarkMode="_isDarkMode" />
                <MudPopoverProvider />
                <MudDialogProvider />
                <MudSnackbarProvider />

                <MudLayout>
                    <MudAppBar>
                        <MudIconButton Icon="@Icons.Material.Filled.Menu" Color="Color.Inherit" Edge="Edge.Start" OnClick="@((e) => DrawerToggle())" />
                        <MudText Typo="Typo.h5" Class="ml-3">My Application</MudText>
                        <MudSpacer />
                        <MudIconButton Icon="@Icons.Material.Filled.MoreVert" Color="Color.Inherit" Edge="Edge.End"/>
                    </MudAppBar>
                    <MudDrawer @bind-Open="@_drawerOpen" Variant="@DrawerVariant.Mini" OpenMiniOnHover="true">
                        <NavMenu/>
                    </MudDrawer>
                    <MudMainContent>
                        <MudContainer MaxWidth="MaxWidth.Medium">
                            @Body
                        </MudContainer>
                    </MudMainContent>
                </MudLayout>
                @code {
                    bool _drawerOpen = true;

                    void DrawerToggle()
                    {
                        _drawerOpen = !_drawerOpen;
                    }
                    private bool _isDarkMode;
                    private MudThemeProvider _mudThemeProvider;

                    protected override async Task OnAfterRenderAsync(bool firstRender)
                    {
                        if (firstRender)
                        {
                            await _mudThemeProvider.WatchSystemPreference(OnSystemDarkModeChanged);
                            StateHasChanged();
                        }
                    }

                    private Task OnSystemDarkModeChanged(bool newValue)
                    {
                        _isDarkMode = newValue;
                        StateHasChanged();
                        return Task.CompletedTask;
                    }
                }

                ### 2.2初始化菜单组件NavMenu
                TargetFilePattern: NavMenu.razor$$
                LinePattern:
                OperationType: Override
                Value: <MudDrawerHeader>
                    <MudText Typo="Typo.h6">My Application</MudText>
                </MudDrawerHeader>
                <MudNavMenu>
                    <MudNavLink Href="/" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Dashboard">Dashboard</MudNavLink>
                    <MudNavLink Href="counter" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.ExposurePlus1">Counter</MudNavLink>
                    <MudNavLink Href="weather" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Cloud">Weather</MudNavLink>
                    <MudNavLink Href="/servers" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Storage">Servers</MudNavLink>
                    <MudNavGroup Title="Settings" Expanded="true" Icon="@Icons.Material.Filled.Settings">
                        <MudNavLink Href="/users" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.People">Users</MudNavLink>
                        <MudNavLink Href="/security" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Security">Security</MudNavLink>
                    </MudNavGroup>
                    <MudNavLink Href="/about" Match="NavLinkMatch.Prefix" Icon="@Icons.Material.Filled.Info">About</MudNavLink>
                </MudNavMenu>{br}
                """;
            opCmd = TmplHelper.ResolveExpressionValue(opCmd, new Dictionary<string, string> { { "SlnDir", testDir } })?.ToString() ?? throw new Exception("模板解析结果为空");
            commandResults = executor([opCmd]);
            await foreach (var commandResult in commandResults)
            {
                outputHelper.WriteLine($"操作完毕: {commandResult.Message}");
            }
            #endregion

            #region 断言
            var mainLayoutResultTxt = await File.ReadAllTextAsync(mainLayoutFile);
            Assert.Equal(mainLayoutResultTxt, mainLayoutExpectedTxt);

            var navMenuResultTxt = await File.ReadAllTextAsync(navMenuFile);
            Assert.Equal(navMenuExpectedTxt.TrimEnd(), navMenuResultTxt.TrimEnd());
            #endregion

            Directory.Delete(testDir, true);
        }

        /// <summary>
        /// 测试换行占位符 {br} 和 {br:N} 的替换功能
        /// </summary>
        [Fact]
        public void BreakPlaceholder_ReplacesCorrectly()
        {
            // {br} 等价于 1 个换行
            Assert.Equal("line1\nline2", SpaceConstants.ReplaceBreakPlaceholders("line1{br}line2"));
            
            // {br:1} 等价于 1 个换行
            Assert.Equal("line1\nline2", SpaceConstants.ReplaceBreakPlaceholders("line1{br:1}line2"));
            
            // {br:2} 等价于 2 个换行（产生一个空行效果）
            Assert.Equal("line1\n\nline2", SpaceConstants.ReplaceBreakPlaceholders("line1{br:2}line2"));
            
            // {br:3} 等价于 3 个换行
            Assert.Equal("line1\n\n\nline2", SpaceConstants.ReplaceBreakPlaceholders("line1{br:3}line2"));
            
            // 空字符串和 null 输入
            Assert.Equal("", SpaceConstants.ReplaceBreakPlaceholders(""));
            Assert.Null(SpaceConstants.ReplaceBreakPlaceholders(null!));
            
            // 无占位符的情况
            Assert.Equal("no placeholders here", SpaceConstants.ReplaceBreakPlaceholders("no placeholders here"));
        }

        /// <summary>
        /// 测试空格和换行占位符混合使用
        /// </summary>
        [Fact]
        public void SpaceAndBreakPlaceholders_MixedUsage()
        {
            // 先替换空格占位符，再替换换行占位符
            string input = "{sp:4}line1{br}{sp:4}line2";
            string afterSpace = SpaceConstants.ReplaceSpacePlaceholders(input);
            string afterBreak = SpaceConstants.ReplaceBreakPlaceholders(afterSpace);
            Assert.Equal("    line1\n    line2", afterBreak);
            
            // 多个换行占位符
            input = "a{br}b{br:2}c";
            afterBreak = SpaceConstants.ReplaceBreakPlaceholders(input);
            Assert.Equal("a\nb\n\nc", afterBreak);
        }
    }
}
