using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
var output=Path.GetFullPath(args[0]);Directory.CreateDirectory(Path.Combine(output,"source-documents"));var rows=new List<object>();
foreach(var dll in Directory.GetFiles(output,"*.dll")){
 using var pe=new PEReader(File.OpenRead(dll));var md=pe.GetMetadataReader();
 using var pdb=MetadataReaderProvider.FromPortablePdbStream(File.OpenRead(Path.ChangeExtension(dll,".pdb")));var pm=pdb.GetMetadataReader();
 var cv=pe.ReadCodeViewDebugDirectoryData(pe.ReadDebugDirectory().Single(x=>x.Type==DebugDirectoryEntryType.CodeView));
 var id=new Guid(pm.DebugMetadataHeader.Id.Take(16).ToArray());if(cv.Guid!=id)throw new Exception("PDB mismatch");
 var docs=new List<object>();foreach(var h in pm.Documents){var d=pm.GetDocument(h);var source=pm.GetString(d.Name);var expected=Convert.ToHexString(pm.GetBlobBytes(d.Hash));byte[] bytes;
if(File.Exists(source)) bytes=File.ReadAllBytes(source);
else {
 var c=pm.GetCustomDebugInformation(h).Select(x=>pm.GetCustomDebugInformation(x)).Single(x=>pm.GetGuid(x.Kind)==new Guid("0e8a571b-6926-466e-b4ad-8ab04611f5fe"));
 var blob=pm.GetBlobBytes(c.Value);var size=BitConverter.ToInt32(blob,0);
 if(size==0)bytes=blob.Skip(4).ToArray();else{using var def=new System.IO.Compression.DeflateStream(new MemoryStream(blob,4,blob.Length-4),System.IO.Compression.CompressionMode.Decompress);using var dst=new MemoryStream();def.CopyTo(dst);bytes=dst.ToArray();if(bytes.Length!=size)throw new Exception("embedded size");}
}
var hash=Convert.ToHexString(SHA256.HashData(bytes));if(hash!=expected)throw new Exception("Source mismatch: "+source);File.WriteAllBytes(Path.Combine(output,"source-documents",hash+".source"),bytes);docs.Add(new{source,hash});}
 rows.Add(new{path=dll,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(dll))),mvid=md.GetGuid(md.GetModuleDefinition().Mvid),codeView=cv.Guid,documents=docs});
}
File.WriteAllText(Path.Combine(output,"metadata.json"),JsonSerializer.Serialize(rows,new JsonSerializerOptions{WriteIndented=true}));Console.WriteLine("Verified "+rows.Count+" DLL/PDB pairs and every source document.");
