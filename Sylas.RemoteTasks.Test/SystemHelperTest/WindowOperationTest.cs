using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sylas.RemoteTasks.App.Utils;
using Xunit.Abstractions;

namespace Sylas.RemoteTasks.Test.SystemHelperTest
{
    public partial class WindowOperationTest : IClassFixture<TestFixture>
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly IConfiguration _configuration;

        public WindowOperationTest(ITestOutputHelper outputHelper, TestFixture fixture)
        {
            _outputHelper = outputHelper;
            _configuration = fixture.ServiceProvider.GetRequiredService<IConfiguration>();
        }
        [Fact]
        public void ShowAllWindowsTest()
        {
            //1: Sylas.RemoteTasks - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; 4691d0c0 - 9370 - 41ea - 9bc9 - 04968dae3fbc], 658074, False
            //2: whitebox.com - 远程桌面连接, TscShellContainerClass, 788622, False
            //3: 调用 windows api显示隐藏最小化到托盘的窗口_百度搜索 和另外 17 个页面 - 个人 - Microsoft​ Edge, Chrome_WidgetWin_1, 67848, False
            //4: 任务管理器, TaskManagerWindow, 12588786, True
            //5: solong - Xshell 6, Xshell6::MainFrame_0, 9775264, True
            //6: SystemProgram.dib - AirGo(Workspace) - Visual Studio Code, Chrome_WidgetWin_1, 395700, False
            //7: docker - compose.yml - crontabs - code - server 和另外 17 个页面 - 个人 - Microsoft​ Edge, Chrome_WidgetWin_1, 67940, False
            //8: iduo.IdentityServer4.Admin - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; 8e5e801f - 4671 - 4ba9 - 8835 - 5611d1942694], 1314604, False
            //9: iduo.my.190 - WindTerm, Qt5152QWindowIcon, 11150236, True
            //10: Windows 输入体验, Windows.UI.Core.CoreWindow, 1516680, False
            //11: Steps - My Workspace, Chrome_WidgetWin_1, 7276730, True
            //12: [Extension Development Host] 设置参数 - BlazorWasmApp - Visual Studio Code, Chrome_WidgetWin_1, 4067908, True
            //13: 选择 C:\Windows\System32\cmd.exe, ConsoleWindowClass, 11284332, True
            //14: C:\Windows\System32\cmd.exe, ConsoleWindowClass, 1845648, True
            //15: MINGW64:/ d /.NET / my / is4 / IdentityServer4.Admin, mintty, 2233660, True
            //16: C:\Windows\System32\cmd.exe, ConsoleWindowClass, 3680782, True
            //17: 选择 Windows PowerShell, ConsoleWindowClass, 1512296, True
            //18: C:\Windows\System32\cmd.exe, ConsoleWindowClass, 471632, True
            //19: Microsoft Visual Studio 调试控制台, ConsoleWindowClass, 3410648, True
            //20: Progress Telerik Fiddler Classic, WindowsForms10.Window.8.app.0.13965fa_r6_ad1, 1381298, True
            //21: iduo.form - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; c865480a - 9e0a - 4179 - b996 - 28cea218df7d], 462854, True
            //22: Microsoft Visual Studio 调试控制台, ConsoleWindowClass, 9122508, True
            //23: Microsoft Visual Studio 调试控制台, ConsoleWindowClass, 6893984, True
            //24: net6.0 - root@192.168.1.229 - WinSCP, TScpCommanderForm, 271438, True
            //25: Microsoft Visual Studio 调试控制台, ConsoleWindowClass, 144906, True
            //26: 选择 Windows PowerShell, ConsoleWindowClass, 21246406, True
            //27: iduo.portal - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; f69c8919 - 9145 - 4bdd - a8eb - c82d79e8213d], 1508072, True
            //28: Microsoft Visual Studio 调试控制台, ConsoleWindowClass, 2962682, True
            //29: Windows PowerShell, ConsoleWindowClass, 5245080, True
            //30: Iduo.IdentityServer4.Admin - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; 4de20a62 - 3615 - 48aa - a980 - fd66643d5dfa], 463178, True
            //31: iduo.SiteManagement - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; 2ed9a7a8 - 671d - 4fc6 - a3f7 - 78e69a579c97], 1777504, True
            //32: 有道云笔记, Chrome_WidgetWin_1, 987700, True
            //33: Navicat Premium, TNavicatMainForm, 5376376, True
            //34: iduo.application - Microsoft Visual Studio, HwndWrapper[DefaultDomain; ; 6ee59405 - f5e0 - 4301 - 9db3 - 0b7f8a08466c], 6819494, True
            //35: LINQPad 7, WindowsForms10.Window.8.app.0.265601d_r3_ad1, 4525316, True
            //36: 钉钉, StandardFrame_DingTalk, 198488, True
            //37: Program Manager, Progman, 16714742, False
            var windows = SystemHelper.GetAllWindows();
            int i = 1;
            foreach (var window in windows)
            {
                _outputHelper.WriteLine($"{i++}: {window.Title}, {window.ClassName}, {window.Handle}, {window.IsIconic}");
            }
        }

        [Fact]
        public void ShowWindowNotInTrayQtScrcpy()
        {
            //var res = SystemHelper.RestoreWindowFromTray(21628630);
            //SystemHelper.SetForegroundWindow(21628630);
            var qtScrcpyHanle = SystemHelper.FindWindowByTitle("QtScrcpy");
            _outputHelper.WriteLine(qtScrcpyHanle.ToString());
            SystemHelper.ShowWindowAsync(qtScrcpyHanle);
        }
    }
}
