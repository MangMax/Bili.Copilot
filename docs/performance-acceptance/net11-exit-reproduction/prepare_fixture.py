"""Fresh isolated source extraction, fixture composition and necessary host build.

Requires existing exact RC3 SDK, restored local kernel and native asset files used
by the sealed helper. Never deploys, copies credentials or changes the checkout.
"""
import io
import pathlib
import shutil
import subprocess
import tarfile

root = pathlib.Path.cwd()
base = root / 'artifacts/exit-reproduction'
source = base / 'source'
fixture = base / 'fixture'
assert not source.exists() and not fixture.exists(), 'Use a fresh diagnostic checkout; do not overwrite evidence'
def archive(command, target):
    data = subprocess.check_output(command)
    with tarfile.open(fileobj=io.BytesIO(data)) as tar:
        for member in tar:
            if member.isfile():
                p = pathlib.Path('\\\\?\\' + str(target.joinpath(*member.name.split('/'))))
                p.parent.mkdir(parents=True, exist_ok=True)
                p.write_bytes(tar.extractfile(member).read())
archive(['git', 'archive', 'f6ed3ec063df8701bb57115abb40c4f7bcffc386', 'src', 'global.json', 'nuget.config'], source)
archive(['git', '-C', 'externals/bili-kernel', 'archive', '0ae5e4e000a41a07f6696746706df11c06e8835b'], source / 'externals/bili-kernel')
for name in ('harness-48-duration-fix', 'integration-pr27-pr43', 'dependency-upgrade', 'net10-adaptation'):
    shutil.copytree(root / 'docs/performance-acceptance' / name, source / 'docs/performance-acceptance' / name)
helper = source / 'docs/performance-acceptance/harness-48-duration-fix/prepare.ps1'
helper.write_text(helper.read_text(encoding='utf-8-sig').replace('git -C $SourceRoot ls-files src/Desktop/BiliCopilot.UI',
                  "git -C '" + str(root).replace("'", "''") + "' ls-files src/Desktop/BiliCopilot.UI"), encoding='utf-8')
pwsh = shutil.which('pwsh')
assert pwsh
subprocess.run([pwsh, '-NoProfile', '-File', str(source / 'docs/performance-acceptance/dependency-upgrade/prepare.ps1'),
                '-Destination', str(fixture), '-RunId', 'XBC1Diag', '-Kind', 'player', '-UseCurrentPackages'], check=True)
subprocess.run([pwsh, '-NoProfile', '-File', str(root / 'docs/performance-acceptance/net10-adaptation/prepare-observer.ps1'),
                '-Fixture', str(fixture)], check=True)
shutil.copyfile(source / 'global.json', fixture / 'global.json')
subprocess.run(['python', str(pathlib.Path(__file__).with_name('instrument.py')), str(fixture)], check=True)
(fixture / 'source-provenance.txt').write_text('f6ed3ec063df8701bb57115abb40c4f7bcffc386 kernel=0ae5e4e000a41a07f6696746706df11c06e8835b diagnostic metadata instrumentation\n')
sdk = r'C:\Program Files\dotnet\dotnet.exe'
assert subprocess.check_output([sdk, '--version'], cwd=fixture).decode().strip() == '11.0.100-rc.3.26522.148'
command = [sdk, 'build', 'BiliCopilot.UI.csproj', '-c', 'Release', '-p:Platform=x64', '-p:PublishAot=false',
           '-p:AppxPackageSigningEnabled=false', '-p:GenerateAppxPackageOnBuild=false',
           '-p:SolutionDir=' + str(source / 'src') + '/', '-p:SourceRoot=' + str(source),
           '-p:BuildOutDir=' + str(base / 'references') + '/']
with (base / 'build.log').open('wb') as log:
    subprocess.run(command, cwd=fixture, stdout=log, stderr=subprocess.STDOUT, check=True)
