using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;
using Native = Windows.Win32.PInvoke;

namespace BiliCopilot.UI.Forms;

// Namespace resolution instruments the source-linked class, without editing it.
// Every operation still calls the generated real Win32 import exactly once.
internal static class PInvoke {
    public static Action<string, HWND> Observe;
    public static HWND SetFocus(HWND hwnd) { Observe?.Invoke("SetFocus", hwnd); return Native.SetFocus(hwnd); }
    public static BOOL SetForegroundWindow(HWND hwnd) { Observe?.Invoke("SetForegroundWindow", hwnd); return Native.SetForegroundWindow(hwnd); }
    public static HWND GetForegroundWindow() => Native.GetForegroundWindow();
    public static BOOL IsZoomed(HWND hwnd) => Native.IsZoomed(hwnd);
    public static BOOL IsIconic(HWND hwnd) => Native.IsIconic(hwnd);
    public static int GetWindowLong(HWND hwnd, WINDOW_LONG_PTR_INDEX index) => Native.GetWindowLong(hwnd,index);
    public static BOOL SetWindowPos(HWND hwnd,HWND after,int x,int y,int cx,int cy,SET_WINDOW_POS_FLAGS flags) => Native.SetWindowPos(hwnd,after,x,y,cx,cy,flags);
}
