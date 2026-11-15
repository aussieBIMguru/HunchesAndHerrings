# Hunches & Herrings

A Victorian point-and-click murder mystery. Every case is generated from a seed: a fresh house party, a fresh body, a fresh set of lies. Walk the manor, question the household, search the rooms, catch the contradictions, and name the murderer, their motive and your key evidence before the Chief Constable arrives.

Built with **WPF on .NET 10**, with no NuGet dependencies. The map, the portraits, the paper texture, the music and the sound effects are all generated in code.

## Running it

1. Open `HunchesAndHerrings.slnx` in Visual Studio 2026 (or any IDE with the .NET 10 SDK).
2. Set **HunchesAndHerrings** as the startup project and press F5.

Saved cases go to `Documents\Hunches & Herrings\*.hhcase`. To build an installer, see [Releasing](#releasing-installer).

## Solution layout

```
src/
  HunchesAndHerrings.Core/      Pure game logic (net10.0, no UI)
    Model/                      Manor, rooms, characters, traits, clues, facts, difficulty
    Generation/                 CaseGenerator (the mystery builder), names, roles, all prose templates
    Game/                       GameSession (clock, actions, sergeant, hints), contradictions, saving
  HunchesAndHerrings/           WPF front end (net10.0-windows)
    Themes/Ink.xaml             Sepia ink-on-paper styles
    Rendering/                  Hand-drawn map, ink portraits, paper texture
    Audio/                      Karplus–Strong harpsichord loop and effects, MediaPlayer playback
    MainWindow*.cs              Room/interview panel, notebook, overlays (title, briefing, accuse, verdict)
```

## How a case is built

`CaseGenerator` (split across `CaseGenerator*.cs`) builds solvability in from the start. It doesn't generate a case and then check it:

- **The story.** It first picks a `CaseModel`: a lone killer, two conspirators, the children together, a house of secrets, a secret heir, an accident made to look like murder, the doctor's poison or blunder, an affair, the wrong victim, or an impostor. The cast is built to fit (children for a family pact, a doctor for the doctor's cases, and so on).
- **The lockdown.** Why nobody can leave (flood, snow, police orders, quarantine, the will, the tide, …), and whether callers from outside may come today.
- **The crime.** A room, a time (10 or 11 pm), a weapon or poison, and two or three **scene indicators** drawn from the 41 facets in `TraitCatalog` (pipe ash, a stammering voice, violet ink, hobnails, a fear of heights, …). Whoever was in the scene (the "hand") is made to match them. On harder ranks an extra mark is planted that someone can explain away.
- **Innocents, split two ways.**
  - **Twins** match every indicator, so they can only be cleared by an **alibi**: a partner, a neighbour's glimpse, the bell register, a child's sighting, or the visitors' book.
  - **Others** fail at least one indicator. That trait is revealed by one or two **truthful sources**: their own word, gossip, or a document in their room.
- **The lie.** The culprits (or, in an accident, the meddler) claim to have been elsewhere. A witness alone in that room, a room provably unused, or the visitors' book exposes it.
- **Secrets.** Innocents with secrets lie about where they were earlier; the proof is among their things, and confronting them with it brings a confession.
- **Noise.** The hand lies about one trait, a culprit may frame someone, the sergeant sometimes mishears, and the optional rival detective trades tips that aren't all true.

Every statement and document carries structured **facts** (`LocationFact`, `TraitFact`, `OccupancyFact`, `PresenceFact`, `ExplainedFact`, …). These drive the suspect list, the dossier and the automatic contradiction finder.

## Play features

- **Your detective:** enter your name and choose male or female. The household then addresses you as "Detective Grey", "sir" or "ma'am", and so does the sergeant. Your name is remembered for next time.
- **Title screen:** pick a rank (Novice, Inspector or Master), then tune the case: number of suspects, deadline, how often the sergeant rescues you, red herrings on or off, and a sergeant who never mishears. Enter a case number for a fixed mystery, roll the dice for a random one, or resume a recent save.
- **Case names:** every case gets a generated name, e.g. *The Case of the Crooked Candlestick*, *Murder at Ravensmere Hall* or *Death in the Library*.
- **The journal:** a leather-bound book with ruled pages. When anything is noted, it opens and a quill writes the entry in, line by line. Click anywhere to skip. Flip pages, filter by tab (All, Circled, Scene, Papers, Statements, Clashes), click a title to **circle** it in red ink, and scrutinise statements right on the page.
- **The blueprint:** each floor is drawn as an architect's plan: walls, door swings, windows, furniture and stairs.
  - **Crime scene:** a red room with a chalk outline and a "Scene of the Crime" stamp.
  - **Your trail:** footprints show the routes you've walked, and numbered stops show where you halted.
  - **Markers:** a "You are here" pin and a ★ SGT badge for the sergeant.
  - **Floor tabs:** glyphs show where you are (◉), where the sergeant is (★), the murder floor (✕) and your watched suspects (◯).
