# live-partition-exit 证据与脚本

本目录是 `docs/performance-acceptance/live-partition-exit-diagnosis.md` 的可复算证据。
不包含转储本体、日志原文或用户数据；这些输入留在本机，见下方「外部输入」。

## 外部输入（未入库）

| 输入 | 本机位置 | 说明 |
| --- | --- | --- |
| 崩溃转储 | `%LOCALAPPDATA%\CrashDumps\BiliCopilot.UI.exe.<pid>.dmp` | `parse_dump.py` / `walk.py` 的输入；本目录只保存结构化结果 |
| 应用日志 | `%LOCALAPPDATA%\Packages\Mang.BiliCopilot.Dev_y1mf40g4r9srg\LocalState\Logger\log-<date>.txt` | `evidence.json` 的 `lastLog` 只引用文件名与行号，原文未入库 |
| WER 报告 | `C:\ProgramData\Microsoft\Windows\WER\ReportArchive\AppCrash_Mang.BiliCopilot_*` | 仅摘录签名到 `evidence.json` |

## 文件

| 文件 | 作用 |
| --- | --- |
| `parse_dump.py` | 纯标准库解析 WER minidump：异常流、线程、模块（含 PDB 标识）、stowed 记录；stowed 数组未被捕获时记录为证据缺口 |
| `walk.py` | 用 DbgHelp StackWalk64 走崩溃线程；用 TimeDateStamp+SizeOfImage 匹配本机 PE 兜底未捕获的代码页；可用公共符号服务器 |
| `repro-nav.ps1` | 通过 UI Automation 复现「直播 → 网游」与对照用例「直播 → 单机游戏」 |
| `evidence.json` | 汇总的崩溃事实（转储元数据、异常记录、关键模块、符号化栈、WER 签名、复现矩阵）；UTF-8 文本，日志行引用指向本机 `LocalState\Logger` |
| `36504-stacks.json` | 36504 转储的完整符号化栈（walk.py 原始输出） |

## 复算

```powershell
# 1) 解析转储
python parse_dump.py "%LOCALAPPDATA%\CrashDumps\BiliCopilot.UI.exe.36504.dmp" . 36504

# 2) 走栈（roots 传入安装目录与 WindowsAppRuntime 框架目录，供 PE 兜底与符号加载）
python walk.py "%LOCALAPPDATA%\CrashDumps\BiliCopilot.UI.exe.36504.dmp" . 36504 `
  --roots "C:\Program Files\WindowsApps\Mang.BiliCopilot.Dev_2.2611.7.8_x64__y1mf40g4r9srg;C:\Program Files\WindowsApps\Microsoft.WindowsAppRuntime.2_2.6.5.0_x64__8wekyb3d8bbwe"

# 3) 复现（先确认没有其他实例）
powershell -NoProfile -File repro-nav.ps1 -Fresh -Partition 网游
powershell -NoProfile -File repro-nav.ps1 -Fresh -Partition 单机游戏   # 对照：不退出
```

`evidence.json` 的 `walk.frames` 是 `36504-stacks.json` 的字段子集（`location` / `pc` / `symbol` / `displacement`），
可用两者逐帧对照复查。

符号缓存默认写到 `<out-dir>/symbols`（已被 git 忽略的路径下），可用环境变量 `SYMBOL_CACHE` 覆盖。
