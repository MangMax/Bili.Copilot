using Microsoft.Diagnostics.Runtime;
using System.Security.Cryptography;
using Iced.Intel;
if(args.Contains("metadata")){Metadata.Run();return;}
const string dump = "C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-bc1a3769/artifacts/exit-reproduction/original1/crash-full.dmp";
const string dac = "C:/Program Files/dotnet/shared/Microsoft.NETCore.App/11.0.0-rc.3.26522.148/mscordaccore.dll";
using(var f=File.OpenRead(dac)) if(Convert.ToHexStringLower(SHA256.HashData(f))!="6bf2f1d90753b2c72deb6654dc32ba73959bfa85e64058ae38c341feb726c819")throw new Exception("DAC mismatch");
using(var f = File.OpenRead(dump)) if(Convert.ToHexString(SHA256.HashData(f)) != "EEAB35BB591C37016DA7EC29CEB607A55ECE8D6135D08E5D4511704D8A1A0D3D") throw new Exception("Dump mismatch");
using var dt = DataTarget.LoadDump(dump);
Console.WriteLine($"Reader {dt.DataReader.Architecture} PID={dt.DataReader.ProcessId}");
if(dt.DataReader.Architecture!=System.Runtime.InteropServices.Architecture.X64 || dt.DataReader.ProcessId!=38208)throw new Exception("Dump identity mismatch");
foreach(var ci in dt.ClrVersions) {
 Console.WriteLine($"CLR {ci.Version} {ci.Flavor} {ci.ModuleInfo.ImageBase:x}");
 using var rt=ci.CreateRuntime(dac, false);
 ulong Ptr(ulong a) { if(!dt.DataReader.ReadPointer(a,out var value)) throw new Exception("Missing structural pointer"); return value; }
 const ulong leo=0x1fb6ca435d0;
 var dispatch=leo & ~63UL;
 var wrapper=Ptr(dispatch);
 var handle=Ptr(wrapper);
 var holder=rt.Heap.GetObject(Ptr(handle));
 var dacHandle=rt.EnumerateHandles().Single(h=>h.Address==handle);
 if(dacHandle.Object.Address!=holder.Address)throw new Exception("DAC handle target mismatch");
 Console.WriteLine($"DAC_HANDLE kind={dacHandle.HandleKind} targetMatchesHolder=true");
 Console.WriteLine($"LEO={leo:x} dispatch={dispatch:x} wrapper={wrapper:x} handle={handle:x} holder={holder.Address:x} type={holder.Type?.Name}");
 if(holder.Type?.Name!="System.Runtime.InteropServices.ComWrappers+ManagedObjectWrapperHolder") throw new Exception("Unexpected LEO holder type");
 if(holder.ReadField<ulong>("_wrapper")!=wrapper) throw new Exception("Wrapper backreference mismatch");
 var wrapped=holder.ReadObjectField("_wrappedObject");
 Console.WriteLine($"LEO object={wrapped.Address:x} type={wrapped.Type?.Name}");
 if(!wrapped.IsException) throw new Exception("LEO not exception");
 var leoex=wrapped.AsException()!;
 Console.WriteLine($"LEO HR={leoex.HResult:x}");
 foreach(var frame in leoex.StackTrace) {
  bool covered=frame.Method?.ILOffsetMap.Any(m=>m.StartAddress<=frame.InstructionPointer && frame.InstructionPointer<=m.EndAddress)==true;
  Console.WriteLine($"LEOFRAME {frame.InstructionPointer:x16} {frame.Method?.Signature} module={Path.GetFileName(frame.Method?.Type?.Module?.Name)} token={frame.Method?.MetadataToken:x} reportedIL={frame.Method?.GetILOffset(frame.InstructionPointer)} mappingCoversIP={covered} nativeStart={frame.Method?.NativeCode:x}");
  if(frame.Method?.Signature?.Contains("OnTempControlTimerTick")==true) {
   foreach(var map in frame.Method.ILOffsetMap) Console.WriteLine($"ILMAP {map.ILOffset} {map.StartAddress:x} {map.EndAddress:x}");
   byte[] code=new byte[0x6b0]; if(dt.DataReader.Read(frame.Method.NativeCode,code)!=code.Length) throw new Exception("Missing JIT bytes");
   var decoder=Decoder.Create(64,new ByteArrayCodeReader(code)); decoder.IP=frame.Method.NativeCode;
   var fmt=new IntelFormatter(); var line=new StringOutput();
   using var dis=File.CreateText("artifacts/exit-full-analysis/timer-disassembly.txt");
   while(decoder.IP < frame.Method.NativeCode + (ulong)code.Length) {
    decoder.Decode(out var ins); line.Reset(); fmt.Format(ins,line);
    string target="";
    if(ins.FlowControl==FlowControl.Call) target=rt.GetMethodByInstructionPointer(ins.NearBranchTarget)?.Signature??"";
    if(ins.FlowControl==FlowControl.IndirectCall && ins.IsIPRelativeMemoryOperand) target=rt.GetMethodByInstructionPointer(Ptr(ins.IPRelativeMemoryAddress))?.Signature??"";
    dis.WriteLine($"{ins.IP:x16} {line} {target}");
   }
  }
 }
 var method=rt.GetMethodByInstructionPointer(0x7ffe90e1034f);
 Console.WriteLine($"JIT={method?.Signature} native={method?.NativeCode:x}");
 foreach(var t in rt.Threads.Where(t=>t.OSThreadId==39912)) {
  Console.WriteLine($"THREAD {t.OSThreadId} managed={t.ManagedThreadId}");
  foreach(var frame in t.EnumerateStackTrace()) Console.WriteLine($"{frame.InstructionPointer:x16} {frame.StackPointer:x16} {frame.Kind} {frame.Method?.Signature ?? frame.FrameName}");
  var ex=t.CurrentException;
  Console.WriteLine($"THREAD CurrentExceptionPresent={ex!=null}");
  if(ex!=null) {
   Console.WriteLine($"EX {ex.Address:x} {ex.Type?.Name} HR={ex.HResult:x}");
   foreach(var frame in ex.StackTrace) Console.WriteLine($"EXFRAME {frame.InstructionPointer:x16} {frame.Method?.Signature}");
  }
 }
}
