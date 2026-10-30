param([Parameter(Mandatory)][string]$Evidence)
$ErrorActionPreference='Stop'
Add-Type @'
using System; using System.Collections.Generic; using System.Runtime.InteropServices;
public static class Screens52 {
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int left,top,right,bottom; }
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] public struct Info { public int cbSize; public Rect monitor,work; public uint flags; [MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)] public string device; }
 delegate bool Callback(IntPtr h,IntPtr dc,ref Rect r,IntPtr data);
 [DllImport("user32.dll")] static extern bool EnumDisplayMonitors(IntPtr dc,IntPtr clip,Callback cb,IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool GetMonitorInfo(IntPtr h,ref Info info);
 [DllImport("shcore.dll")] static extern int GetDpiForMonitor(IntPtr h,int type,out uint x,out uint y);
 public static object[] Read() {
  var list=new List<object>();
  EnumDisplayMonitors(IntPtr.Zero,IntPtr.Zero,(IntPtr h,IntPtr dc,ref Rect r,IntPtr data)=>{
   var info=new Info{cbSize=Marshal.SizeOf<Info>()}; GetMonitorInfo(h,ref info);
   uint x,y; int hr=GetDpiForMonitor(h,0,out x,out y);
   list.Add(new {handle=h.ToInt64(),device=info.device,monitor=info.monitor,work=info.work,primary=(info.flags&1)!=0,dpiX=x,dpiY=y,dpiHresult=hr});return true;
  },IntPtr.Zero);return list.ToArray();
 }
}
'@
$tools=@('winapp','dotnet','wpr','wpa','xperf','PerfView','vmmap','vmmap64','dotnet-counters','dotnet-trace','cdb','windbg') | ForEach-Object {
 $c=Get-Command $_ -ErrorAction SilentlyContinue
 [pscustomobject]@{name=$_;available=($null -ne $c);path=$c.Source}
}
@{utc=[DateTimeOffset]::UtcNow;monitors=@([Screens52]::Read());tools=$tools;os=[Environment]::OSVersion.VersionString;note='Read-only EnumDisplayMonitors/GetMonitorInfo/GetDpiForMonitor; monitor DPI is caller-awareness dependent; GetDpiForWindow and XamlRoot in records.json are authoritative for the owned app.'} | ConvertTo-Json -Depth 8 | Set-Content "$Evidence/inventory.json"
& wpr -profiles | Set-Content "$Evidence/wpr-profiles.txt"
& dotnet tool list -g | Set-Content "$Evidence/dotnet-tools.txt"
