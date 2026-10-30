using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
public static class NativeCompetitor {
 delegate IntPtr WndProc(IntPtr h,uint m,IntPtr w,IntPtr l);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct WC {public uint style;public WndProc proc;public int extra1,extra2;public IntPtr instance,icon,cursor,brush;public string menu,name;}
 [StructLayout(LayoutKind.Sequential)] struct MSG {public IntPtr h;public uint m;public UIntPtr w;public IntPtr l;public uint time;public int x,y;public uint priv;}
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern ushort RegisterClassW(ref WC c);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr CreateWindowExW(uint ex,string c,string title,uint style,int x,int y,int width,int height,IntPtr parent,IntPtr menu,IntPtr instance,IntPtr param);
 [DllImport("user32.dll")] static extern IntPtr DefWindowProcW(IntPtr h,uint m,IntPtr w,IntPtr l);
 [DllImport("user32.dll")] static extern bool GetMessageW(out MSG m,IntPtr h,uint a,uint b);
 [DllImport("user32.dll")] static extern bool TranslateMessage(ref MSG m);
 [DllImport("user32.dll")] static extern IntPtr DispatchMessageW(ref MSG m);
 [DllImport("user32.dll")] static extern void PostQuitMessage(int code);
 [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h,int cmd);
 [DllImport("user32.dll")] static extern bool UpdateWindow(IntPtr h);
 [DllImport("user32.dll")] static extern IntPtr SetTimer(IntPtr h,IntPtr id,uint ms,IntPtr cb);
 [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
 [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint pid);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h,StringBuilder b,int len);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandleW(string n);
 static WndProc proc=WindowProc;static IntPtr main,edit;static string folder,previous="",previousText="";
 static void Log(string file,string body){File.AppendAllText(Path.Combine(folder,file),"{\"utc\":\""+DateTime.UtcNow.ToString("o")+"\","+body+"}\n");}
 static IntPtr WindowProc(IntPtr h,uint m,IntPtr w,IntPtr l){
  if(m==2){PostQuitMessage(0);return IntPtr.Zero;}
  if(m==0x113){
   var fg=GetForegroundWindow();uint pid;GetWindowThreadProcessId(fg,out pid);var visible=IsWindowVisible(main);var min=IsIconic(main);
   var value=fg+","+visible+","+min;
   if(value!=previous){previous=value;Log("timeline.jsonl","\"foreground\":"+fg.ToInt64()+",\"foregroundPid\":"+pid+",\"mainHwnd\":"+main.ToInt64()+",\"visible\":"+visible.ToString().ToLowerInvariant()+",\"minimized\":"+min.ToString().ToLowerInvariant()+",\"targetForeground\":"+(fg==main).ToString().ToLowerInvariant());}
   var text=new StringBuilder(256);GetWindowTextW(edit,text,256);
   if(text.ToString()!=previousText){previousText=text.ToString();Log("competitor-events.jsonl","\"kind\":\"text\",\"length\":"+previousText.Length);}
  }
  return DefWindowProcW(h,m,w,l);
 }
 public static void Run(string path,long target){
  folder=path;main=new IntPtr(target);var instance=GetModuleHandleW(null);
  var wc=new WC{proc=proc,instance=instance,brush=new IntPtr(6),name="TrayDiagOwnedCompetitorEE493"};
  if(RegisterClassW(ref wc)==0)throw new Exception("RegisterClass failed");
  var window=CreateWindowExW(0,wc.name,"Owned tray diagnostic competitor EE493",0x00CF0000,30,100,480,210,IntPtr.Zero,IntPtr.Zero,instance,IntPtr.Zero);
  edit=CreateWindowExW(0x200,"EDIT","",0x50010080,20,50,420,40,window,new IntPtr(101),instance,IntPtr.Zero);
  // STARTUPINFO from the hidden helper launch can suppress the first ShowWindow.
  ShowWindow(window,5);ShowWindow(window,5);UpdateWindow(window);SetTimer(window,new IntPtr(1),20,IntPtr.Zero);
  File.WriteAllText(Path.Combine(folder,"competitor-ready.json"),"{\"pid\":"+Environment.ProcessId+",\"hwnd\":"+window.ToInt64()+"}");
  MSG msg;while(GetMessageW(out msg,IntPtr.Zero,0,0)){TranslateMessage(ref msg);DispatchMessageW(ref msg);}
 }
}
