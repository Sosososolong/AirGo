#include <stdio.h>
#include <string.h>
#include <windows.h>
#include <ctype.h>
#define MAX_TITLE_LENGTH 100

// ����ṹ��
typedef struct {
    int Id;
    int Modifiers;
    char VmKey;
    char HotKey[100];
    char WindowTitle[MAX_TITLE_LENGTH];
} HotkeyConfig;

// ****������Ҫ�ȶ�����ܵ���, ������Ҫ�Ѻ���ֱ�Ӷ�����main����ǰ��****
// ������Ϣ������
LRESULT CALLBACK WindowProc(HWND hwnd, UINT uMsg, WPARAM wParam, LPARAM lParam);
// ע��ȫ���ȼ�����
int RegisterGlobalHotKey();

// ************************ȫ�ֱ���************************
// �������ļ���ȡHotKeyConfig����
int ReadHotKeyConfig(char* filename);
// ����ṹ�弯��
HotkeyConfig hotkeyConfigs[100];  // ���������100��������
int nextConfigIndex = 0;

int nextHotkeyId = 1;
int lastHotKeyId = 0;
HWND hwnd;

/// <summary>
/// ע��ȫ���ȼ�, ����ָ������Ĵ�����ʾ����С��
/// �ȼ�������config.txt��, һ������һ���ȼ�"ctrl+shift+k kakaxi"; ��ʾ��סctrl+shift+kʱ, ��ʾ/���ر���Ϊkakaxi�Ĵ���
/// </summary>
/// <param name="argc"></param>
/// <param name="argv"></param>
/// <returns></returns>
int main(int argc, char* argv[])
{
    // ���ÿ���̨����Ϊutf-8
    SetConsoleOutputCP(65001);

    // ע��ȫ���ȼ�
    RegisterGlobalHotKey();
}

