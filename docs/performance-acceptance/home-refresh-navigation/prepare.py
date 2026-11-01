"""Export a fresh offline production-source host without changing any existing checkout."""
import argparse
import io
import re
import shutil
import subprocess
import uuid
import zipfile
from pathlib import Path

p = argparse.ArgumentParser()
p.add_argument("--ref", default="HEAD")
p.add_argument("--kernel-repo", required=True, type=Path)
p.add_argument("--runtime-assets", required=True, type=Path)
p.add_argument("--output", default="artifacts/home-regressions-replay", type=Path)
args = p.parse_args()
root = Path(__file__).resolve().parents[3]
out = args.output.resolve()
if out.exists():
    raise SystemExit("Fresh output required; existing evidence is never overwritten")
source = out / "source"
fixture = out / "fixture"
source.mkdir(parents=True)
zipfile.ZipFile(io.BytesIO(subprocess.check_output([
    "git", "archive", "--format=zip", args.ref, "src", "tests", "packages",
    "global.json", "nuget.config", ".gitignore"], cwd=root))).extractall(source)
kernel = source / "externals/bili-kernel"
kernel.mkdir(parents=True, exist_ok=True)
gitlink = subprocess.check_output(["git", "rev-parse", f"{args.ref}:externals/bili-kernel"], cwd=root, text=True).strip()
zipfile.ZipFile(io.BytesIO(subprocess.check_output([
    "git", "-C", str(args.kernel_repo.resolve()), "archive", "--format=zip", gitlink]))).extractall(kernel)
ui = source / "src/Desktop/BiliCopilot.UI"
shutil.copytree(ui, fixture)
for name in ("libmpv", "ffmpeg", "bbdown"):
    shutil.copytree(args.runtime_assets / name, fixture / "Assets" / name, dirs_exist_ok=True)
for name, original in (("Directory.Packages.props", source / "src/Directory.Packages.props"),
                       ("global.json", source / "global.json"), ("nuget.config", source / "nuget.config")):
    shutil.copyfile(original, fixture / name)
config = fixture / "nuget.config"
config.write_text(config.read_text(encoding="utf-8-sig").replace(
    'value="packages/danmaku"', 'value="../source/packages/danmaku"'), encoding="utf-8")
(fixture / "Directory.Build.props").write_text(
    '<Project><PropertyGroup><LangVersion>preview</LangVersion><ImplicitUsings>enable</ImplicitUsings>'
    '<Nullable>enable</Nullable></PropertyGroup></Project>')
project = fixture / "BiliCopilot.UI.csproj"
text = project.read_text(encoding="utf-8-sig")
text = re.sub(r'<ProjectReference Include="([^"]+)"',
              lambda m: '<ProjectReference Include="' + str((ui / m[1].replace("\\", "/")).resolve()) + '"', text)
project.write_text(text, encoding="utf-8")
identity = "Mang.BiliCopilot.HomeRegression." + uuid.uuid4().hex[:12]
for name in ("Package.appxmanifest", "Package.Dev.appxmanifest"):
    manifest = fixture / name
    text = manifest.read_text(encoding="utf-8-sig")
    text = re.sub(r'(<Identity\s+Name=")[^"]+', lambda m: m[1] + identity, text)
    for guid in set(re.findall(r"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", text)):
        if guid != "00000000-0000-0000-0000-000000000000":
            text = text.replace(guid, str(uuid.uuid4()))
    manifest.write_text(text, encoding="utf-8")
# The shell and external discovery service are the only composition replacements.
app = fixture / "App.xaml.cs"
text = app.read_bytes().replace(
    b'GlobalDependencies.Kernel.GetRequiredService<AppViewModel>().LaunchCommand.Execute(default);',
    b'InitializeTrayLifecycle(); ControlledRefresh.Start();')
text = re.sub(rb'private const string Id = "[^"]+";',
              b'private const string Id = "' + identity.encode() + b'";', text)
app.write_bytes(text)
dependencies = fixture / "GlobalDependencies.cs"
text = dependencies.read_bytes().replace(b'.Build();', b'.WithHomeDiscovery().Build();', 1)
dependencies.write_bytes(text)
shutil.copyfile(Path(__file__).with_name("HomeRegression.cs"), fixture / "ControlledRefresh.cs")
(out / "source-ref.txt").write_text(subprocess.check_output(["git", "rev-parse", args.ref], cwd=root, text=True))
(out / "kernel-ref.txt").write_text(gitlink)
print(f"Prepared {fixture}; identity {identity}")
