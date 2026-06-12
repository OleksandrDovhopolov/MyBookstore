# Code Language Policy

All text inside C# source files in this repository (`Assets/**/*.cs` and any future code dirs) MUST be in **English** only.

## What is covered

- Code comments — both single-line (`//`) and block (`/* */`).
- XML doc comments (`///`).
- `Debug.Log` / `LogWarning` / `LogError` messages.
- Exception messages (`throw new ...Exception("...")`).
- TODO / FIXME / HACK notes.
- String literals that end up visible to the player or to a developer (UI placeholder text, log prefixes, error toasts).
- `#region` / `#endregion` labels.
- Conditional compilation symbol names (already English by convention).

No Russian, no mixed-language strings, no transliteration.

## What is NOT covered (Russian is OK)

- `docs/INPROGRESS/*.md` — GDD / spec docs (`Утро.md`, `Подготовка.md`, etc.).
- `docs/REFERENCE_*.md` — reference notes.
- `docs/archive/*.md` — historical material.
- `Assets/Configs/*.json` — **game content** (book titles, customer request lines, location display names). This is design data, not code; it will go through localization later anyway.
- Notion task descriptions / comments.
- Commit messages — either language is fine, but be consistent within a single message.
- Slack / chat / planning discussions.

## Rule for legacy code

There is **no blanket refactor**. The policy applies as work moves through the codebase:

- **New code** — English from day one, always.
- **Touched code** — when you edit an existing file that contains Russian, translate the Russian parts as part of the same edit. Don't leave a half-translated file.
- **Untouched code** — left alone. Don't open files just to translate them.

This keeps change-sets focused on actual work and avoids one giant noisy commit.

## Why

- Unity, .NET, Newtonsoft, VContainer, UniTask — all tooling logs, stack traces, and exception messages are in English by default. Russian inserts among them look broken.
- Code review by another person (or an LLM) is faster and more accurate in a single language.
- Future localization replaces hardcoded UI strings with keys — English placeholders translate cleanly; Russian placeholders require an extra rewrite step.
- The project source is going to be read by people who don't speak Russian (open-source contributors, code reviewers, support engineers).

## Exceptions

If a Russian comment is **genuinely useful** for reasoning (rare — usually a culture-specific game-design decision), add an English summary alongside it:

```csharp
// In TB the morning event acts as the "hook of the day"; we mirror that here.
// (RU: утренний контекст — главный драйвер дневных решений в референсе.)
private MorningDayContext _resolved;
```

Russian placeholder strings for content that will move to a config or localization table later — wrap with a TODO that points to the destination:

```csharp
// TODO: read from RequestConfig.Text once content is wired
_requestLabel.text = "Customer request goes here";
```

## Scope of immediate work

When this policy was introduced (commit referencing this file), only `Assets/Game/Features/BookSell/` was translated in a single pass. Everything else stays Russian until it is next touched, per the rule above.

## Where to find this rule

- This document: `docs/LANGUAGE_POLICY.md`.
- Project-wide AGENTS/CLAUDE instructions (if added later) should reference this file rather than restating it.
