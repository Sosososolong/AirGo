using System.Runtime.InteropServices;
using System.Text;

namespace Sylas.RemoteTasks.App.Utils
{
    public static class SystemHelper
    {
        #region 键盘代码
        public const int MOD_ALT = 1;
        public const int MOD_CONTROL = 2;
        public const int MOD_SHIFT = 4;
        public const int MOD_WIN = 8;

        public const uint VK_BACK = 8;
        public const uint VK_TAB = 9;
        public const uint VK_CLEAR = 12;
        public const uint VK_RETURN = 13;
        public const uint VK_SHIFT = 16;
        public const uint VK_CONTROL = 17;
        public const uint VK_MENU = 18;
        public const uint VK_PAUSE = 19;
        public const uint VK_CAPITAL = 20;
        public const uint VK_KANA = 0x15;
        public const uint VK_HANGEUL = 0x15;
        public const uint VK_HANGUL = 0x15;
        public const uint VK_JUNJA = 0x17;
        public const uint VK_FINAL = 0x18;
        public const uint VK_HANJA = 0x19;
        public const uint VK_KANJI = 0x19;
        public const uint VK_ESCAPE = 0x1B;
        public const uint VK_CONVERT = 0x1C;
        public const uint VK_NONCONVERT = 0x1D;
        public const uint VK_ACCEPT = 0x1E;
        public const uint VK_MODECHANGE = 0x1F;
        public const uint VK_SPACE = 32;
        public const uint VK_PRIOR = 33;
        public const uint VK_NEXT = 34;
        public const uint VK_END = 35;
        public const uint VK_HOME = 36;
        public const uint VK_LEFT = 37;
        public const uint VK_UP = 38;
        public const uint VK_RIGHT = 39;
        public const uint VK_DOWN = 40;
        public const uint VK_SELECT = 41;
        public const uint VK_PRINT = 42;
        public const uint VK_EXECUTE = 43;
        public const uint VK_SNAPSHOT = 44;
        public const uint VK_INSERT = 45;
        public const uint VK_DELETE = 46;
        public const uint VK_HELP = 47;
        public const uint VK_LWIN = 0x5B;
        public const uint VK_RWIN = 0x5C;
        public const uint VK_APPS = 0x5D;
        public const uint VK_SLEEP = 0x5F;
        public const uint VK_NUMPAD0 = 0x60;
        public const uint VK_NUMPAD1 = 0x61;
        public const uint VK_NUMPAD2 = 0x62;
        public const uint VK_NUMPAD3 = 0x63;
        public const uint VK_NUMPAD4 = 0x64;
        public const uint VK_NUMPAD5 = 0x65;
        public const uint VK_NUMPAD6 = 0x66;
        public const uint VK_NUMPAD7 = 0x67;
        public const uint VK_NUMPAD8 = 0x68;
        public const uint VK_NUMPAD9 = 0x69;
        public const uint VK_MULTIPLY = 0x6A;
        public const uint VK_ADD = 0x6B;
        public const uint VK_SEPARATOR = 0x6C;
        public const uint VK_SUBTRACT = 0x6D;
        public const uint VK_DECIMAL = 0x6E;
        public const uint VK_DIVIDE = 0x6F;
        public const uint VK_F1 = 0x70;
        public const uint VK_F2 = 0x71;
        public const uint VK_F3 = 0x72;
        public const uint VK_F4 = 0x73;
        public const uint VK_F5 = 0x74;
        public const uint VK_F6 = 0x75;
        public const uint VK_F7 = 0x76;
        public const uint VK_F8 = 0x77;
        public const uint VK_F9 = 0x78;
        public const uint VK_F10 = 0x79;
        public const uint VK_F11 = 0x7A;
        public const uint VK_F12 = 0x7B;
        public const uint VK_F13 = 0x7C;
        public const uint VK_F14 = 0x7D;
        public const uint VK_F15 = 0x7E;
        public const uint VK_F16 = 0x7F;
        public const uint VK_F17 = 0x80;
        public const uint VK_F18 = 0x81;
        public const uint VK_F19 = 0x82;
        public const uint VK_F20 = 0x83;
        public const uint VK_F21 = 0x84;
        public const uint VK_F22 = 0x85;
        public const uint VK_F23 = 0x86;
        public const uint VK_F24 = 0x87;
        public const uint VK_NUMLOCK = 0x90;
        public const uint VK_SCROLL = 0x91;
        public const uint VK_LSHIFT = 0xA0;
        public const uint VK_RSHIFT = 0xA1;
        public const uint VK_LCONTROL = 0xA2;
        public const uint VK_RCONTROL = 0xA3;
        public const uint VK_LMENU = 0xA4;
        public const uint VK_RMENU = 0xA5;

