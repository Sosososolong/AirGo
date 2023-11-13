#include <stdio.h>
#include <windows.h>
#include <ctype.h>
#define MAX_TITLE_LENGTH 100

// 定义结构体
typedef struct {
    int Id;
    int Modifiers;
    char VmKey;
    char HotKey[100];
    char WindowTitle[MAX_TITLE_LENGTH];
} HotkeyConfig;

// ****函数需要先定义才能调用, 否则需要把函数直接定义在main方法前面****
// 窗体消息处理函数
LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
// 注册全局热键函数
int RegisterGlobalHotKey();

// ************************全局变量************************
// 从配置文件读取HotKeyConfig集合
int ReadHotKeyConfig(char* filename);
// 定义结构体集合
HotkeyConfig hotkeyConfigs[100];  // 假设最多有100个配置项
int nextConfigIndex = 0;

int nextHotkeyId = 1;
int lastHotKeyId = 0;
HWND hwnd;

/// <summary>
/// 注册全局热键, 控制指定标题的窗体显示和最小化
/// 热键配置在config.txt中, 一行配置一个热键"ctrl+shift+k kakaxi"; 表示按住ctrl+shift+k时, 显示/隐藏标题为kakaxi的窗体
/// </summary>
/// <param name="argc"></param>
/// <param name="argv"></param>
/// <returns></returns>
int main(int argc, char* argv[])
{
    // 设置控制台编码为utf-8
    SetConsoleOutputCP(65001);

    // 注册全局热键
    RegisterGlobalHotKey();
}

/// @brief 注册全局热键, 切换对应窗体的显示和隐藏
/// @return 0成功
int RegisterGlobalHotKey() {
    HINSTANCE hInstance;
    MSG msg;
    BOOL bRet;

    // 创建一个隐藏的窗口 - 接收热键消息
    hInstance = GetModuleHandle(NULL);
    WNDCLASS wc = { 0 };
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = "HotkeyWindowClass";
    RegisterClass(&wc);
    hwnd = CreateWindowEx(0, wc.lpszClassName, "", 0, 0, 0, 0, 0, HWND_MESSAGE, NULL, hInstance, NULL);

    // 注册信号处理函数
    // signal(SIGINT, HandleSignal);
    // signal(SIGTERM, HandleSignal);
    // signal(SIGABRT, HandleSignal);

    if (ReadHotKeyConfig("config.txt") > 0) {
        printf("读取配置文件失败\n");
        return 1;
    }
    for (size_t i = 0; i < nextConfigIndex; i++)
    {
        HotkeyConfig config = hotkeyConfigs[i];
        int modifiers = config.Modifiers;
        char vmKey = config.VmKey;

        // 注册全局热键 - 指定hwnd为接收热键消息的窗口
        if (!RegisterHotKey(hwnd, config.Id, config.Modifiers, config.VmKey))
        {
            printf("无法注册全局热键\n");
            return 1;
        }
    }

    // 消息循环
    while ((bRet = GetMessage(&msg, NULL, 0, 0)) != 0)
    {
        printf("GetMessage, msg.message: %d\n", msg.message);
        if (bRet == -1)
        {
            printf("获取消息失败\n");
            break;
        }
        else
        {
            TranslateMessage(&msg);
            DispatchMessage(&msg);
        }
    }

    return 0;
}

void RemoveNewline(char* str) {
    char* newlinePos = strchr(str, '\n');  // 查找换行符的位置
    if (newlinePos != NULL) {
        *newlinePos = '\0';  // 将换行符替换为字符串结束符
    }

    newlinePos = strchr(str, '\r');
    if (newlinePos != NULL) {
        *newlinePos = '\0';
    }
}

