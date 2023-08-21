using System.Runtime.InteropServices;
using System.Text;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class SystemHelper
    {
        // 操作系统接口
        // 1. 显示窗口
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);

        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        [DllImport("user32.dll")]
        static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        // 枚举所有窗口
        delegate bool EnumWindowsCallback(IntPtr hWnd, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsCallback lpEnumFunc, IntPtr lParam);
        
        // 找到某一个窗口
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindowW(string? lpClassName, string? lpWindowName);

        // 发送消息
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder lpWindowText, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        [DllImport("user32.dll")]
        static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [DllImport("user32.dll")]
        static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int lpdwProcessId);

        [DllImport("user32.dll")]
        static extern bool IsIconic(IntPtr hWnd);

        // ShowWindow 函数中用到的常量值
        static int SW_HIDE => 0;
        static int SW_SHOWNORMAL => 1;
        static int SW_SHOWMINIMIZED => 2;
        static int SW_SHOWMAXIMIZED => 3;
        static int SW_SHOWNOACTIVATE => 4;
        static int SW_SHOW => 5;
        static int SW_MINIMIZE => 6;
        static int SW_SHOWMINNOACTIVE => 7;
        static int SW_SHOWNA => 8;
        static int SW_RESTORE => 9;
        // 发送还原窗口消息给系统托盘图标
        static int WM_LBUTTONDOWN => 0x0201;
        static int WM_LBUTTONUP => 0x0202;

        static readonly List<WindowInfo> _windowsList = new();
        public static List<WindowInfo> GetAllWindows()
        {
            static bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam)
            {
                // 获取窗口标题
                StringBuilder titleBuilder = new(256);
                _ = GetWindowText(hWnd, titleBuilder, titleBuilder.Capacity);
                string windowTitle = titleBuilder.ToString();

                // 检查窗口是否可见
                bool isVisible = IsWindowVisible(hWnd);

                // 排除无标题或者不可见的窗体
                if (string.IsNullOrWhiteSpace(windowTitle) || !isVisible)
                {
                    return true;
                }

                // 获取窗口类名
                StringBuilder classNameBuilder = new(256);
                _ = GetClassName(hWnd, classNameBuilder, classNameBuilder.Capacity);
                string className = classNameBuilder.ToString();
                // 获取窗口矩形区域
                _ = GetWindowRect(hWnd, out RECT windowRect);

                // 获取窗口位置和状态信息
                WINDOWPLACEMENT placement = new();
                placement.length = Marshal.SizeOf(placement);
                _ = GetWindowPlacement(hWnd, ref placement);

                // 获取窗口所属的线程和进程标识符
                _ = GetWindowThreadProcessId(hWnd, out int processId);

                bool isIconic = IsIconic(hWnd);

                if (!string.IsNullOrWhiteSpace(windowTitle))
                {
                    WindowInfo windowInfo = new()
                    {
                        Handle = hWnd,
                        Title = windowTitle,
                        ClassName = className,
                        Rect = windowRect,
                        Placement = placement,
                        IsVisiable = isVisible,
                        ProcessId = processId,
                        IsIconic = isIconic
                    };
                    _windowsList.Add(windowInfo);
                }
                return true;
            }
            

            EnumWindows(EnumWindowsProc, IntPtr.Zero);

            return _windowsList;
        }

        public static bool ShowWindow(IntPtr hwnd) => ShowWindow(hwnd, SW_RESTORE);
        public static bool ShowWindowAsync(IntPtr hwnd) => ShowWindowAsync(hwnd, SW_RESTORE);
        public static bool RestoreWindowFromTray(IntPtr handle)
        {
            var r1 = SendMessage(handle, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
            var r2 = SendMessage(handle, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
            Console.WriteLine($"r1: {r1}; r2: {r2}");
            return r1 + r2 > 0;
        }
        /// <summary>
        /// 通过标题找窗口
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static IntPtr FindWindowByTitle(string title) => FindWindowW(null, title);
        /// <summary>
        /// 通过ClassName找窗口
        /// </summary>
        /// <param name="classname"></param>
        /// <returns></returns>
        public static IntPtr FindWindowByClassName(string classname) => FindWindowW(classname, null);
    }
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public RECT Rect { get; set; }
        public WINDOWPLACEMENT Placement { get; set; }
        public bool IsVisiable { get; set; }
        public int ProcessId { get; set; }
        public bool IsIconic { get; set; }
        public override string ToString() => $"Title: {Title}; IsIconic: {IsIconic}; Handle: {Handle}; Visiable: {IsVisiable}; React: {Rect}; Placement: {Placement}; ClassName: {ClassName}; ProcessId: {ProcessId};";
    }
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
        public override string ToString()
        {
            return $"{Left} {Top} {Right} {Bottom}";
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public int length;
        public int flags;
        public int showCmd;
        public POINT minPosition;
        public POINT maxPosition;
        public RECT normalPosition;
        public override string ToString()
        {
            return $"length:{length},flags:{flags},showCmd:{showCmd},minPosition:{minPosition},maxPosition:{maxPosition},normalPosition:{normalPosition}";
        }
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;
        public override string ToString() => $"[{X}, {Y}]";
    }
}
