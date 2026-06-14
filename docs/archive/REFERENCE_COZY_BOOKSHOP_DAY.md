# Reference: a day in a cozy mobile-bookshop sim

> Design reference notes distilled from a successful reference title in the
> cozy-management genre. Game-specific names and external links have been
> stripped; what remains is the structural and emotional design that
> translates to any project in the same niche.
>
> This file is **archive-grade**: it documents "how the genre works" and was
> the source for `docs/CORE_LOOP.md` / `docs/FTUE.md`. Treat discrepancies
> between this file and the implementation plans as expected — this is
> reference, not spec.

---

## The day's rhythm

A day in this genre is a short (3–6 minute) self-contained session with four
phases. Between phases the player makes a **meaningful choice**; inside each
phase the feedback is **readable in a glance**. The attraction is not the
economy — it is a small situation in town: *today there are exams / a
festival / rain, people walk into my shop, I match books to them, they leave
happy or not*. Money is a side effect of doing this well, not the goal.

```
Morning (context)  →  Preparation (decisions)  →  Sales (gameplay)  →  Results (reward + hook)
                                                                              ↓
                                                                         next day
```

---

## 1. Morning — sets the context

A short "diary" page: what day it is, what is happening in town today (news,
event, holiday, weather), and optionally a brief story note or one NPC line.

What this genre gets right:

- The morning is **never overloaded**. It is not a strategy panel; it is the
  "opening of a book" — atmospheric, brief, calming.
- It always carries **at least one thread** that affects later decisions:
  weather (sun vs rain), an active event (exam week / festival / book club),
  a demographic hook (students gathering in district A today, tourists in B).
- Story is **threaded softly**: an NPC writes a note, mentions a book,
  recommends a place — not a quest with a marker, but a **hint** the player
  can use.

In the reference design, the morning is **a reason to wake up and play
again**, not a KPI summary.

---

## 2. Preparation — the only strategic moment

The player answers three questions in descending weight.

### 2.1 Where to park the shop (the main decision of the day)

The shop is a **mobile cart on a small van/trailer**. The player picks a spot
on a city map: a park, a waterfront, a university district, near a market, by
the beach, by the train station, and so on. Each location has:

- an **audience** (students / families with kids / tourists / regulars /
  hobbyist niches);
- a **demand profile** in genres and themes (universities lean toward
  popular-science and philosophy; parks lean toward kids' books and light
  romance; waterfronts toward travel and adventure);
- **hidden modifiers** under event/weather (a park empties out in the rain;
  exam week buffs the university).

This decision **shapes the rest of the day**: pick correctly and the day
flows; pick poorly and the shop is empty and the player is frustrated.

### 2.2 What to put on the shelves

Inventory holds books the player has acquired — bought, found, gifted,
event-rewarded — and the day's shelf has **limited slots**. The choice: which
of those books to bring today.

Books carry:

- **genre** (popular science, children's, mystery, romance, classics, poetry,
  speculative fiction, non-fiction, …);
- **tags / themes** (loneliness, travel, survival, love, history, science,
  …);
- **mood / tone** (smart, cozy, dark, romantic, optimistic, …);
- **price** and **rarity**.

The key beat: the player should pick **against today's context**, not
abstractly. Exam week in the district → short non-fiction and study aids. A
children's book festival → children's books. Rain → cozy and atmospheric.

### 2.3 Decor and shop items

In this genre, decor is not cosmetic — it is **functional**. The player has
slots for items that **buff a specific genre or mood**:

- a houseplant → cozy / nature themes;
- an old lamp → cozy mood;
- a globe → adventure / travel;
- a coffee pot → extends customer stay (more chances for additional sales);
- rare seasonal pieces → unlocked by progression / events.

Decor accumulates between days and is the **long-term progression of the
shop**.

Early in development the reference shipped less decor and added it
iteratively; by the mature build it is a system with noticeable depth.

---

## 3. Sales — the gameplay loop

This is the **heart of the game**. After the shop opens in a location,
customers arrive one by one. The thing the whole game is built around
happens: **matching a book to a person**.

### 3.1 Two interaction modes

**Background sales.** Some customers buy "on their own" — they walk in, glance
around, pick something on genre vibes, and leave. A passive stream that the
chosen assortment and decor power on their own. The player sees this as
small ticking coins and "sold: X."