// 从配置文件读取HotKeyConfig集合
int ReadHotKeyConfig(char* filename)
{
    // 加载配置文件
    FILE* configFile = fopen(filename, "r");
    if (configFile == NULL) {
        printf("无法打开配置文件config.txt\n");
        return 1;
    }

    // 逐行读取配置文件并解析为结构体
    char line[256];
    // fgets读取一行数据存储到line中, 包含\n
    while (fgets(line, sizeof(line), configFile) != NULL) {
        // 解析每行数据为结构体属性
        char hotkey[50], modifier[10];
        char vmKey;
        int modifiers = 0;

        char windowTitle[MAX_TITLE_LENGTH];

        // 使用适当的方法解析每个属性的值
        sscanf(line, "%s %s", hotkey, windowTitle);
        char hotkeyOriginal[50];
        strcpy(hotkeyOriginal, hotkey);

        // 使用逗号作为分隔符  
        char* token = strtok(hotkey, "+");
        while (token != NULL) {
            strcpy(modifier, token);
            // 获取下一个
            char* next = strtok(NULL, "+");

            if (next == NULL) {
                vmKey = toupper(*modifier);
                printf("vmKey..........: %c\n", vmKey);
                break;
            }
            else {
                printf("modifier........: %s\n", modifier);
                strcpy(modifier, modifier);
                if (strstr(modifier, "ctrl") != NULL) {
                    modifiers |= MOD_CONTROL;
                }
                else if (strstr(modifier, "shift") != NULL) {
                    modifiers |= MOD_SHIFT;
                }
                else if (strstr(modifier, "alt") != NULL) {
                    modifiers |= MOD_ALT;
                }
                else if (strstr(modifier, "win") != NULL) {
                    modifiers |= MOD_WIN;
                }
                else {
                    printf("Unknown modifier: %s\n", modifier);
                    return 1;
                }
                token = next;
            }
        }

        // 将解析出的值存入结构体
        hotkeyConfigs[nextConfigIndex].Id = lastHotKeyId = nextHotkeyId++;
        hotkeyConfigs[nextConfigIndex].Modifiers = modifiers;
        hotkeyConfigs[nextConfigIndex].VmKey = vmKey;
        strncpy(hotkeyConfigs[nextConfigIndex].HotKey, hotkeyOriginal, 100);
        strncpy(hotkeyConfigs[nextConfigIndex].WindowTitle, windowTitle, MAX_TITLE_LENGTH);
        RemoveNewline(hotkeyConfigs[nextConfigIndex].WindowTitle);

        nextConfigIndex++;
        if (nextConfigIndex >= 100) {
            break;  // 达到最大配置项数量，退出循环
        }
    }

    // 关闭配置文件
    fclose(configFile);

    // 打印加载的结构体集合
    for (int i = 0; i < nextConfigIndex; i++) {
        printf("%d\t%s\t%s\n", hotkeyConfigs[i].Id, hotkeyConfigs[i].HotKey, hotkeyConfigs[i].WindowTitle);
        printf("\n");
    }

    return 0;
}

/// @brief 全局热键接收消息的窗体的消息处理函数
/// @param hwnd 所属窗体句柄
/// @param uMsg 接收到的消息
/// @param wParam 全局热键Id
/// @param lParam 
/// @return 
LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam)
{
    printf("WindowProc: uMsg: %d; wParam: %d\n", uMsg, wParam);
    switch (uMsg)
    {
    case WM_HOTKEY:
    {
        HotkeyConfig config;
        for (size_t i = 0; i < nextConfigIndex; i++)
        {
            if (i + 1 == hotkeyConfigs[i].Id)
            {
                config = hotkeyConfigs[i];
                break;
            }
        }

        if (&config != NULL) {
            printf("WindowProc: global hot key triggered: %s: %s\n\n", config.HotKey, config.WindowTitle);
            // 找到窗口句柄
            HWND hwnd = FindWindowA(NULL, config.WindowTitle);
            if (hwnd) {
                SetForegroundWindow(hwnd);
                if (IsWindowVisible(hwnd)) {
                    if (IsIconic(hwnd)) {
                        // Window is minimized, restore it
                        ShowWindow(hwnd, SW_RESTORE);
                    }
                    else if (IsZoomed(hwnd)) {
                        printf("Window is maximized\n");
                    }
                    else {
                        // Window is in normal state, minimize it
                        ShowWindow(hwnd, SW_MINIMIZE);
                    }
                }
                else {
                    printf("Window is hidden\n");
                    ShowWindow(hwnd, SW_SHOW);
                }
            }
            else {
                printf("找不到kakaxi窗口\n");
            }
        }
        else {
            printf("HotkeyConfig not found\n");
        }
    }
    break;

    case WM_DESTROY:
        PostQuitMessage(0);
        break;

    default:
        return DefWindowProc(hwnd, uMsg, wParam, lParam);
    }

    return 0;
}
