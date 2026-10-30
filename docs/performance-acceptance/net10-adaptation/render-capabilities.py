import json, pathlib
here=pathlib.Path(__file__).parent
rows=json.loads((here/'package-metadata.json').read_text(encoding='utf-8'))
api=json.loads((here/'api-diff.json').read_text(encoding='utf-8'))
def decision(name):
    if name.startswith('Richasy.AgentKernel.'):
        return ('AI connectors', 'Azure adds generated Edge speech JSON types; other connector XML surfaces show no added documented members. No new provider behavior inferred from version.', 'GlobalDependencies.cs; SettingsPageViewModel.AI.cs', 'Keep connector registration. Offline idle does not exercise provider protocol; authenticated/provider acceptance deferred (M per provider).', 'Build plus offline Settings; no live provider claim')
    if name=='Richasy.WinUIKernel.AI':
        return ('WinUI kernel','Updated preview package; no documented member addition identified in shipped XML comparison. Transitive Share/UI controls remain relevant.', 'SettingsPageViewModel.AI.cs; LayoutPageViewModelBase.cs','No proven hotspot requiring a new base class/API. Keep compatible composition; defer provider UI redesign (M).','Native Settings and first font display')
    if name.startswith('CommunityToolkit.WinUI.'):
        return ('Toolkit controls','8.2 servicing includes Segmented navigation/visibility, Sizer style keys, WrapPanel stretching and AOT layout fixes; not a new rendering ownership model.','Controls; Pages/SettingsPage.xaml','No observed need for custom Sizer style keys. Keep current XAML; do not attribute existing lifecycle fixes to Toolkit.','Controls7 and Player/Main; AOT not verified')
    if name=='CommunityToolkit.Mvvm':
        return ('MVVM','8.4.1 supports C#14 partial observable properties without preview; 8.4.2 fixes analyzer version ordering.','ViewModels/Core; ViewModels/View','Useful future AOT migration, but hundreds of fields span many domains (L). No mass rewrite; existing generated commands retained.','487 headless cases and generated app build')
    if name=='Microsoft.WindowsAppSDK':
        return ('Windows UI','2.6.4 adds unrestricted Language Model API access and improves shutdown/dispatcher diagnostics per release notes.','App.xaml.cs; Forms/MainWindow.xaml.cs','No new AI feature requested. Shutdown fixes do not supersede app-owned WIC/tray lifetimes. Runtime diagnostics benefit is passive, unquantified.','Debug/Release JIT; Controls7/Player15/Main4/20 cycles')
    if name=='Microsoft.Graphics.Win2D':
        return ('Graphics','WinAppSDK-branch changelog: 1.3.3 updates projections; 1.4 switches to WindowsAppSDK.WinUI and updates CsWinRT. Animated-control CPU/pause fixes predate old 1.3.2.','Controls/BasicCoverImage.cs; Controls/RecommendationCoverImage.cs; Controls/BlurCoverImage.cs','No new safe nonblocking WIC teardown API documented. Keep operation-owned stream lifetime; no historical-defect claim.','Controls7; decode/unload; physical device loss excluded')
    if name=='Microsoft.Windows.CsWinRT':
        return ('Projection/AOT','2.2 introduced explicit generation attributes; 2.3 stable includes expanded generated ABI/AOT support.','BiliCopilot.UI.csproj; XAML-bound partial types','Consume shipped projections with CsWinRTGenerateProjection=false; keep generator/analyzers. New annotations only when a demonstrated ABI gap exists.','Generated build and native controls; no AOT claim')
    if name=='WinUIEx':
        return ('Windows','2.9.2 SVG tray icons; 2.9.3 fullscreen persistence and explorer-restart tray reliability; no 2.9.3 API addition.','Forms/MainWindow.xaml.cs; Forms/PlayerWindow.xaml.cs','App tray uses H.NotifyIcon. Switching implementation is unrelated and would alter lifecycle. Existing window extensions retained.','Player modes and Main residency; no explorer restart test')
    if name=='H.NotifyIcon.WinUI':
        return ('Tray','2.4.1 provides net10 Windows assets (2.3.2 supports net9); repository commit pinned in nuspec.','App.xaml.cs; Toolkits/Pure/TrayLifecycle.cs','Adopt net10-compatible assets; no unsupported claim that newer package fixes app disposal races. Common 2.3.2 used only for runtime isolation.','Enable/disable/recreate, same HWND restore, explicit exit')
    if name=='HtmlAgilityPack':
        return ('Parsing','New documented UseAttributeOriginalName/GetXmlName overload and HtmlEntity.UseWebUtility.','Controls/Markdown; article rendering','Changing entity handling can alter output. No observed parser hotspot or requirement for original-case XML attributes.','Build; HTML semantic fixture is future M follow-up')
    if name=='Markdig':
        return ('Parsing','Shipped XML adds parser events, extension/options and helper surfaces; XML additions include internal APIs and are not all callable public APIs.','Controls/Markdown/MarkdownTextBlock.xaml.cs; Controls/Markdown/TextElements','Existing AST renderer depends on syntax semantics. Do not enable additional extensions or rewrite parser without a Markdown workload.','Compile AST consumers; dedicated rendering corpus deferred (M)')
    if name.startswith('Serilog'):
        return ('Logging','4.4 adds SelfMetrics and extra-template-argument SelfLog warnings; logging adapter targets updated abstractions; File sink unchanged.','GlobalDependencies.cs:AddSerilog; App.xaml.cs','No evidence sink drops events or is CPU hotspot. SelfMetrics listener would add measurement work; defer diagnostics feature (S/M). Existing level guards retained.','Build and native unhandled log audit')
    if name.startswith('Grpc.') or name=='Google.Protobuf':
        return ('Network/protobuf','Protobuf adds edition/naming/visibility feature descriptors. gRPC XML delta includes internal serialization-context leasing, not an application API.','externals/bili-kernel/src/BiliKernel.Grpc; Services.Media/Core/DanmakuClient.cs','Protocol is existing proto3 schema. Do not select a new edition or call internal pooling APIs. No on-network perf claim from offline fixture.','Regenerate/compile protobuf; network workload deferred (M)')
    if name=='System.Text.Json':
        return ('JSON','10 adds strict defaults, conditional ignore options, JsonElement.Parse and PipeReader parsing.','kernel SourceGenerationContext.cs and HTTP clients','Strictness changes server compatibility; PipeReader requires ownership/buffering redesign. No applicable bounded idle fix.','Source generator compile; future malformed/duplicate payload tests')
    if name=='Microsoft.Web.WebView2':
        return ('Web content','New documented worker lifecycle/messaging APIs.','Forms/WebSignInWindow.xaml.cs; Controls/Article/ArticleReader.xaml.cs','No worker-message requirement; avoid new cross-origin/security surface. Browser authentication is outside this offline run.','Compile; authenticated browser acceptance deferred')
    if 'Analyz' in name or name.startswith('Roslynator') or name.startswith('Microsoft.CodeAnalysis') or name=='Microsoft.Windows.CsWin32':
        return ('Build tooling','New compiler/generator/analyzer surfaces or diagnostics; not runtime hot-path APIs.','BiliCopilot.UI.ResourceGenerator; central PackageReference analyzers','Keep enforced rules; six prior diagnostics already adapted. No new blanket suppression. WinUIAnalyzer trial remains blocked as documented.','Production build with existing analysis settings')
    if name in ('Microsoft.NET.Test.Sdk','xunit','xunit.runner.visualstudio','coverlet.collector'):
        return ('Test tooling','Runner/coverage compatibility; xunit framework itself unchanged.','tests/BiliCopilot.UI.Tests/BiliCopilot.UI.Tests.csproj','Keep VSTest mode; switching to MTP provides no production performance evidence.','Fresh suite/TRX, expected 487 cases')
    if name.startswith(('Danmaku.','Richasy.MpvKernel','ComputeSharp.')):
        return ('Rendering engine','Selected version unchanged; no new version capability to adopt.','ViewModels/Core/DanmakuRenderViewModel; PlayerViewModel','No package-based fix for #49 pause. Keep renderer and native ownership; do not invent a new API.','Player15; #49 remains historical FAIL')
    if name.startswith(('Microsoft.Bcl.','System.Runtime.InteropServices','RichasyKernel.')):
        return ('Compatibility','Compatibility interfaces/container; no applicable new documented capability identified.','kernel abstractions; GlobalDependencies.cs','Keep compatible netstandard/kernel targets; do not remove shims required by other targets.','Restore all target graphs and compile')
    if name=='System.Reflection.Metadata':
        return ('Compatibility','10 documents declaring-type/assembly-name-info helpers.','App project workaround only','No application metadata reader. Remove direct net9 workaround after net10 restore; central unused declaration is inert.','Restore without downgrade; inspect graph')
    if name=='System.Drawing.Common':
        return ('QR graphics','Windows-only GDI+ assets for current runtime; documentation delta includes private helpers.','kernel Authorizers.Tv','No demonstrated QR hotspot. Keep Windows image lifetime; net10 removes the net9 Metadata workaround need.','Restore/build; real-account QR excluded')
    if name=='Microsoft.Windows.SDK.BuildTools':
        return ('Build tooling','Updated SDK packaging tooling, not application APIs.','BiliCopilot.UI.csproj and Windows resolvers','Keep minimum OS/Windows target unchanged; do not raise OS requirement with the tool version.','Build and owned package activation')
    if name.startswith(('Humanizer','Roman-','FluentIcons','ColorCode','QRCoder','System.Management')):
        return ('Presentation/utilities','No applicable new capability established for existing use; metadata and documented API comparison archived.','VideoItemViewModel; Markdown controls; SettingsPageViewModel; kernel Authorizers','Retain formatting/icon/QR/system-query semantics. No matching measured hotspot; no speculative rewrite.','Build/native visual checks; specialized locale/QR cases deferred')
    return ('Unclassified','No applicable capability established','See exact referring project','Do not manufacture a source change','Restore/build')

lines=['# Actual referenced package capability review','',
'Versions and repository commit provenance are in `package-metadata.json`; shipped XML member differences are in `api-diff.json`. These are documentation-surface comparisons across available TFMs, not a public-ABI compatibility proof. Missing XML is unknown, never "no APIs". Online NuGet existence was rechecked for each selected version; the retained one TLS failure has a successful explicit retry.','',
'Each row is an actual reference declaration (duplicate IDs across app/tests/kernel are intentional). The 58 unused central declarations and three kernel sample references are not represented as runtime adoption. Package fixes listed below are upstream claims; local historical defects are not automatically fixed.','',
'| Package / old resolved → selected | Capability reviewed | Actual code path | Decision / reason | Minimum acceptance |','|---|---|---|---|---|']
for row in rows:
    group,cap,path,why,accept=decision(row['id'])
    old=row.get('oldResolvedApp') or row['old']
    lines.append(f"| {row['id']} ({row['area']}): {old} → {row['selected']} | {group}: {cap} | {path}; reference: {', '.join(row['references'])} | {why} | {accept} |")
(here/'package-capabilities.md').write_text('\n'.join(lines)+'\n',encoding='utf-8')
