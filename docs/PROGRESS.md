# Task Progress

Log of completed tasks for the MyBookstore project. Source of truth for tasks — Notion (database "Tasks before release (MVP)").
This file — local log of completed work for quick context and commit ↔ task linkage.

Record format:

```
## <Task title>
- **Notion:** <url>
- **Commit:** <short hash> — <message>
- **Date:** YYYY-MM-DD
- **Summary:** brief description
```

---

## Modular architecture skeleton (asmdef)
- **Notion:** https://app.notion.com/p/370511859db38186bbf5c93e68ea430c
- **Commit:** `754fcd1` — add base asmdef folders
- **Date:** 2026-06-08
- **Summary:** Created folders and root asmdef files for modules `Infrastructure`, `Core.Models`, `UIShared` and features `BookSell`, `Shop`, `Quest`, `RewardDrop`, `Inventory`, `IAP`, `Analytics`, `Save`. Existing `Game.Http` and `Game.Commands` moved under `Infrastructure/`. Cross-assembly `references` were not configured (folders are empty placeholders). `UISystem` skipped per decision. Rules — `docs/ASMDEF_RULES.md`.
