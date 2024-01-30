using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Sylas.RemoteTasks.Utils
{
    /// <summary>
    /// 系统接口
    /// </summary>
    public static class SystemHelper
    {
        #region 键盘代码

        /// <summary>
        /// 修饰键ALT
        /// </summary>
        public const int MOD_ALT = 1;
        /// <summary>
        /// 修饰键CONTROL
        /// </summary>
        public const int MOD_CONTROL = 2;
        /// <summary>
        /// 修饰键SHIFT
        /// </summary>
        public const int MOD_SHIFT = 4;
        /// <summary>
        /// 修饰键WIN
        /// </summary>
        public const int MOD_WIN = 8;

        /// <summary>
        /// 键盘 - BACK键
        /// </summary>
        public const uint VK_BACK = 8;
        /// <summary>
        /// 键盘 - TAB键
        /// </summary>
        public const uint VK_TAB = 9;
        /// <summary>
        /// 键盘 - CLEAR键
        /// </summary>
        public const uint VK_CLEAR = 12;
        /// <summary>
        /// 键盘 - RETURN键
        /// </summary>
        public const uint VK_RETURN = 13;
        /// <summary>
        /// 键盘 - SHIFT键
        /// </summary>
        public const uint VK_SHIFT = 16;
        /// <summary>
        /// 键盘 - CONTROL键
        /// </summary>
        public const uint VK_CONTROL = 17;
        /// <summary>
        /// 键盘 - MENU键
        /// </summary>
        public const uint VK_MENU = 18;
        /// <summary>
        /// 键盘 - PAUSE键
        /// </summary>
        public const uint VK_PAUSE = 19;
        /// <summary>
        /// 键盘 - CAPITAL键
        /// </summary>
        public const uint VK_CAPITAL = 20;
        /// <summary>
        /// 键盘 - KANA键
        /// </summary>
        public const uint VK_KANA = 0x15;
        /// <summary>
        /// 键盘 - HANGEUL键
        /// </summary>
        public const uint VK_HANGEUL = 0x15;
        /// <summary>
        /// 键盘 - HANGUL键
        /// </summary>
        public const uint VK_HANGUL = 0x15;
        /// <summary>
        /// 键盘 - JUNJA键
        /// </summary>
        public const uint VK_JUNJA = 0x17;
        /// <summary>
        /// 键盘 - FINAL键
        /// </summary>
        public const uint VK_FINAL = 0x18;
        /// <summary>
        /// 键盘 - HANJA键
        /// </summary>
        public const uint VK_HANJA = 0x19;
        /// <summary>
        /// 键盘 - KANJI键
        /// </summary>
        public const uint VK_KANJI = 0x19;
        /// <summary>
        /// 键盘 - ESCAPE键
        /// </summary>
        public const uint VK_ESCAPE = 0x1B;
        /// <summary>
        /// 键盘 - CONVERT键
        /// </summary>
        public const uint VK_CONVERT = 0x1C;
        /// <summary>
        /// 键盘 - NONCONVERT键
        /// </summary>
        public const uint VK_NONCONVERT = 0x1D;
        /// <summary>
        /// 键盘 - ACCEPT键
        /// </summary>
        public const uint VK_ACCEPT = 0x1E;
        /// <summary>
        /// 键盘 - MODECHANGE键
        /// </summary>
        public const uint VK_MODECHANGE = 0x1F;
        /// <summary>
        /// 键盘 - SPACE键
        /// </summary>
        public const uint VK_SPACE = 32;
        /// <summary>
        /// 键盘 - PRIOR键
        /// </summary>
        public const uint VK_PRIOR = 33;
        /// <summary>
        /// 键盘 - NEXT键
        /// </summary>
        public const uint VK_NEXT = 34;
        /// <summary>
        /// 键盘 - END键
        /// </summary>
        public const uint VK_END = 35;
        /// <summary>
        /// 键盘 - HOME键
        /// </summary>
        public const uint VK_HOME = 36;
        /// <summary>
        /// 键盘 - LEFT键
        /// </summary>
        public const uint VK_LEFT = 37;
        /// <summary>
        /// 键盘 - UP键
        /// </summary>
        public const uint VK_UP = 38;
        /// <summary>
        /// 键盘 - RIGHT键
        /// </summary>
        public const uint VK_RIGHT = 39;
        /// <summary>
        /// 键盘 - DOWN键
        /// </summary>
        public const uint VK_DOWN = 40;
        /// <summary>
        /// 键盘 - SELECT键
        /// </summary>
        public const uint VK_SELECT = 41;
        /// <summary>
        /// 键盘 - PRINT键
        /// </summary>
        public const uint VK_PRINT = 42;
        /// <summary>
        /// 键盘 - EXECUTE键
        /// </summary>
        public const uint VK_EXECUTE = 43;
        /// <summary>
        /// 键盘 - SNAPSHOT键
        /// </summary>
        public const uint VK_SNAPSHOT = 44;
        /// <summary>
        /// 键盘 - INSERT键
        /// </summary>
        public const uint VK_INSERT = 45;
        /// <summary>
        /// 键盘 - DELETE键
        /// </summary>
        public const uint VK_DELETE = 46;
        /// <summary>
        /// 键盘 - HELP键
        /// </summary>
        public const uint VK_HELP = 47;
        /// <summary>
        /// 键盘 - LWIN键
        /// </summary>
        public const uint VK_LWIN = 0x5B;
        /// <summary>
        /// 键盘 - RWIN键
        /// </summary>
        public const uint VK_RWIN = 0x5C;
        /// <summary>
        /// 键盘 - APPS键
        /// </summary>
        public const uint VK_APPS = 0x5D;
        /// <summary>
        /// 键盘 - SLEEP键
        /// </summary>
        public const uint VK_SLEEP = 0x5F;
        /// <summary>
        /// 键盘 - NUMPAD0键
        /// </summary>
        public const uint VK_NUMPAD0 = 0x60;
        /// <summary>
        /// 键盘 - NUMPAD1键
        /// </summary>
        public const uint VK_NUMPAD1 = 0x61;
        /// <summary>
        /// 键盘 - NUMPAD2键
        /// </summary>
        public const uint VK_NUMPAD2 = 0x62;
        /// <summary>
        /// 键盘 - NUMPAD3键
        /// </summary>
        public const uint VK_NUMPAD3 = 0x63;
        /// <summary>
        /// 键盘 - NUMPAD4键
        /// </summary>
        public const uint VK_NUMPAD4 = 0x64;
        /// <summary>
        /// 键盘 - NUMPAD5键
        /// </summary>
        public const uint VK_NUMPAD5 = 0x65;
        /// <summary>
        /// 键盘 - NUMPAD6键
        /// </summary>
        public const uint VK_NUMPAD6 = 0x66;
        /// <summary>
        /// 键盘 - NUMPAD7键
        /// </summary>
        public const uint VK_NUMPAD7 = 0x67;
        /// <summary>
        /// 键盘 - NUMPAD8键
        /// </summary>
        public const uint VK_NUMPAD8 = 0x68;
        /// <summary>
        /// 键盘 - NUMPAD9键
        /// </summary>
        public const uint VK_NUMPAD9 = 0x69;
        /// <summary>
        /// 键盘 - MULTIPLY键
        /// </summary>
        public const uint VK_MULTIPLY = 0x6A;
        /// <summary>
        /// 键盘 - ADD键
        /// </summary>
        public const uint VK_ADD = 0x6B;
        /// <summary>
        /// 键盘 - SEPARATOR键
        /// </summary>
        public const uint VK_SEPARATOR = 0x6C;
        /// <summary>
        /// 键盘 - SUBTRACT键
        /// </summary>
        public const uint VK_SUBTRACT = 0x6D;
        /// <summary>
        /// 键盘 - DECIMAL键
        /// </summary>
        public const uint VK_DECIMAL = 0x6E;
        /// <summary>
        /// 键盘 - DIVIDE键
        /// </summary>
        public const uint VK_DIVIDE = 0x6F;
        /// <summary>
        /// 键盘 - F1键
        /// </summary>
        public const uint VK_F1 = 0x70;
        /// <summary>
        /// 键盘 - F2键
        /// </summary>
        public const uint VK_F2 = 0x71;
        /// <summary>
        /// 键盘 - F3键
        /// </summary>
        public const uint VK_F3 = 0x72;
        /// <summary>
        /// 键盘 - F4键
        /// </summary>
        public const uint VK_F4 = 0x73;
        /// <summary>
        /// 键盘 - F5键
        /// </summary>
        public const uint VK_F5 = 0x74;
        /// <summary>
        /// 键盘 - F6键
        /// </summary>
        public const uint VK_F6 = 0x75;
        /// <summary>
        /// 键盘 - F7键
        /// </summary>
        public const uint VK_F7 = 0x76;
        /// <summary>
        /// 键盘 - F8键
        /// </summary>
        public const uint VK_F8 = 0x77;
        /// <summary>
        /// 键盘 - F9键
        /// </summary>
        public const uint VK_F9 = 0x78;
        /// <summary>
        /// 键盘 - F10键
        /// </summary>
        public const uint VK_F10 = 0x79;
        /// <summary>
        /// 键盘 - F11键
        /// </summary>
        public const uint VK_F11 = 0x7A;
        /// <summary>
        /// 键盘 - F12键
        /// </summary>
        public const uint VK_F12 = 0x7B;
        /// <summary>
        /// 键盘 - F13键
        /// </summary>
        public const uint VK_F13 = 0x7C;
        /// <summary>
        /// 键盘 - F14键
        /// </summary>
        public const uint VK_F14 = 0x7D;
        /// <summary>
        /// 键盘 - F15键
        /// </summary>
        public const uint VK_F15 = 0x7E;
        /// <summary>
        /// 键盘 - F16键
        /// </summary>
        public const uint VK_F16 = 0x7F;
        /// <summary>
        /// 键盘 - F17键
        /// </summary>
        public const uint VK_F17 = 0x80;
        /// <summary>
        /// 键盘 - F18键
        /// </summary>
        public const uint VK_F18 = 0x81;
        /// <summary>
        /// 键盘 - F19键
        /// </summary>
        public const uint VK_F19 = 0x82;
        /// <summary>
        /// 键盘 - F20键
        /// </summary>
        public const uint VK_F20 = 0x83;
        /// <summary>
        /// 键盘 - F21键
        /// </summary>
        public const uint VK_F21 = 0x84;
        /// <summary>
        /// 键盘 - F22键
        /// </summary>
        public const uint VK_F22 = 0x85;
        /// <summary>
        /// 键盘 - F23键
        /// </summary>
        public const uint VK_F23 = 0x86;
        /// <summary>
        /// 键盘 - F24键
        /// </summary>
        public const uint VK_F24 = 0x87;
        /// <summary>
        /// 键盘 - NUMLOCK键
        /// </summary>
        public const uint VK_NUMLOCK = 0x90;
        /// <summary>
        /// 键盘 - SCROLL键
        /// </summary>
        public const uint VK_SCROLL = 0x91;
        /// <summary>
        /// 键盘 - LSHIFT键
        /// </summary>
        public const uint VK_LSHIFT = 0xA0;
        /// <summary>
        /// 键盘 - RSHIFT键
        /// </summary>
        public const uint VK_RSHIFT = 0xA1;
        /// <summary>
        /// 键盘 - LCONTROL键
        /// </summary>
        public const uint VK_LCONTROL = 0xA2;
        /// <summary>
        /// 键盘 - RCONTROL键
        /// </summary>
        public const uint VK_RCONTROL = 0xA3;
        /// <summary>
        /// 键盘 - LMENU键
        /// </summary>
        public const uint VK_LMENU = 0xA4;
        /// <summary>
        /// 键盘 - RMENU键
        /// </summary>
        public const uint VK_RMENU = 0xA5;

        /// <summary>
        /// 键盘 - OEM_1键
        /// </summary>
        public const uint VK_OEM_1 = 0xBA;
        /// <summary>
        /// 键盘 - OEM_2键
        /// </summary>
        public const uint VK_OEM_2 = 0xBF;
        /// <summary>
        /// 键盘 - OEM_3键
        /// </summary>
        public const uint VK_OEM_3 = 0xC0;
        /// <summary>
        /// 键盘 - OEM_4键
        /// </summary>
        public const uint VK_OEM_4 = 0xDB;
        /// <summary>
        /// 键盘 - OEM_5键
        /// </summary>
        public const uint VK_OEM_5 = 0xDC;
        /// <summary>
        /// 键盘 - OEM_6键
        /// </summary>
        public const uint VK_OEM_6 = 0xDD;
        /// <summary>
        /// 键盘 - OEM_7键
        /// </summary>
        public const uint VK_OEM_7 = 0xDE;
        /// <summary>
        /// 键盘 - OEM_8键
        /// </summary>
        public const uint VK_OEM_8 = 0xDF;

        /// <summary>
        /// 键盘 - PROCESSKEY键
        /// </summary>
        public const uint VK_PROCESSKEY = 0xE5;

        /// <summary>
        /// 键盘 - ATTN键
        /// </summary>
        public const uint VK_ATTN = 0xF6;
        /// <summary>
        /// 键盘 - CRSEL键
        /// </summary>
        public const uint VK_CRSEL = 0xF7;
        /// <summary>
        /// 键盘 - EXSEL键
        /// </summary>
        public const uint VK_EXSEL = 0xF8;
        /// <summary>
        /// 键盘 - EREOF键
        /// </summary>
        public const uint VK_EREOF = 0xF9;
        /// <summary>
        /// 键盘 - PLAY键
        /// </summary>
        public const uint VK_PLAY = 0xFA;
        /// <summary>
        /// 键盘 - ZOOM键
        /// </summary>
        public const uint VK_ZOOM = 0xFB;
        /// <summary>
        /// 键盘 - NONAME键
        /// </summary>
        public const uint VK_NONAME = 0xFC;
        /// <summary>
        /// 键盘 - PA1键
        /// </summary>
        public const uint VK_PA1 = 0xFD;
        /// <summary>
        /// 键盘 - OEM_CLEAR键
        /// </summary>
        public const uint VK_OEM_CLEAR = 0xFE;
        /// <summary>
        /// TME_HOVER
        /// </summary>
        public const int TME_HOVER = 1;
        /// <summary>
        /// TME_LEAVE
        /// </summary>
        public const int TME_LEAVE = 2;
        /// <summary>
        /// TME_QUERY
        /// </summary>
        public const int TME_QUERY = 0x40000000;
        /// <summary>
        /// TME_CANCEL
        /// </summary>
        public const uint TME_CANCEL = 0x80000000;
        /// <summary>
        /// HOVER_DEFAULT
        /// </summary>
        public const uint HOVER_DEFAULT = 0xFFFFFFFF;
        /// <summary>
        /// MK_LBUTTON
        /// </summary>
        public const int MK_LBUTTON = 1;
        /// <summary>
        /// MK_RBUTTON
        /// </summary>
        public const int MK_RBUTTON = 2;
        /// <summary>
        /// MK_SHIFT
        /// </summary>
        public const int MK_SHIFT = 4;
        /// <summary>
        /// MK_CONTROL
        /// </summary>
        public const int MK_CONTROL = 8;
        /// <summary>
        /// MK_MBUTTON
        /// </summary>
        public const int MK_MBUTTON = 16;
        #endregion

        #region 操作系统接口 - 注册全局热键(需创建窗体用于接收热键消息)
        delegate IntPtr WndProcDelegate(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [StructLayout(LayoutKind.Sequential)]
        struct WNDCLASS
        {
            public uint style;
            public WndProcDelegate lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string lpszMenuName;
            public string lpszClassName;
        }
        [DllImport("user32.dll")]
        static extern int PostQuitMessage(int nExitCode);
        [DllImport("user32.dll")]
        static extern int GetMessage(out MSG lpMsg, IntPtr hWnd, uint wMsgFilterMin, uint wMsgFilterMax);

        [DllImport("user32.dll")]
        static extern bool TranslateMessage(ref MSG lpMsg);

        [DllImport("user32.dll")]
        static extern IntPtr DispatchMessage(ref MSG lpMsg);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate IntPtr WNDPROC(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern bool RegisterClass(ref WNDCLASS lpWndClass);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool UnregisterClass(string lpClassName, IntPtr hInstance);
        [DllImport("kernel32.dll")]
        static extern IntPtr GetModuleHandle(string? lpModuleName);
        /// <summary>
        /// 创建窗体
        /// </summary>
        /// <param name="dwExStyle">指定窗口的扩展样式</param>
        /// <param name="lpClassName">指定窗口类的名称</param>
        /// <param name="lpWindowName">指定窗口的标题</param>
        /// <param name="dwStyle">指定窗口的样式</param>
        /// <param name="x">指定窗口左上角的x坐标</param>
        /// <param name="y">指定窗口左上角的y坐标</param>
        /// <param name="width">指定窗口的宽度</param>
        /// <param name="height">指定窗口的高度</param>
        /// <param name="hWndParent">指定窗口的父窗口句柄</param>
        /// <param name="hMenu">指定窗口菜单句柄</param>
        /// <param name="hInstance">指定应用程序的实例句柄</param>
        /// <param name="lpParam">指定传递给窗口的附加数据</param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName, string lpWindowName, uint dwStyle, int x, int y, int width, int height, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);
        /// <summary>
        /// 注册全局热键
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="id"></param>
        /// <param name="mk"></param>
        /// <param name="vk"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        static extern bool RegisterHotKey(IntPtr hWnd, int id, int mk, uint vk);
        /// <summary>
        /// 注销全局热键
        /// </summary>
        /// <param name="hWnd"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        // Unregisters the hot key with Windows.
        [DllImport("user32.dll")]
        static extern bool UnregisterHotKey(IntPtr hWnd, int id);
        #endregion

        #region  操作系统接口 - 查找操作窗体
        // 显示窗口
        [DllImport("user32.dll")]
        static extern bool ShowWindow(IntPtr hwnd, int nCmdShow);
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

        [DllImport("user32.dll")]
        static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowTextLength(IntPtr hWnd);
        /// <summary>
        /// 将窗体前置
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
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
        /// <summary>
        /// 窗体被隐藏
        /// </summary>
        static int SW_HIDE => 0;
        /// <summary>
        /// 正常状态, 不是最小化也不是最大化
        /// </summary>
        static int SW_SHOWNORMAL => 1;
        /// <summary>
        /// 最小化托盘模式
        /// </summary>
        static int SW_SHOWMINIMIZED => 2;
        /// <summary>
        /// 最大化
        /// </summary>
        static int SW_SHOWMAXIMIZED => 3;
        /// <summary>
        /// 正常状态, 但是不激活焦点
        /// </summary>
        static int SW_SHOWNOACTIVATE => 4;
        /// <summary>
        /// 窗体被显示, 同时激活焦点
        /// </summary>
        static int SW_SHOW => 5;
        /// <summary>
        /// 窗体被最小化
        /// </summary>
        static int SW_MINIMIZE => 6;
        /// <summary>
        /// 窗体被最小化为托盘模式，但不激活焦点
        /// </summary>
        static int SW_SHOWMINNOACTIVE => 7;
        /// <summary>
        /// 窗体被显示，但不激活焦点
        /// </summary>
        static int SW_SHOWNA => 8;
        /// <summary>
        /// 窗体被还原到之前的状态
        /// </summary>
        static int SW_RESTORE => 9;
        /// <summary>
        /// 根据窗体的显示属性，显示窗体
        /// </summary>
        static int SW_SHOWDEFAULT => 10;
        // 发送还原窗口消息给系统托盘图标
        static int WM_LBUTTONDOWN => 0x0201;
        static int WM_LBUTTONUP => 0x0202;

        static readonly List<WindowInfo> _windowsList = [];
        #endregion

        #region 消息代码
        /// <summary>
        /// 表示有热键被触发
        /// </summary>
        public const int WM_HOTKEY = 0x0312;
        /// <summary>
        /// 销毁消息
        /// </summary>
        public const int WM_DESTROY = 0x0002;
        #endregion

        #region 封装操作系统操作窗体相关接口
        /// <summary>
        /// 销毁窗体
        /// </summary>
        /// <param name="hWnd"></param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int DestroyWindow(IntPtr hWnd);
        /// <summary>
        /// 获取所有可见的有标题的窗体
        /// </summary>
        /// <returns></returns>
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
        /// <summary>
        /// 获取当前处于激活焦点的窗体的句柄和标题
        /// </summary>
        /// <returns></returns>
        public static (IntPtr, string) GetForegroundWindowHandlerAndTitle()
        {
            IntPtr hWnd = GetForegroundWindow();
            int length = GetWindowTextLength(hWnd);
            StringBuilder caption = new();

            if (length > 0)
            {
                _ = GetWindowText(hWnd, caption, length);
            }
            return (hWnd, caption.ToString());
        }
        /// <summary>
        /// 根据窗体句柄获取窗体状态信息
        /// </summary>
        /// <param name="hWnd">窗体句柄</param>
        /// <returns></returns>
        public static WINDOWPLACEMENT GetWindowStatus(IntPtr hWnd)
        {
            WINDOWPLACEMENT placement = new();
            placement.length = Marshal.SizeOf(placement);
            GetWindowPlacement(hWnd, ref placement);

            return placement;
        }

        /// <summary>
        /// 根据窗体标题获取窗体状态信息
        /// </summary>
        /// <param name="title">窗体标题</param>
        /// <returns></returns>
        public static WINDOWPLACEMENT GetWindowStatus(string title)
        {
            var hWnd = FindWindowByTitle(title);
            return GetWindowStatus(hWnd);
        }
        /// <summary>
        /// 根据窗体句柄显示窗体
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static bool ShowWindow(IntPtr hwnd)
        {
            return ShowWindow(hwnd, SW_SHOWDEFAULT);
        }
        /// <summary>
        /// 根据窗体标题显示窗体
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static bool ShowWindow(string title)
        {
            var hwnd = FindWindowByTitle(title);
            return ShowWindow(hwnd, SW_SHOWDEFAULT);
        }
        /// <summary>
        /// 异步方式根据窗体句柄显示窗体
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static bool ShowWindowAsync(IntPtr hwnd)
        {
            return ShowWindowAsync(hwnd, SW_RESTORE);
        }
        /// <summary>
        /// 异步方式根据窗体标题显示窗体
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public static bool ShowWindowAsync(string title)
        {
            var hwnd = FindWindowByTitle(title);
            return ShowWindowAsync(hwnd, SW_RESTORE);
        }
        /// <summary>
        /// 从托盘显示窗体
        /// </summary>
        /// <param name="handle"></param>
        /// <returns></returns>
        public static bool RestoreWindowFromTray(IntPtr handle)
        {
            var r1 = SendMessage(handle, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
            var r2 = SendMessage(handle, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
            Console.WriteLine($"r1: {r1}; r2: {r2}");
            return Convert.ToInt32(r1) + Convert.ToInt32(r2) > 0;
        }
        /// <summary>
        /// 最小化到托盘
        /// </summary>
        /// <param name="hwnd"></param>
        /// <returns></returns>
        public static bool ShowWindowInTray(IntPtr hwnd)
        {
            return ShowWindow(hwnd, SW_SHOWMINIMIZED);
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

        static int GetMkValue(string mk) => mk.ToLower() switch
        {
            "ctrl" => MOD_CONTROL,
            "shift" => MOD_SHIFT,
            "alt" => MOD_ALT,
            "win" => MOD_WIN,
            _ => throw new Exception("无效的修饰键"),
        };
        static uint GetVkValue(string vk) => vk.ToLower() switch
        {
            "f1" => VK_F1,
            "f2" => VK_F2,
            "f3" => VK_F3,
            "f4" => VK_F4,
            "f5" => VK_F5,
            "f6" => VK_F6,
            "f7" => VK_F7,
            "f8" => VK_F8,
            "f9" => VK_F9,
            "f10" => VK_F10,
            "f11" => VK_F11,
            "f12" => VK_F12,
            "f13" => VK_F13,
            "0" => VK_NUMPAD0,
            "1" => VK_NUMPAD1,
            "2" => VK_NUMPAD2,
            "3" => VK_NUMPAD3,
            "4" => VK_NUMPAD4,
            "5" => VK_NUMPAD5,
            "6" => VK_NUMPAD6,
            "7" => VK_NUMPAD7,
            "8" => VK_NUMPAD8,
            "9" => VK_NUMPAD9,
            "home" => VK_HOME,
            "end" => VK_END,
            "space" => VK_SPACE,
            _ => Convert.ToChar(vk.ToUpper())
        };

        static IntPtr WindowProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            Console.WriteLine($"WindowProc uMsg: {uMsg}; wParam(HotKeyId): {wParam}");
            switch (uMsg)
            {
                case WM_HOTKEY:
                    var hotKey = _globalHotKeys.FirstOrDefault(x => x.Id == Convert.ToInt32(wParam));
                    if (hotKey is not null)
                    {
                        Console.WriteLine($"全局热键[{wParam}]被触发: {hotKey}");
                    }
                    else
                    {
                        Console.WriteLine($"未找到热键{wParam}");
                    }
                    break;

                case WM_DESTROY:
                    Console.WriteLine("Destroy msg");
                    _ = PostQuitMessage(0);
                    break;

                default:
                    return DefWindowProc(hwnd, uMsg, wParam, lParam);
            }

            return IntPtr.Zero;
        }
        /// <summary>
        /// 全局热键
        /// </summary>
        public static List<GlobalHotKey> _globalHotKeys = new();
        /// <summary>
        /// 注册全局热键
        /// </summary>
        /// <param name="globalHotKeys"></param>
        public static void RegisterGlobalHotKey(List<GlobalHotKey> globalHotKeys)
        {
            Task.Run(() =>
            {
                IntPtr hwnd;
                IntPtr hInstance;
                int bRet;

                // 创建一个隐藏的窗口
                hInstance = GetModuleHandle(null);
                WNDCLASS wc = new()
                {
                    lpfnWndProc = WindowProc,
                    hInstance = hInstance,
                    lpszClassName = $"HotkeyWindow"
                };
                RegisterClass(ref wc);
                hwnd = CreateWindowEx(0, wc.lpszClassName, "Z", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

                // 去重
                List<GlobalHotKey> ghks = [];
                foreach (var ghk in globalHotKeys)
                {
                    if (ghks.Any(x => x.Id == ghk.Id && x.VmKey == ghk.VmKey))
                    {
                        ghks.Add(ghk);
                    }
                }

                _globalHotKeys = ghks;
                foreach (var globalHotKey in globalHotKeys)
                {
                    int hotkeyMk = 0;
                    foreach (var mk in globalHotKey.Modifiers)
                    {
                        int mkValue = GetMkValue(mk);
                        if (hotkeyMk == 0)
                        {
                            hotkeyMk = mkValue;
                        }
                        else
                        {
                            hotkeyMk |= mkValue;
                        }
                    }
                    uint hotkeyVk = GetVkValue(globalHotKey.VmKey);

                    // 注册全局热键
                    if (!RegisterHotKey(hwnd, globalHotKey.Id, hotkeyMk, hotkeyVk))
                    {
                        Console.WriteLine($"无法注册全局热键 {globalHotKey}");
                        return;
                    }

                    Console.WriteLine($"成功注册热键{globalHotKey}[{globalHotKey.Id}]: {hwnd} - {wc.lpszClassName}; Thread: {Environment.CurrentManagedThreadId}");
                }

                // 消息循环
                while ((bRet = GetMessage(out MSG msg, IntPtr.Zero, 0, 0)) != 0)
                {
                    Console.WriteLine($"接收到消息: {bRet}; msg: {msg.message}");
                    if (bRet == -1)
                    {
                        Console.WriteLine("获取消息失败");
                        break;
                    }
                    else
                    {
                        TranslateMessage(ref msg);
                        DispatchMessage(ref msg);
                    }
                }
            });
            return;
        }
        #endregion
    }

    #region 窗体信息类
    /// <summary>
    /// 描述一个Windows窗体
    /// </summary>
    public class WindowInfo
    {
        /// <summary>
        /// 窗体句柄
        /// </summary>
        public IntPtr Handle { get; set; }
        /// <summary>
        /// 窗体标题
        /// </summary>
        public string Title { get; set; } = string.Empty;
        /// <summary>
        /// 窗体ClassName
        /// </summary>
        public string ClassName { get; set; } = string.Empty;
        /// <summary>
        /// 形状
        /// </summary>
        public RECT Rect { get; set; }
        /// <summary>
        /// 位置
        /// </summary>
        public WINDOWPLACEMENT Placement { get; set; }
        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisiable { get; set; }
        /// <summary>
        /// 
        /// </summary>
        public int ProcessId { get; set; }
        /// <summary>
        /// 是否托盘中
        /// </summary>
        public bool IsIconic { get; set; }
        /// <summary>
        /// 重写ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"Title: {Title}; IsIconic: {IsIconic}; Handle: {Handle}; Visiable: {IsVisiable}; React: {Rect}; Placement: {Placement}; ClassName: {ClassName}; ProcessId: {ProcessId};";
    }
    #endregion

    #region 定义系统API相关的结构体
    /// <summary>
    /// 描述窗体位置的矩形结构
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        /// <summary>
        /// Left
        /// </summary>
        public int Left;
        /// <summary>
        /// Top
        /// </summary>
        public int Top;
        /// <summary>
        /// Right
        /// </summary>
        public int Right;
        /// <summary>
        /// Bottom
        /// </summary>
        public int Bottom;
        /// <summary>
        /// 重写ToString
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString()
        {
            return $"{Left} {Top} {Right} {Bottom}";
        }
    }

    /// <summary>
    /// 窗体位置
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        /// <summary>
        /// 
        /// </summary>
        public int length;
        /// <summary>
        /// 
        /// </summary>
        public int flags;
        /// <summary>
        /// 对应SW_HIDE, SW_SHOWNORMAL, ...
        /// </summary>
        public int showCmd;
        /// <summary>
        /// 
        /// </summary>
        public POINT minPosition;
        /// <summary>
        /// 
        /// </summary>
        public POINT maxPosition;
        /// <summary>
        /// 
        /// </summary>
        public RECT normalPosition;
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString()
        {
            return $"length:{length},flags:{flags},showCmd:{showCmd},minPosition:{minPosition},maxPosition:{maxPosition},normalPosition:{normalPosition}";
        }
    }
    /// <summary>
    /// 描述点的数据结构
    /// </summary>
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        /// <summary>
        /// 横坐标
        /// </summary>
        public int X;
        /// <summary>
        /// 纵坐标
        /// </summary>
        public int Y;
        /// <summary>
        /// 重新ToString
        /// </summary>
        /// <returns></returns>
        public override readonly string ToString() => $"[{X}, {Y}]";
    }

    /// <summary>
    /// 消息
    /// </summary>
    public struct MSG
    {
        /// <summary>
        /// 资源句柄
        /// </summary>
        public IntPtr hwnd;
        /// <summary>
        /// 消息代码
        /// </summary>
        public uint message;
        /// <summary>
        /// 
        /// </summary>
        public IntPtr wParam;
        /// <summary>
        /// 
        /// </summary>
        public IntPtr lParam;
        /// <summary>
        /// 
        /// </summary>
        public uint time;
        /// <summary>
        /// 
        /// </summary>
        public POINT pt;
    }
    /// <summary>
    /// 描述一个全局热键
    /// </summary>
    /// <remarks>
    /// 初始化id, 修饰键, 其他键
    /// </remarks>
    /// <param name="id"></param>
    /// <param name="modifiers"></param>
    /// <param name="vmKey"></param>
    public class GlobalHotKey(int id, List<string> modifiers, string vmKey)
    {
        /// <summary>
        /// 
        /// </summary>
        public int Id { get; set; } = id;
        /// <summary>
        /// 修饰键
        /// </summary>
        public List<string> Modifiers { get; set; } = modifiers;
        /// <summary>
        /// 
        /// </summary>
        public string VmKey { get; set; } = vmKey;
        /// <summary>
        /// 重写ToString
        /// </summary>
        /// <returns></returns>
        public override string ToString() => $"{string.Join('+', Modifiers)}+{VmKey}";
    }
    #endregion
}