**Active recommendations.** Some customers **approach and ask for advice**.
This is the moment of play. The request is sometimes plain ("something
interesting?") and sometimes specific ("I want a book about the sea, not a
scary one, with hope in it"). Hidden parameters under the line: desired
genres, tags, mood; sometimes a price ceiling.

The player:

1. Opens the shelf.
2. Sees book cards (title, genre, price; sometimes tags).
3. Picks **one** and hands it over.
4. Gets a **result** — perfect / okay / great / poor.

A clean alternative: **decline** ("nothing here suits you"). That is an honest
out when the shelf truly does not match the request.

### 3.2 Scoring and feedback

Under the hood — a simple **match matrix**: genre matches for many points, a
tag for fewer, a mood for fewer still, price-in-budget for a small bonus,
the location buffing a theme for a small bonus.

What sets this genre apart is that the player **sees the reason** for the
result. Not "7/10 matched" but a **human-readable line**:

> *Great choice. Matched: sci-fi, survival, hope. And the university crowd
> loves science stories today.*

This is the difference between a sterile simulation and a game the player
wants to play again. Without the reason the feedback feels random; with it,
the player **learns**.

### 3.3 Atmospheric mini-reviews

After a good recommendation the customer often leaves a **short review** —
one or two lines along the lines of *"Finished it in one evening, cried,
thank you."* Sometimes the review is signed (*"From Margaret's diary, July
14"*). It does not matter for the economy, but it is **the** thing that makes
the game feel *cozy* — the player feels real people pass through the shop
with their own little stories.

### 3.4 Pacing

A fixed flow of customers per day (a handful of active requests plus
background sales), no stress timer. The player can close the shop early if
they want to. A day feels **finished**, not "cut short" — this is a conscious
design decision. The challenge is reading the customer, not surviving the
clock.

---

## 4. Results — fixes progress, plants the hook

A short screen, **no tables**. This genre deliberately leans on emotion:

- **Revenue** — one number.
- **Quality of the day** — counts of excellent / normal / failed
  recommendations.
- **Best match of the day** — one card: *"Best recommendation: [name] got
  [book]. Matched [tags]. Review: '…'"*.
- **One atmospheric line** — *"Today the shop smelled of rain and coffee, and
  every book found its reader."*
- **Reward** — gold, reputation, **sometimes** a new book "found you" (from
  a book club, a flea-market find, a gift from a fan, …).
- **Hook** — a hint about tomorrow ("[NPC] mentioned a festival" / "Rain is
  due this week" / "A famous professor returns to the university").

After the tap the day changes. Between days the player can **buy more books**
from a supplier, **buy decor**, **rearrange shelves** — short menus, not
separate phases.

The design goal of this screen is **not to close the books — it is to plant
a reason to open the game tomorrow.**

---

## Progression arc

A day in this genre changes **nothing on its own**. The power is in
**accumulation**:

- In 3–5 days the player learns their first location.
- In 10 days they unlock 2–3 more locations.
- By days 15–20 they start understanding which books work for which customer
  type and curate a **collection** — switching from reactive ("what do I have
  → who gets it?") to proactive ("I want a specific book for this customer
  by Thursday").
- Late game adds **character arcs**: returning customers with long stories
  who need books matched to their theme of life.

This genre does **not** build the arc with quest markers on a map. It grows
**out of the day itself** — repeated, warm, with tiny micro-stories.

---

## Critical takeaways for any project in this niche

1. **A day must feel finished in 3–6 minutes.** Do not stretch any phase.
2. **"Why" beats "how much."** Feedback after a recommendation = text with an
   explanation. Without it, the game feels random.
3. **Location is the lever of the day.** Even with one location at MVP, the
   UI must teach that this is *the* decision.
4. **Morning sets the context, results plant the hook.** Without either, days
   blur into "another shift survived."
5. **Books are inventory with character, not a catalogue.** Genre + tags +
   mood + price. Strip any of these on MVP and customer requests stop being
   interesting.
6. **Cozy ≠ no challenge.** The challenge is **reading the person**, not
   surviving the economy.
7. **Decor is shop progression.** In the reference it has functional
   meaning. If MVP keeps decor cosmetic, the project loses one of the main
   return-loop hooks.
8. **Atmospheric mini-reviews** are cheap text assets with very high return
   value — every version of the game should ship them.
