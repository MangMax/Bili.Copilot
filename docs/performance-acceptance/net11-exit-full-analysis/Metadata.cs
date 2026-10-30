using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Reflection.Emit;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
static class Metadata {
 public static void Run() {
  string frozen="C:/Users/hp/.t3/worktrees/Bili.Copilot/t3-bc1a3769/artifacts/exit-reproduction/fixture/frozen-output";
  using var gz=new GZipStream(File.OpenRead("docs/performance-acceptance/net11-exit-reproduction/frozen-files.json.gz"),CompressionMode.Decompress);
  var manifest=JsonDocument.Parse(gz).RootElement.EnumerateArray().ToDictionary(x=>x.GetProperty("path").GetString()!,x=>x.GetProperty("sha256").GetString()!);
  foreach(var file in new[]{"BiliCopilot.UI.dll","Richasy.WinUIKernel.Share.dll","Microsoft.WinUI.dll","Microsoft.InteractiveExperiences.Projection.dll"}) {
   using(var hashFile=File.OpenRead(Path.Combine(frozen,file))) if(Convert.ToHexStringLower(SHA256.HashData(hashFile))!=manifest[file])throw new Exception("Frozen module mismatch");
   using var fs=File.OpenRead(Path.Combine(frozen,file)); using var pe=new PEReader(fs); var md=pe.GetMetadataReader();
   string Name(EntityHandle h) => h.Kind switch {
    HandleKind.TypeDefinition => md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)h).Namespace)+"."+md.GetString(md.GetTypeDefinition((TypeDefinitionHandle)h).Name),
    HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Namespace)+"."+md.GetString(md.GetTypeReference((TypeReferenceHandle)h).Name),
    HandleKind.MemberReference => Name(md.GetMemberReference((MemberReferenceHandle)h).Parent)+"."+md.GetString(md.GetMemberReference((MemberReferenceHandle)h).Name),
    HandleKind.MethodDefinition => Name(md.GetMethodDefinition((MethodDefinitionHandle)h).GetDeclaringType())+"."+md.GetString(md.GetMethodDefinition((MethodDefinitionHandle)h).Name),
    HandleKind.FieldDefinition => md.GetString(md.GetFieldDefinition((FieldDefinitionHandle)h).Name),
    HandleKind.MethodSpecification => Name(md.GetMethodSpecification((MethodSpecificationHandle)h).Method),
    _ => h.Kind.ToString()+":"+MetadataTokens.GetToken(h).ToString("x")
   };
   foreach(var mh in md.MethodDefinitions) {
    var m=md.GetMethodDefinition(mh); string name=Name(mh);
    if(!(name.Contains("OnTempControlTimerTick") || name.Contains("LayoutUserControlBase")&&name.EndsWith(".get_ViewModel") || name=="ABI.Microsoft.UI.Xaml.IDependencyObjectMethods.GetValue" || name.Contains("TypedEventHandler_2_Microsoft_UI_Dispatching_DispatcherQueueTimer__object.Do_Abi_Invoke"))) continue;
    Console.WriteLine($"MODULE {file} METHOD {name} TOKEN {MetadataTokens.GetToken(mh):x}");
    if(file=="BiliCopilot.UI.dll") {
     var pdbFile=Path.Combine(frozen,"BiliCopilot.UI.pdb");
     using(var hashFile=File.OpenRead(pdbFile)) if(Convert.ToHexStringLower(SHA256.HashData(hashFile))!=manifest["BiliCopilot.UI.pdb"])throw new Exception("PDB hash mismatch");
     using var pdb=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(pdbFile)); var pm=pdb.GetMetadataReader();
     var guid=new Guid(pm.DebugMetadataHeader!.Id.AsSpan().Slice(0,16));
     if(!pe.ReadDebugDirectory().Where(d=>d.Type==DebugDirectoryEntryType.CodeView).Any(d=>pe.ReadCodeViewDebugDirectoryData(d).Guid==guid)) throw new Exception("Portable PDB GUID mismatch");
     Console.WriteLine($"PDB GUID={guid} matches PE; frozen hash verified");
     foreach(var point in pm.GetMethodDebugInformation(mh).GetSequencePoints().Where(p=>!p.IsHidden)) {
      var doc=pm.GetDocument(point.Document);
      Console.WriteLine($"SEQ IL={point.Offset} {Path.GetFileName(pm.GetString(doc.Name))}:{point.StartLine} checksum={Convert.ToHexStringLower(pm.GetBlobBytes(doc.Hash))}");
     }
    }
    if(m.RelativeVirtualAddress==0)continue;
    var body=pe.GetMethodBody(m.RelativeVirtualAddress); var il=body.GetILBytes()!;
    var ops=typeof(OpCodes).GetFields().Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)!).ToDictionary(o=>(ushort)o.Value);
    for(int p=0;p<il.Length;) {
     int at=p; ushort op=il[p++]; if(op==0xfe)op=(ushort)(0xfe00|il[p++]); var oc=ops[op]; string operand="";
     int n=oc.OperandType switch {OperandType.InlineNone=>0, OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar=>1, OperandType.InlineVar=>2, OperandType.InlineI8 or OperandType.InlineR=>8, OperandType.InlineSwitch=>4+4*BitConverter.ToInt32(il,p), _=>4};
     if(oc.OperandType is OperandType.InlineMethod or OperandType.InlineField or OperandType.InlineType or OperandType.InlineTok) operand=Name(MetadataTokens.EntityHandle(BitConverter.ToInt32(il,p)));
     else if(n>0 && oc.OperandType!=OperandType.InlineString)operand=Convert.ToHexString(il.AsSpan(p,n));
     Console.WriteLine($"IL_{at:x4} {oc.Name} {operand}"); p+=n;
    }
   }
  }
 }
}
