# Remaining-work decisions

2026-10-31. User decisions recorded before implementation; these are not validation results.

- Complete the existing remaining work (#49/#61, Native AOT validation and #56 resource investigation) before starting the newly proposed dependency-capability improvements.
- After a paused seek or renderer switch, reconstruct still-active danmaku at the target time using the current font size and speed. Do not preserve and replay the full history of speed, font or density changes. Normal playback behavior remains unchanged. Exact per-mode lifetime, layout and compatibility rules still need specification and tests; this decision alone does not close #49.
- Installation through the existing Visual Studio Installer of missing Native AOT MSVC C++ x64/ARM64 tools and Windows SDK components is authorized. UAC and large downloads were disclosed. Do not automatically restart the computer or reinstall the IDE. Detect existing components and add only what is required.

The reconstruction decision supersedes the pending user choice in the revision-2 design and review. It does not waive their resource-lifetime findings or the B1 capability gate. It does not authorize publishing an external engine package or declare AOT supported on untested architectures.
