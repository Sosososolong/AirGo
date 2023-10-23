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
        public async Task GetForegroundWindow()
        {
            // 启动测试后, 给2秒钟的时间将测试窗体置顶并处于焦点激活状态
            await Task.Delay(2000);
            (IntPtr hWnd, string title) = SystemHelper.GetForegroundWindowHandlerAndTitle();
            _outputHelper.WriteLine($"Title: {title}; Handler: {hWnd}");

            await Task.Delay(1 * 1000);
            _outputHelper.WriteLine(SystemHelper.ShowWindowInTray(hWnd).ToString());
            await Task.Delay(1 * 1000);
            _outputHelper.WriteLine(SystemHelper.ShowWindow(hWnd).ToString());
        }
    }
}
