// Never interprets or records argv. A per-launch owned directory is the only control channel.
using System.Text.Json;
var root = Environment.GetEnvironmentVariable("BILI55_CASE") ?? throw new InvalidOperationException();
File.WriteAllText(Path.Combine(root, "child.json"), JsonSerializer.Serialize(new { schemaVersion=1, pid=Environment.ProcessId, startedUtc=DateTimeOffset.UtcNow }));
var until = DateTime.UtcNow.AddMinutes(3);
while (!File.Exists(Path.Combine(root,"release")) && DateTime.UtcNow < until) await Task.Delay(40);
var code = File.Exists(Path.Combine(root,"release")) ? int.Parse(File.ReadAllText(Path.Combine(root,"exit-code"))) : 91;
File.WriteAllText(Path.Combine(root,"child-exit.json"), JsonSerializer.Serialize(new {schemaVersion=1, pid=Environment.ProcessId, code, exitingUtc=DateTimeOffset.UtcNow}));
return code;
