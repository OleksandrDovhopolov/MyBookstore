# Content Strategy — MVP (Day 1 → Day 7 → Day 30)

> Status: draft v1. Characters and locations are **placeholders**
> (`character_01`..`character_05`, `location_01`..`location_05`) — naming,
> personalities and settings are a separate pass. This document fixes the
> **shape** of the content (how much, in what order, wired to which system),
> not the narrative content itself.

Reference: [Tiny Bookshop Wiki](https://tiny-bookshop.fandom.com/wiki/Tiny_Bookshop_Wiki)
— specifically [Characters](https://tiny-bookshop.fandom.com/wiki/Characters),
[Challenges](https://tiny-bookshop.fandom.com/wiki/Challenges),
[Journal](https://tiny-bookshop.fandom.com/wiki/Journal). Used as a proven
in-genre pattern, not copied 1:1.

---

## 1. Scope

- **5 characters**, each with an independent quest chain ([CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md)).
- **5 locations**, unlocking sequentially ([LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md)).
- **Day 1** — must work standalone (FTUE, [FTUE.md](FTUE.md)).
- **Day 1–7** — first content arc, ships with MVP.
- **Day 8–30** — extension arc, only built out if Day 1–7 retention justifies it.
- **Endgame hook** — collecting 5 photograph fragments, one per completed
  character chain, assembled into one old photo. Primary proposal + one
  alternative (§7), both scoring against the same systems.

Everything below is expressed in terms of systems that already exist or are
in progress per [CATALOG.md](CATALOG.md), so each content item has a concrete
technical home instead of inventing new architecture.

---

## 2. System mapping (what content rides on what)

| Content concept | Backing system | Status |
|---|---|---|
| Character profile, discovery, memories | `Game.Characters` ([CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md)) | ✅ implemented (Stages 1–3) |
| Quest chain, tasks, conditions, permanent effects | `Game.Quest` over `Game.Conditions` ([ADR-0007](adr/0007-quest-system.md), [QUESTS.md](QUESTS.md)) | ✅ MVP core implemented |
| Character ↔ quest link (`CharacterId`, `Memories[]`) | [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md) | ✅ |
| Sequential location unlock (conditions, cost) | `LocationUnlock` + `Conditions` engine ([LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md)) | 🚧 vertical slice implemented (SalesStats provider only) |
| Passive / active book sales | Customer simulation ([ADR-0003](adr/0003-customer-simulation.md), [ADR-0004](adr/0004-stock-model-hybrid-sale-chance.md), [ADR-0006](adr/0006-passive-sales-requested-genre.md)) | ✅ |
| Day structure (Morning/Preparation/Sales/Results) | [CORE_LOOP.md](CORE_LOOP.md) | ✅ design fixed |
| Decor as sale-chance modifier | `IDecorModifierProvider` seam ([CORE_LOOP.md](CORE_LOOP.md) §2.3) | 🚧 slot exists, modifier is a stub |
| Fragment as a reward item | Quest reward flow + `Game.Inventory` ([INVENTORY.md](INVENTORY.md)) | ⏳ inventory feature is a stub — **blocking dependency**, see §8 |
| Fragment assembly UI, Journal "Characters" tab | Journal read-side already scoped in [CHARACTER_SYSTEM.md](CHARACTER_SYSTEM.md) | ⏳ UI not built |

The one real gap is **`Game.Inventory`** (currently a stub doc): a fragment
needs to exist as an item somewhere between "quest reward" and "assembly
screen." Until that lands, fragments have nowhere to live except a bespoke
one-off flag, which is the kind of shortcut that gets expensive later.

---

## 3. Day 1 — must stand alone

Day 1 has no character chains finished yet; it only has to prove the core
loop and open the first thread.

- **Location:** `location_01` only (already unlocked, matches [CORE_LOOP.md](CORE_LOOP.md) §2.1 — "MVP ships with one functional location").
- **Characters present:** `character_01` — discovery quest fires on day 1 (per
  [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md) §3, discovery = any
  `DiscoveryQuestIds` started). One short narrative beat only, per
  [CORE_LOOP.md](CORE_LOOP.md) §1 ("1 NPC line + Continue", no long dialogue
  in Morning).
- **Quests:** first task of `character_01`'s chain becomes available at
  Preparation or Results, not blocking Sales.
- **Sales:** passive + active sales fully live (already implemented system) —
  this is the actual gameplay of day 1, the character beat is a hook layered
  on top, not a replacement.
- **Decor:** none required yet — `DailyDecorSlots` seam exists but content
  can ship with zero decor items and add them from day 2–3 once
  `IDecorModifierProvider` has real values, or as a stub multiplier of 1.0 if
  it ships earlier.

Day 1 goal: player finishes one Morning→Results loop, sees `character_01`
introduced, and gets a visible task in the Journal for tomorrow.

---

## 4. Day 1–7 arc

Pace one character discovery and one location unlock roughly every 1–2 days,
front-loaded slightly faster than evenly-spaced so the Journal never looks
empty:

| Day | Locations unlocked | Characters discovered | Chain progress |
|---|---|---|---|
| 1 | `location_01` | `character_01` | chain 1 starts |
| 2 | — | — | chain 1, task 2 |
| 3 | `location_02` | `character_02` | chain 1 finale **or** chain 2 starts |
| 4 | — | — | chain 2 continues |
| 5 | `location_03` | `character_03` | chain 2 finale possible |
| 6 | — | — | chain 3 continues |
| 7 | `location_04` | `character_04` | chain 3 finale possible |

By end of day 7: 4 of 5 locations unlocked, 4 of 5 characters discovered,
at least one chain fully closed (first fragment awarded — see §6). This
mirrors [CORE_LOOP.md](CORE_LOOP.md)'s own progression arc ("Days 1–5: learns
first location" / "Days 5–10: unlocks 2–3 new locations"), just made
concrete with numbers instead of a range.

Location unlock conditions for this window should stay on the one provider
that already exists (`ISalesStatsReader` / `soldGenre`,
[LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md) §8) plus `UnlockCost`
gold gates — not on `IPlayerLevelProvider` or
`ICharacterRelationshipReader`, both of which are explicitly listed as **not
implemented** (§12 of that doc). Character-gated location unlocks are a
later-arc feature, not MVP.

---

## 5. Day 8–30 arc (conditional — only if Day 1–7 retains)

Do not author this in detail until Day 1–7 numbers justify it. Shape only:

| Window | Content |
|---|---|
| Day 8–14 | `character_05` discovered, `location_05` unlocked (all 5 of each now live) |
| Day 15–20 | Remaining chains progress; first decor items with real modifiers ship, replacing the stub multiplier |
| Day 21–30 | Remaining chain finales close; last fragments awarded; assembly payoff becomes reachable |

By day 30 all 5 chains should be closeable by an engaged player, but not
necessarily closed automatically — the fragment endgame (§6) should still
feel earned, not handed out on a timer.

---

## 6. Endgame feature — 5 photo fragments (primary proposal)

**Trigger:** each character chain's finale quest, on reaching `Awarded`,
grants a fragment item as its permanent effect/reward
([ADR-0007](adr/0007-quest-system.md) — permanent effects already carry
reward payloads; [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md) §5
calls this exact shape out as the "mentor / endgame" archetype: "final
'heir' → unlock content"). No new quest-engine capability needed.

**Storage:** fragment = one item type in `Game.Inventory` (blocking
dependency — §8). Five fragment ids, one per character.

**Assembly:** once all 5 are owned, a dedicated screen (or a Journal
"Characters" tab element, matching the reference's own Journal ↔ Characters
pairing) reveals the composed photo. This is a **new** UI surface — nothing
in the current UI system needs to change structurally
([UI_SYSTEM.md](docs/SERVICES/UI_SYSTEM.md) already supports arbitrary
windows.

**Risk to flag:** the payoff is only visible once **all five** chains are
done. If a player skips or stalls on one character, they get zero visible
payoff from this feature regardless of how much of the other four they
finished. See the alternative below for a lower-risk shape of the same
underlying mechanism.

---

## 7. Alternative variant (based on the reference's actual model)

Tiny Bookshop does **not** use a single shared collectible as its main
content payoff. Its [Journal](https://tiny-bookshop.fandom.com/wiki/Journal)
has a dedicated **Characters** tab where each character accumulates their
own **Memories** — one or more per character, unlocked independently as
their tasks are completed, each one a self-contained photo/story beat (see
also the [Memories guide](https://www.neoseeker.com/tiny-bookshop/Memories)
— e.g. Tilde alone gives several memories over the game, not one).

Proposed alternative, same underlying tech, different payoff shape:

1. **Each character chain finale grants its own Memory** (photo + short
   text), shown immediately in the Journal's Characters tab. This is exactly
   the `IsGolden` memory already described in
   [CHARACTERS_AND_QUESTS.md](CHARACTERS_AND_QUESTS.md) §3 — no new concept,
   just using the mechanism that's already specced.
2. **The 5-fragment photo becomes a bonus meta-goal layered on top**, not the
   only goal: the same finale reward also drops a fragment (as in §6), but
   the fragment is a secondary/hidden reward, discoverable once a player
   has closed 2+ chains, framed as "a stranger thing you're collecting" the
   Journal is right the surfaces separately from the required track.

This keeps §6's fragment mechanic intact for players who go for full
completion, but guarantees every finished chain gives an immediately visible
reward (a golden memory) instead of a payoff gated behind finishing all five.
It also degrades gracefully if content for one character slips past the Day
30 window — four finished chains still produced four visible memories.

**Recommendation:** ship the alternative shape for MVP (independent
memories, fragment as bonus), keep §6 as the literal "endgame" framing in
marketing copy since the mechanism is identical either way.

---

## 8. Open dependencies / blockers

- **`Game.Inventory`** ([INVENTORY.md](INVENTORY.md), currently ⏳) — needed
  to hold fragment items. This is the one piece of infrastructure this
  content plan cannot ship without.
- **`Game.Reward`** ([REWARD_SYSTEM.md](REWARD_SYSTEM.md), ⏳) — if fragment
  drops should go through a unified reward-presentation flow (toast/animation
  via [AnimationBuilder.md](INPROGRESS/AnimationBuilder.md)) rather than a
  bespoke fragment popup.
- **Decor** ([DECOR.md](DECOR.md), ⏳) — Day 1–7 arc above assumes decor can
  ship late (stub multiplier) without blocking the character/location
  content; confirm this is acceptable before locking the day-by-day table.
- **`IPlayerLevelProvider` / `ICharacterRelationshipReader`** — explicitly
  unimplemented per [LOCATION_UNLOCK_SYSTEM.md](LOCATION_UNLOCK_SYSTEM.md)
  §12; do not author location-unlock conditions against them for MVP.