        public const uint VK_OEM_1 = 0xBA;
        public const uint VK_OEM_2 = 0xBF;
        public const uint VK_OEM_3 = 0xC0;
        public const uint VK_OEM_4 = 0xDB;
        public const uint VK_OEM_5 = 0xDC;
        public const uint VK_OEM_6 = 0xDD;
        public const uint VK_OEM_7 = 0xDE;
        public const uint VK_OEM_8 = 0xDF;

        public const uint VK_PROCESSKEY = 0xE5;

        public const uint VK_ATTN = 0xF6;
        public const uint VK_CRSEL = 0xF7;
        public const uint VK_EXSEL = 0xF8;
        public const uint VK_EREOF = 0xF9;
        public const uint VK_PLAY = 0xFA;
        public const uint VK_ZOOM = 0xFB;
        public const uint VK_NONAME = 0xFC;
        public const uint VK_PA1 = 0xFD;
        public const uint VK_OEM_CLEAR = 0xFE;
        public const int TME_HOVER = 1;
        public const int TME_LEAVE = 2;
        public const int TME_QUERY = 0x40000000;
        public const uint TME_CANCEL = 0x80000000;
        public const uint HOVER_DEFAULT = 0xFFFFFFFF;
        public const int MK_LBUTTON = 1;
        public const int MK_RBUTTON = 2;
        public const int MK_SHIFT = 4;
        public const int MK_CONTROL = 8;
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

        static readonly List<WindowInfo> _windowsList = new();
        #endregion
        
        #region 消息代码
        public const int WM_HOTKEY = 0x0312;
        public const int WM_DESTROY = 0x0002;
        #endregion

