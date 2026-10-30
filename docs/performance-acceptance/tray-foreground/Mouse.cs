using System;using System.Runtime.InteropServices;
public static class VerifiedMouse {
 [StructLayout(LayoutKind.Sequential)] struct POINT{public int x,y;}
 [StructLayout(LayoutKind.Sequential)] struct MOUSE{public int x,y;public uint data,flags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)] struct INPUT{public uint type;public MOUSE mouse;}
 [DllImport("user32.dll")] static extern bool SetCursorPos(int x,int y);
 [DllImport("user32.dll")] static extern IntPtr WindowFromPoint(POINT p);
 [DllImport("user32.dll")] static extern IntPtr GetAncestor(IntPtr h,uint flags);
 [DllImport("user32.dll")] static extern uint SendInput(uint count,INPUT[] inputs,int size);
 [DllImport("user32.dll")] static extern IntPtr SetThreadDpiAwarenessContext(IntPtr context);
 public static void Click(long hwnd,int x,int y,bool right){
  var old=SetThreadDpiAwarenessContext(new IntPtr(-4));
  try{
  var root=GetAncestor(WindowFromPoint(new POINT{x=x,y=y}),2);
  if(root.ToInt64()!=hwnd)throw new Exception("Point not owned by expected root HWND: "+root);
  if(!SetCursorPos(x,y))throw new Exception("SetCursorPos failed");
  var a=new[]{new INPUT{mouse=new MOUSE{flags=right?8u:2u}},new INPUT{mouse=new MOUSE{flags=right?16u:4u}}};
  if(SendInput(2,a,Marshal.SizeOf<INPUT>())!=2)throw new Exception("SendInput failed");
  }finally{SetThreadDpiAwarenessContext(old);}
 }
}