/// @brief ע��ȫ���ȼ�, �л���Ӧ�������ʾ������
/// @return 0�ɹ�
int RegisterGlobalHotKey() {
    HINSTANCE hInstance;
    MSG msg;
    BOOL bRet;

    // ����һ�����صĴ��� - �����ȼ���Ϣ
    hInstance = GetModuleHandle(NULL);
    WNDCLASS wc = { 0 };
    wc.lpfnWndProc = WindowProc;
    wc.hInstance = hInstance;
    wc.lpszClassName = "HotkeyWindowClass";
    RegisterClass(&wc);
    hwnd = CreateWindowEx(0, wc.lpszClassName, "", 0, 0, 0, 0, 0, HWND_MESSAGE, NULL, hInstance, NULL);

    // ע���źŴ�����
    // signal(SIGINT, HandleSignal);
    // signal(SIGTERM, HandleSignal);
    // signal(SIGABRT, HandleSignal);

    if (ReadHotKeyConfig("config.txt") > 0) {
        printf("��ȡ�����ļ�ʧ��\n");
        return 1;
    }
    for (size_t i = 0; i < nextConfigIndex; i++)
    {
        HotkeyConfig config = hotkeyConfigs[i];
        int modifiers = config.Modifiers;
        char vmKey = config.VmKey;

        // ע��ȫ���ȼ� - ָ��hwndΪ�����ȼ���Ϣ�Ĵ���
        if (!RegisterHotKey(hwnd, config.Id, config.Modifiers, config.VmKey))
        {
            printf("�޷�ע��ȫ���ȼ�\n");
            return 1;
        }
    }

    // ��Ϣѭ��
    while ((bRet = GetMessage(&msg, NULL, 0, 0)) != 0)
    {
        printf("GetMessage, msg.message: %d\n", msg.message);
        if (bRet == -1)
        {
            printf("��ȡ��Ϣʧ��\n");
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
    char* newlinePos = strchr(str, '\n');  // ���һ��з���λ��
    if (newlinePos != NULL) {
        *newlinePos = '\0';  // �����з��滻Ϊ�ַ���������
    }

    newlinePos = strchr(str, '\r');
    if (newlinePos != NULL) {
        *newlinePos = '\0';
    }
}

// �������ļ���ȡHotKeyConfig����
int ReadHotKeyConfig(char* filename)
{
    // ���������ļ�
    FILE* configFile = fopen(filename, "r");
    if (configFile == NULL) {
        printf("�޷��������ļ�config.txt\n");
        return 1;
    }

    // ���ж�ȡ�����ļ�������Ϊ�ṹ��
    char line[256];
    // fgets��ȡһ�����ݴ洢��line��, ����\n
    while (fgets(line, sizeof(line), configFile) != NULL) {
        // ����ÿ������Ϊ�ṹ������
        char hotkey[50], modifier[10];
        char vmKey;
        int modifiers = 0;

        char windowTitle[MAX_TITLE_LENGTH];

        // ʹ���ʵ��ķ�������ÿ�����Ե�ֵ
        sscanf(line, "%s %s", hotkey, windowTitle);
        char hotkeyOriginal[50];
        strcpy(hotkeyOriginal, hotkey);

        // ʹ�ö�����Ϊ�ָ���  
        char* token = strtok(hotkey, "+");
        while (token != NULL) {
            strcpy(modifier, token);
            // ��ȡ��һ��
            char* next = strtok(NULL, "+");

            if (next == NULL) {
                if (strcasecmp(modifier, "space") == 0) {
                    vmKey = VK_SPACE;
                    printf("VmKey: %s\n", "space");
                }
                else if (strcasecmp(modifier, "enter") == 0) {
                    vmKey = VK_RETURN;
                    printf("VmKey: %s\n", "enter");
                }
                else if (strcasecmp(modifier, "tab") == 0) {
                    vmKey = VK_TAB;
                    printf("VmKey: %s\n", "tab");
                }
                else if (strcasecmp(modifier, "esc") == 0) {
                    vmKey = VK_ESCAPE;
                    printf("VmKey: %s\n", "esc");
                }
                else if (strcasecmp(modifier, "backspace") == 0) {
                    vmKey = VK_BACK;
                    printf("VmKey: %s\n", "backspace");
                }
                else if (strcasecmp(modifier, "capslock") == 0) {
                    vmKey = VK_CAPITAL;
                    printf("VmKey: %s\n", "capslock");
                }
                else if (strcasecmp(modifier, "numlock") == 0) {
                    vmKey = VK_NUMLOCK;
                    printf("VmKey: %s\n", "numlock");
                }
                else if (strcasecmp(modifier, "scrolllock") == 0) {
                    vmKey = VK_SCROLL;
                    printf("VmKey: %s\n", "scrolllock");
                }
                else if (strcasecmp(modifier, "printscreen") == 0) {
                    vmKey = VK_SNAPSHOT;
                    printf("VmKey: %s\n", "printscreen");
                }
                else if (strcasecmp(modifier, "pause") == 0) {
                    vmKey = VK_PAUSE;
                    printf("VmKey: %s\n", "pause");
                }
                else if (strcasecmp(modifier, "f1") == 0) {
                    vmKey = VK_F1;
                    printf("VmKey: %s\n", "f1");
                }
                else if (strcasecmp(modifier, "f2") == 0) {
                    vmKey = VK_F2;
                    printf("VmKey: %s\n", "f2");
                }
                else if (strcasecmp(modifier, "f3") == 0) {
                    vmKey = VK_F3;
                    printf("VmKey: %s\n", "f3");
                }
                else if (strcasecmp(modifier, "f4") == 0) {
                    vmKey = VK_F4;
                    printf("VmKey: %s\n", "f4");
                }
                else if (strcasecmp(modifier, "f5") == 0) {
                    vmKey = VK_F5;
                    printf("VmKey: %s\n", "f5");
                }
                else if (strcasecmp(modifier, "f6") == 0) {
                    vmKey = VK_F6;
                    printf("VmKey: %s\n", "f6");
                }
                else if (strcasecmp(modifier, "f7") == 0) {
                    vmKey = VK_F7;
                    printf("VmKey: %s\n", "f7");
                }
                else if (strcasecmp(modifier, "f8") == 0) {
                    vmKey = VK_F8;
                    printf("VmKey: %s\n", "f8");
                }
                else if (strcasecmp(modifier, "f9") == 0) {
                    vmKey = VK_F9;
                    printf("VmKey: %s\n", "f9");
                }
                else if (strcasecmp(modifier, "f10") == 0) {
                    vmKey = VK_F10;
                    printf("VmKey: %s\n", "f10");
                }
                else if (strcasecmp(modifier, "f11") == 0) {
                    vmKey = VK_F11;
                    printf("VmKey: %s\n", "f11");
                }
                else if (strcasecmp(modifier, "f12") == 0) {
                    vmKey = VK_F12;
                    printf("VmKey: %s\n", "f12");
                }
                else {
                    vmKey = toupper(*modifier);
                    printf("vmKey: %c\n", vmKey);
                }
                break;
            }
            else
            {
                printf("modifier %s\n", modifier);
                strcpy(modifier, modifier);
                if (strcasecmp(modifier, "ctrl") == 0) {
                    modifiers |= MOD_CONTROL;
                }
                else if (strcasecmp(modifier, "shift") == 0) {
                    modifiers |= MOD_SHIFT;
                }
                else if (strcasecmp(modifier, "alt") == 0) {
                    modifiers |= MOD_ALT;
                }
                else if (strcasecmp(modifier, "win") == 0) {
                    modifiers |= MOD_WIN;
                }
                else {
                    printf("Unknown modifier: %s\n", modifier);
                    return 1;
                }
                token = next;
            }
        }

        // ����������ֵ����ṹ��
        hotkeyConfigs[nextConfigIndex].Id = lastHotKeyId = nextHotkeyId++;
        hotkeyConfigs[nextConfigIndex].Modifiers = modifiers;
        hotkeyConfigs[nextConfigIndex].VmKey = vmKey;
        strncpy(hotkeyConfigs[nextConfigIndex].HotKey, hotkeyOriginal, 100);
        strncpy(hotkeyConfigs[nextConfigIndex].WindowTitle, windowTitle, MAX_TITLE_LENGTH);
        RemoveNewline(hotkeyConfigs[nextConfigIndex].WindowTitle);

        nextConfigIndex++;
        if (nextConfigIndex >= 100) {
            break;  // �ﵽ����������������˳�ѭ��
        }
    }

    // �ر������ļ�
    fclose(configFile);

    // ��ӡ���صĽṹ�弯��
    for (int i = 0; i < nextConfigIndex; i++) {
        printf("%d\t%s\t%s\n", hotkeyConfigs[i].Id, hotkeyConfigs[i].HotKey, hotkeyConfigs[i].WindowTitle);
        printf("\n");
    }

    return 0;
}

/// @brief ȫ���ȼ�������Ϣ�Ĵ������Ϣ������
/// @param hwnd ����������
/// @param uMsg ���յ�����Ϣ
/// @param wParam ȫ���ȼ�Id
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
            // �ҵ����ھ��
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
                printf("�Ҳ���kakaxi����\n");
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