        #region 封装操作系统操作窗体相关接口
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        public static extern int DestroyWindow(IntPtr hWnd);
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
        public static bool ShowWindow(IntPtr hwnd)
        {
            return ShowWindow(hwnd, SW_SHOWDEFAULT);
        }
        public static bool ShowWindow(string title)
        {
            var hwnd = FindWindowByTitle(title);
            return ShowWindow(hwnd, SW_SHOWDEFAULT);
        }
        public static bool ShowWindowAsync(IntPtr hwnd)
        {
            return ShowWindowAsync(hwnd, SW_RESTORE);
        }
        public static bool ShowWindowAsync(string title)
        {
            var hwnd = FindWindowByTitle(title);
            return ShowWindowAsync(hwnd, SW_RESTORE);
        }
        public static bool RestoreWindowFromTray(IntPtr handle)
        {
            var r1 = SendMessage(handle, WM_LBUTTONDOWN, IntPtr.Zero, IntPtr.Zero);
            var r2 = SendMessage(handle, WM_LBUTTONUP, IntPtr.Zero, IntPtr.Zero);
            Console.WriteLine($"r1: {r1}; r2: {r2}");
            return r1 + r2 > 0;
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

        static int _currentId = 0;
        static List<HotKeyInfo> _hotKeys = new();
        static IntPtr WindowProc(IntPtr hwnd, uint uMsg, IntPtr wParam, IntPtr lParam)
        {
            switch (uMsg)
            {
                case WM_HOTKEY:
                    if (wParam.ToInt32() == _currentId)
                    {
                        Console.WriteLine("全局热键被触发");
                        var hotKey = _hotKeys.FirstOrDefault(x => x.Handler == hwnd);
                        if (hotKey.Callback is not null)
                        {
                            hotKey.Callback();
                        }
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
            _ => Convert.ToChar(vk)
        };
        public static void RegisterGlobalHotKey(string[] mks, string vk, Action callback)
        {
            _currentId++;
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
                    lpszClassName = $"HotkeyWindowClass{_currentId}"
                };
                RegisterClass(ref wc);
                hwnd = CreateWindowEx(0, wc.lpszClassName, "Z", 0, 0, 0, 0, 0, IntPtr.Zero, IntPtr.Zero, hInstance, IntPtr.Zero);

                int hotkeyMk = 0;
                foreach (var mk in mks)
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
                uint hotkeyVk = GetVkValue(vk);

                var hotkeyName = string.Join('_', mks.Select(x => x.ToLower())) + vk;

                if (_hotKeys.Any(x => x.Name == hotkeyName))
                {
                    // 关闭窗体
                    var registed = _hotKeys.First(x => x.Name == hotkeyName);
                    Console.WriteLine($"热键已经注册: name: {registed.Name}; handler: {registed.Handler}; id: {registed.Id};");
                    return;
                }
                // 注册全局热键
                if (!RegisterHotKey(hwnd, _currentId, hotkeyMk, hotkeyVk))
                //if (!RegisterHotKey(hwnd, _currentId, MOD_CONTROL| MOD_SHIFT, VK_F12))
                {
                    Console.WriteLine("无法注册全局热键");
                    return;
                }
                // ctrl_shiftK, 0x00001, 回调函数, 热键id, 是否关闭热键
                _hotKeys.Add(new HotKeyInfo(_currentId, hotkeyName, hwnd, callback, false));
                Console.WriteLine($"成功注册热键{hotkeyName}: {hwnd} - {wc.lpszClassName}; Thread: {Environment.CurrentManagedThreadId}");
                // 消息循环
                while ((bRet = GetMessage(out MSG msg, IntPtr.Zero, 0, 0)) != 0)
                {
                    Console.WriteLine($"接收到消息: {bRet}");
                    if (!_hotKeys.Any(x => x.Handler == hwnd))
                    {
                        Console.WriteLine("热键状态存储异常, 注销热键");
                        break;
                    }
                    var hotkey = _hotKeys.First(x => x.Handler == hwnd);
                    if (hotkey.Disabled)
                    {
                        Console.WriteLine("检测到热键已关闭, 注销热键");
                        break;
                    }
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

                // 注销全局热键
                UnregisterHotKey(hwnd, _currentId);
                Console.WriteLine("注销热键, 子线程退出");
            });
            return;
        }
        #endregion
    }

    #region 窗体信息类
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
    #endregion
    
    #region 定义系统API相关的结构体
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
        /// <summary>
        /// 对应SW_HIDE, SW_SHOWNORMAL, ...
        /// </summary>
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

    /// <summary>
    /// 消息
    /// </summary>
    public struct MSG
    {
        public IntPtr hwnd;
        public uint message;
        public IntPtr wParam;
        public IntPtr lParam;
        public uint time;
        public POINT pt;
    }
    public struct HotKeyInfo
    {
        public HotKeyInfo(int id, string name, nint handler, Action callback, bool disabled)
        {
            Id = id;
            Name = name;
            Handler = handler;
            Callback = callback;
            Disabled = disabled;
        }
        /// <summary>
        /// 热键id
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// 热键的拼接作为热键名, 如: ctrl_shiftK
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 接收消息的窗体句柄
        /// </summary>
        public nint Handler { get; set; }
        /// <summary>
        /// 回调函数
        /// </summary>
        public Action Callback { get; set; }
        /// <summary>
        /// 是否关闭热键
        /// </summary>
        public bool Disabled { get; set; }
    }
    #endregion
}
