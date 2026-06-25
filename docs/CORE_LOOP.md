# Core Loop — Design Overview

Compact design reference for the four-phase day cycle:
**Morning → Preparation → Sales → Results → (next day)**.

> Scope: design intent and decisions. Implementation status, file paths, and "who
> owns what" live in `docs/PROGRESS.md` (history) and Notion (current tasks).
> FTUE / first-day scripting is a separate document: `docs/FTUE.md`.

---

## At a glance

```
Morning (context)  →  Preparation (decisions)  →  Sales (gameplay)  →  Results (reward + hook)
                                                                              ↓
                                                                         next day
```

The day is one self-contained 3–6 minute session. Between phases the player makes
a **meaningful choice**; inside each phase the feedback is **readable in seconds**.
The core fantasy is not running an economy — it is *recommending the right book
to the right person*. Money is a side effect of doing that well, not the goal.

---

## 1. Morning — sets the context

Short, atmospheric screen that opens the day and gives the player a reason to
make deliberate choices in Preparation.

**What the player sees:** day number, weather, an active event/news line, one
demand modifier ("today students are looking for short non-fiction"), and an
optional story hook for the day.

**Inputs:** `DayConfig` (today's event/weather/hint/demand), `LocationConfig`
(audience and demand modifiers), Save (current day, gold, unlocked locations).

**Outputs to the next phase:** `Day`, `EventId`, `WeatherId`,
`ActiveModifierIds`, `DemandGenres`, `DemandTags`, `TargetLocationIds`.

**Rules:**
- Always reads in 5–10 seconds. No tables, no strategy panels. Atmosphere first.
- At least one piece of information must matter for Preparation. Otherwise the
  screen is decorative and players will start skipping it.
- One short narrative beat is OK (1 NPC line + Continue). Long dialogues belong
  in Sales or in a dedicated character arc, not here.

**Out of scope:** full calendar, multiple parallel events, generative news.

---

## 2. Preparation — the only strategic moment of the day

The player answers three questions, in descending order of weight: **where to
park the shop, which books to stock, which decor to slot.**

### 2.1 Location

The single most impactful choice of the day. Each location carries an audience,
demand genres/tags, and hidden modifiers tied to weather/event. The wrong place
on the wrong day is a quiet day.

The MVP ships with one functional location; the UI and data model still treat
"location" as a list so locked entries can be added without rework.

### 2.2 Books

The player picks N books from inventory to put on today's shelf, limited by a
per-player capacity. Capacity is **state**, not a UI constant: it starts at a
configured value and grows through shop upgrades.

| Knob                | Baseline (MVP)   | Notes                                             |
|---------------------|------------------|---------------------------------------------------|
| `DailyBookSlots`    | 12 (range 12–20) | Grows via shop upgrades; lives on player state.   |
| `MinDailyBooks`     | 1                | Prevents starting Sales with an empty shelf.      |
| `DailyDecorSlots`   | 1–2              | Same shape as books; modifier-bearing items.      |

Books carry: genre, tags, mood/tone, base price, rarity weight. The player picks
**under today's context** (location demand + morning modifier) — not abstractly.

### 2.3 Decor

In a mature build decor is **functional**, not cosmetic — each item modifies
sale chance for a genre or mood (a plant nudges cozy themes, a globe nudges
travel, a coffee machine extends customer stay). Decor accumulates between days
and is the long-term progression of the shop.

In the MVP decor is wired as a passthrough: the slot exists, items can be owned
and selected, but the modifier effect is a stub until the real decor feature
ships. The modifier seam is reserved (`IDecorModifierProvider`).

### 2.4 Validation

Before Sales opens: a location is picked, `MinDailyBooks ≤ N ≤ DailyBookSlots`
holds, decor count is within `DailyDecorSlots`, every chosen id exists in
inventory, no duplicates. Failed validation disables the Continue button.

### 2.5 Output

```json
{
  "selectedLocationId": "loc_downtown",
  "selectedBookIds":   ["book_001", "book_002", "..."],
  "selectedDecorIds":  ["decor_plant"],
  "activeModifiers":   ["weather_clear", "event_exam_week"]
}
```

---

## 3. Sales — the gameplay heart

Two interaction modes coexist on the same shelf:

### 3.1 Passive sales (background flow)

Customers walk in, browse, and either buy or leave on their own. The player
sees this as quiet income with small per-genre feedback ("sold: *Quiet Orbit*").

Each customer arrives with a small **desire profile** — 1–3 genres (usually 2).
Each passive attempt picks **one** of those genres at random (among the ones that
are actually stocked) and rolls **only that genre's** probabilistic gate
(`baseSaleChance × locationMod × decorMod`); on success a specific title is chosen
by weighted random over `RarityWeight`. Because the attempt commits to one genre
up front, the chosen genre is known on **both** a hit and a miss — so the feedback
bubble can show that genre's sprite either way. Passive sales **do not** use
tag/mood matching — that depth belongs to the active mini-game. (Design: see
[ADR-0006](adr/0006-passive-sales-requested-genre.md), refining the stage-1 gate
of [ADR-0004](adr/0004-stock-model-hybrid-sale-chance.md).)

If a passive roll fails, that ends the passive part of that customer's visit:
no further passive attempts are allowed, but the customer may still continue
into non-passive beats such as an active recommendation, a comment, or dialogue.

The shelf is a per-title inventory; the genre count is an aggregate over the
shelf, not a separate entity. The right-hand panel shows the count and, on
hover, the current sale chance — a direct progression hook ("optimise the
shelf/decor to nudge sale chance").

### 3.2 Active recommendations (the mini-game)

A customer approaches and asks for a specific recommendation, sometimes plainly
("something light") and sometimes specifically ("a sea story that's tense but
hopeful"). The request has hidden parameters — desired genres, tags, mood, and
sometimes a price cap.

The player opens the shelf, picks **one** card (or skips with a reason), and
gets a result tier: **Excellent**, **Normal**, **Failed**, or **Skipped**.

### 3.3 Scoring

A simple matrix of matches:

| Match                | Points |
|----------------------|-------:|
| Genre                |     +3 |
| Tag (per tag)        |     +2 |
| Mood/tone (per item) |     +1 |
| Price within budget  |     +1 |
| Location supports it |     +1 |

Thresholds: `0–2 → Failed`, `3–5 → Normal`, `6+ → Excellent`.

### 3.4 Feedback — "why" matters more than "how much"

Every result is shown with a **human-readable reason**, not a number:

> *Excellent. Matched: sci-fi, survival, engineering. The university nudges
> science themes today.*

This is the single most important UX rule in the loop. Without the reason the
recommendation feels random and the game stops teaching.

### 3.5 Tempo

5–8 customers per day, no time-of-day pressure, no patience clock. The day ends
when **all customers are served** or **the shelf is empty**. The player can also
close the shop early if they want. A day should feel *finished*, not *cut short*.

### 3.6 Sales output

```json
{
  "customersServed":         8,
  "manualRequests":          5,
  "salesCount":              6,
  "excellentRecommendations":2,
  "normalRecommendations":   3,
  "failedRecommendations":   1,
  "goldEarned":             74,
  "soldBookIds":           ["book_001", "book_004"]
}
```

---

## 4. Results — fixes progress, plants the hook

Short emotional summary; no tables.

**What the player sees:**

- **Revenue** — one number.
- **Quality of recommendations** — excellent / normal / failed counts.
- **Best match of the day** — one card: customer + book + reason ("Maria got
  *The Quiet Orbit*; we matched space, loneliness, smart").
- **One atmospheric line** — *"Today the shop smelled of rain and coffee; every
  book found its reader."*
- **Reward** — gold and reputation; sometimes a new book "found you" (a
  classifieds drop, a gift, a discovery).
- **Hook** — a hint about tomorrow ("Tilde mentioned a festival" / "Rain is
  expected later this week" / "A returning professor will visit the university").

After tap → **Next Day** → state advances and Morning opens for day N+1.

### 4.1 Reward math (MVP)

```
gold     = sum(soldBookPrice) + excellent * excellentBonus
rep      += excellent
rep      -= 1 if failed > 2
rep      = clamp(rep + delta, 0, +∞)
```

### 4.2 Idempotency

Restart on the Results screen does **not** double-grant. A dedicated save
module (`results.applied_rewards`) records which days have been applied; if the
day is already in the list, the screen rebuilds the summary from stored deltas
instead of recomputing them.

**Out of scope:** detailed per-customer log, social feed, supplier shop on the
Results screen, multi-screen post-day cinematics.

---

## Cross-cutting design principles

1. **Session length is sacred.** The full day must fit in 3–6 minutes. No phase
   should stretch beyond what its purpose requires.
2. **"Why" beats "how much."** Every recommendation result carries a reason. A
   sale without a reason is a number; a sale with a reason is a story.
3. **Location is the lever.** Even at one functional location, the UI must teach
   that this is *the* decision of the day.
4. **Cozy ≠ no challenge.** The challenge is *reading the customer*, not
   surviving the economy. Money pressure should never dominate the feel.
5. **Books carry character, not just price.** A book is genre + tags + mood +
   price + rarity weight. Strip the qualitative fields and customer requests
   stop being interesting.
6. **Decor is progression.** If decor stays purely cosmetic, the shop loses one
   of its return-loop hooks. Bake the modifier seam in from day one even if the
   effect ships later.
7. **Atmospheric micro-reviews.** Cheap-to-author flavour lines after sales pay
   off out of proportion to their cost — they are what makes the loop *feel*
   cozy.

---

## Progression arc

A single day does little. The loop's power is in **accumulation**:

| Stage          | Player experience                                           |
|----------------|-------------------------------------------------------------|
| Days 1–5       | Learns the first location, builds intuition for one demand. |
| Days 5–10      | Unlocks 2–3 new locations; starts choosing per day.         |
| Days 10–20    | Sees what "works" for which customer type, curates a collection, switches from reactive ("what do I have to give?") to proactive ("I want a specific book for this customer next Thursday"). |
| Late game      | Returning customers with arcs; theme-of-life books; small persistent stories. |

The arc is **not** built with quest markers and waypoints. It grows out of the
day loop itself — repeated, warm, with tiny micro-stories accruing in the
margins.

---

## Related documents

- `docs/FTUE.md` — first-time experience (current implementation + future
  scripted day 1).
- `docs/archive/REFERENCE_COZY_BOOKSHOP_DAY.md` — generic cozy-bookshop sim
  reference notes (insights distilled, no game names).
- `docs/adr/0003-customer-simulation.md` — Sales real-time simulation decision.
- `docs/adr/0004-stock-model-hybrid-sale-chance.md` — per-title + per-genre
  hybrid stock model and the probabilistic passive sale.
- `docs/PROGRESS.md` — implementation log per task.