- **Whereabouts at any hour:** step through the day with ◀ ▶.
  - **Known people** (seen, or told by the household) are solid markers.
  - **Rough whereabouts** come from the household routine: room-level on Novice or for watched suspects, otherwise "somewhere upstairs".
- **Watch suspects:** circle up to three suspects (◯) and they're ringed in amber on the plan, so you always know roughly where to find them.
- **The sergeant chat** (bottom right): type plain English or use the quick buttons, e.g. *"search the library"*, *"question the cook"*, *"where is the colonel?"*, *"what next?"*, *"any thoughts?"* or *"recap"*. The sergeant has a personality, runs errands while you work, sometimes gets things wrong, and rescues you when you fall behind.

- **Clock:** walking costs 5 min (10 for stairs), each question 10, a search 20. The day runs 8 am to 6, 5 or 4 pm depending on difficulty.
- **Schedules.** People move room to room through the day. Ask anyone "Where is everyone today?" to plan your route.
- **Deduction.** You earn 1 point per clue and 2 per contradiction. Spend points on a **Hunch** (a nudge toward a key clue) or to **Scrutinise** a statement (reliability depends on difficulty).
- **Sergeant.** Send them to search a room or question someone while you work. Results can be partial and, on harder settings, wrong. If you're well behind at the half and three-quarter marks, they volunteer a finding (2 rescues on Novice, 1 on Inspector, none on Master).
- **Verdict.** Naming the right murderer wins. Motive and key evidence decide your grade (A to C). After the verdict you see the full truth, how each innocent could have been cleared, and what you missed.
- **Seeds.** Every case has a number. Enter it on the title screen to replay or share a mystery.

## Releasing (installer)

Same routine as Pixel8 and Throng. Needs the .NET 10 SDK and [Inno Setup 6](https://jrsoftware.org/isinfo.php) (a
build tool, not shipped).

1. Set the version in `Directory.Build.props` (`<Version>`, currently **0.4.0**). That's the only place it lives.
2. Close Visual Studio and double-click `publish.bat` (or `powershell -ExecutionPolicy Bypass -File build\publish.ps1`).

The script:
- builds the solution in Release (it runs the tests instead, once a `tests\` project exists);
- publishes HunchesAndHerrings self-contained for win-x64 to `artifacts\app\`;
- builds `artifacts\HunchesAndHerrings-Setup-<version>.exe`, plus its SHA-256.

Switches: `-SkipTests`, `-InnoCompiler <path to ISCC.exe>`, and `-Sign -CertThumbprint <thumbprint>` (signing is
off for now).

The setup installs per user (no admin) to `%LocalAppData%\Programs\Hunches & Herrings` and adds a Start menu
shortcut (a desktop shortcut is optional). It asks you to close the game if it's running. Uninstalling keeps your
saved cases (`Documents\Hunches & Herrings`) and your detective profile (`%LocalAppData%\HunchesAndHerrings`). Unsigned builds show a SmartScreen warning the first time (More info ▸
Run anyway).

To release, add an entry to [CHANGELOG.md](CHANGELOG.md), tag the commit (`v0.4.0`), and attach the setup and its
`.sha256.txt` to a GitHub release. `clean.bat` removes all bin/obj folders. The installer's parchment artwork comes
from `tools/make_installer_art.py`.

Credits and licences are listed in [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md).

## Logo

`Assets/HunchesAndHerrings.ico` and `.png` hold the HH monogram in a Victorian calligraphic hand on scorched parchment. To regenerate them, use `tools/make_icon.py` (Python with Pillow).

## Extending

- **New prose:** `Generation/TextBank.cs` holds every template.
- **New roles:** add a `RoleKind` and a `RoleDef` in `RoleCatalog`.
- **New trait categories:** add a `TraitCategory` and a `TraitDef` in `Model/TraitCatalog.cs` (values, prose, scene text, how it's assigned).
- **New case models:** add a `CaseModel`, then handle it in `PlanCrime` (Night), `PlanSecrets`, `BuildStoryClues` and `BuildTruth` (Story).
- **New lockdowns, accidents, secrets:** `Generation/StoryBank.cs`.
- **New portraits:** drop a JPEG in `Assets/Portraits` and list it in `Model/PortraitCatalog.cs` with its sex, age, hair, spectacles and the roles it suits.
- **New rooms:** `ManorLayout` holds the floor plan in 640 × 400 plan units per floor.
