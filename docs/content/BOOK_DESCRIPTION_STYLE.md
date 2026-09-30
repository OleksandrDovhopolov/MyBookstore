# Book description style

The single source of the writing rules for the book-description rewrite (CONTENT-1 in
[RELEASE_TASKS.md](../RELEASE_TASKS.md)). Written to be handed to a writer verbatim — everything below
the line is the brief.

The numbers here are mirrored from `BookDescriptionDraftValidator` (`MinReasonableLength`,
`MaxReasonableLength`, `OverlapWindowWords`). **C# is authoritative**; a test fails if this file and the
constants disagree, so change the constants first.

---

## The job

Write the shop-card description a player reads when deciding whether a book answers a customer's
request. One paragraph, one to three sentences.

## Clean room — the rule the whole task exists for

The catalogue's current descriptions were copied from another product and are being replaced. So:

- **Never read the text being replaced.** Not `localization_books_en.json`, not
  `book_descriptions_seed_en.json`, not the game's published copy, not a wiki or store page that
  reproduces it. If you cannot write without it, say so instead of writing.
- Write from the metadata given to you (title, author, genres, qualities, published, pages) plus your
  own knowledge of the book. `books.json` holds no prose at all, which is why it is the only catalogue
  file a writer ever needs.
- Paraphrasing the old wording is not a fix — it produces a derivative of it. The validator warns when a
  rewrite shares a run of 8 words or more with **any** seeded description, not just the one it replaces.

## Voice

Allowed, and wanted:

- warmth and dry wit — the shop has taste and is not afraid to show it;
- addressing the reader: "you", rhetorical questions, the occasional exclamation;
- a verdict. "Bleak, and still the standard reference" tells a player more than a neutral summary.

Not allowed:

- **first person** — no "I", "my", "we", "us". The card presents the book; it is not the bookseller
  talking. This is an error, not a preference.
- **jokes at the real author's expense**, or addressing them ("George, where is the next one?"). Real
  people, and the one habit that made the copied text instantly recognizable.
- marketing noise: "a must-have", "a wild journey", "for all those readers out there".
- spoilers of a twist the book sells itself on.

## What the text must carry

- The **primary genre** (the first genre listed) must be inferable without naming the tag. A player
  matches a customer's request to a book by reading this line.
- At least one of the listed **qualities** must show through — `Animals`, `Tragic`, `Light Reading`,
  `Female Author`, `Series` and so on are what requests are built from.
- Nothing that contradicts the metadata: not the genre, not the era, not the length.

## Hard constraints

- **130-265 characters** inclusive. Shorter reads like a stub, longer starts pushing the card layout.
- Single paragraph: no line breaks, no tabs, no double spaces, no leading or trailing space.
- Never use `{` `}` `<` `>` — the text renderer parses them as rich-text tags and eats the sentence.
- Straight quotes and apostrophes only. No smart quotes, no em dashes, no replacement characters.
- Do not restate the title as the whole sentence.

## Facts

You are describing real books, and a wrong plot summary on a real title is worse than a dull one.

- Report `recognized: false` for any book you do not actually know. That is not a failure — it routes
  the row to review.
- When `recognized` is false: no plot claims, no character names, no places, no dates. Write from genre,
  qualities, era and length only.
- Never invent a year. A year in the text that disagrees with `published` is flagged automatically.

## Output

A JSON array, nothing else:

```json
[
  { "id": "book165", "recognized": true, "new": "..." }
]
```

## Calibration

The first pass of this rewrite obeyed a stricter rule — strict third person — and the result was
correct and lifeless. For `Charlotte's Web` (Kids/Classic; Animals, Tragic, Light Reading, Nature):

> **too dry:** A spring pig is saved from the smokehouse by a barn spider who knows how to write, and
> how to spend what time she has. A farmyard story that does not pretend death away.

> **right:** A spring pig is marked for the smokehouse until a barn spider decides otherwise, spelling
> out his rescue one word at a time. Gentle, funny, and quietly honest that a friend can run out of time.

Both say the same things about the book. The second one has a temperature. Do not reuse this phrasing —
it is here to show the register, not to be copied.
