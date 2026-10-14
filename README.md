# Stored Research Inline ETA

A RimWorld 1.6 and 1.7 UI mod that displays estimated research completion time in the lower-left inspect pane. The estimate stays beside the final research-progress line when space allows; otherwise, the complete label and time move to the next line.

## Features

- Accounts for active researchers across all maps, pawn and facility research stats, difficulty, and each researcher's current faction tech-cost factor.
- Deducts available stored research points when Stored Research is active; uses vanilla remaining cost otherwise.
- Refreshes the same project every 30 game ticks and recalculates immediately when the project changes.
- Removes the legacy standalone ETA line to prevent duplicate output.
- Provides English (`ETA`) and Simplified Chinese (`预计研究时间`) labels.
- Does not alter research points, research speed, settings, or save data.

## Dependencies

Harmony is required:
https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077

Stored Research is optional:
https://steamcommunity.com/sharedfiles/filedetails/?id=3024088640

The mod retains its original name to distinguish it from Show Research ETA, even though Stored Research is no longer required.

## Inspiration and credits

The feature concept was inspired by Show Research ETA by pureMJ:
https://steamcommunity.com/sharedfiles/filedetails/?id=3283434105

Implementation was developed with assistance from OpenAI Codex.

## Update — 2026-10-14

- Added RimWorld 1.7 support.
- Shortened the English label to `ETA`.
- The ETA moves to the next line when the research-progress text leaves insufficient space.

## Building

Both projects share the C# source and emit the same assembly name in separate version folders.

- **1.6:** `Source/ResearchBenchETACompactFix.csproj`, Release, .NET Framework 4.8.1. Override `RimWorld16ReferenceRoot` to a pinned 1.6 reference directory containing `Managed/` and `Harmony/`.
- **1.7:** `Source/ResearchBenchETACompactFix.1.7.csproj`, Release, .NET 10. Override `RimWorld17Root` and `Harmony17Root` for the installed 1.7 game and Harmony assemblies.

Game, Unity, and Harmony dependencies are not bundled. `LoadFolders.xml` selects the matching version's DLL and translations.

Both Release builds passed compilation with zero errors and warnings. The compiled layout helper passed 24 offline cases; real game-font rendering and gameplay regression still require in-game verification.

## 简体中文

在左下角检查面板中显示预计研究时间。空间足够时追加在最终研究进度行末尾，空间不足时将标签和时间一起移到下一行。

Harmony 为必需依赖，Stored Research 为可选兼容项。启用 Stored Research 时扣除可用储存研究点；未启用时使用原版剩余研究成本。

功能概念受到 pureMJ 的 Show Research ETA 启发，代码由 OpenAI Codex 协助编写。为与原 Mod 区分，沿用当前名称。

### 更新 — 2026-10-14

- 新增 RimWorld 1.7 支持。
- 英文标签缩短为“ETA”，中文保持“预计研究时间”。
- 优化长研究进度的显示：空间不足时，预计时间自动移到下一行。

## Links

Steam Workshop:
https://steamcommunity.com/sharedfiles/filedetails/?id=3783678785

GitHub:
https://github.com/vorsell/StoredResearchInlineETA
