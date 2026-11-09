# BLACKBOX design notes

How the game plays, what each chapter teaches, and how the code is put together.
The component rules are listed near the end, under a spoiler heading.

## The loop

Every circuit has **inputs** on the left, **outputs** on the right, and **boxes** in between.
Each box hides a rule. The player never reads the rule; they work it out.

1. **Set.** A box's dots are its settings. Clicking the box steps to the next setting, clicking a dot jumps to it, and scrolling turns it either way.
2. **Run.** Running tests the whole circuit against every case. The signal sweeps across the board and the readout along the bottom fills in.
3. **Probe.** With Probe on (P, or hold Shift), hovering a box shows what it did. A logic box lists every input combination and what it gave for the ones it has been seen to handle. A timed box shows its inputs and output over the whole run.
4. **Name.** Right-clicking a box, or pressing N, names the behaviour with one of 18 symbols or up to four typed letters. The name belongs to the kind and setting, so every box that behaves the same way shows it, in every circuit. Names are the player's notebook.
5. **Solve.** When every case matches, the whole circuit lights up.

Changing a setting after a run doesn't hide the result. The board, the readout and the match count dim to show that they are out of date, and Run pulses until the circuit is tested again.

### Two kinds of test

| Mode | Used in | A case is | Readout | Counter |
|---|---|---|---|---|
| Table | Chapter I | One moment: the inputs go in, the outputs come out. | A truth table: IN, WANT and GOT for every row. Click a row, or use Up and Down, to show it on the board. | Rows matched |
| Trace | Chapter II | A run of ticks. The inputs change over time and boxes can remember. | A timeline: one lane per input and output. The wanted wave is a dashed ghost, and misses are marked in violet. Click or drag to scrub. | Ticks matched |

After a trace run the cursor stops on the first tick that doesn't match.

## Progression

* Circuits are grouped into **chapters**. Each chapter has one new idea and gets harder as it goes.
* Inside a chapter, the first circuit is open. Solving circuits opens the next ones, and there is always **one spare** open, so a player who is stuck can try the next circuit instead.
* A chapter opens once enough circuits of the chapter before are solved. Chapter II needs 6 of the 8 in Chapter I.
* The first time a player enters a chapter, its title card plays.
* Logic boxes from Chapter I come back in Chapter II unchanged, so names carry over.

### Chapter I: First Light

*Every box keeps a rule. Find it.* Logic only. Every case is one moment.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | First Light | Click a box to change it, then Run | 2 | 1 |
| 02 | Two Ways | Every row of the table has to match, not just one | 4 | 1 |
| 03 | Meeting | Two-input boxes in a chain. Probe | 4 | 1 |
| 04 | Fork | One box feeding two outputs. Naming | 16 | 1 |
| 05 | Gatekeeper | Locked boxes: work out what they do, then work around them | 16 | 1 |
| 06 | Carry | A half adder | 16 | 1 |
| 07 | Quorum | The three-input box: exactly two inputs on | 36 | 1 |
| 08 | Sum | A full adder | 128 | 1 |

### Chapter II: Afterglow

*Some boxes remember.* Every case is a trace of 12 to 16 ticks.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | Echo | Delay, and reading the timeline | 3 | 1 |
| 02 | Hold | A delay feeding a logic box: "on for two ticks in a row" | 6 | 1 |
| 03 | Edges | Comparing now with the tick before: rising and falling edges | 9 | 1 |
| 04 | Switch | Toggles: a blinker and a push button | 8 | 1 |
| 05 | Doorbell | A latch: set by one input, cleared by another | 18 | 1 |
| 06 | Relay | Latch and delay together | 54 | 2 |
| 07 | Metronome | A feedback loop. The delay sets the tempo | 12 | 1 |
| 08 | Steady Hand | A debouncer | 96 | 2 |

Every circuit is checked by brute force in the tests and from **BLACKBOX → Validate Puzzles**. Each one must have at least one solution, and it must not be solved at the start.

## Look and sound

The look is called **Void**. The background is pure black, and nothing glows except the signal.

* A wire carrying a 1 is lit with the signal gradient, cyan on the left of the screen fading to violet on the right, with dots of light drifting along it. A 0 is a dark hairline.
* A run sweeps the light across the board column by column, and outputs flash as the signal arrives.
* A violet dot marks something that doesn't match. Nothing in the game is red.
* Results that are out of date are dimmed, not hidden.
* When a circuit is solved, every wire lights and the circuit's title fills with the gradient.
* Type is Sora at light weights, with widely tracked uppercase labels.
* Everything is drawn as procedural meshes with anti-aliased edges and vertex-alpha glow, so it is crisp at any resolution and needs no sprites or post-processing.
* Reduce Motion (in Settings) stops pulses, bumps and drifting light. Fast Runs skips the run animation.

All sound is synthesised when the game starts: soft sine blips for clicks and settings, a chord when a circuit is solved, filtered noise for menus, and a slow, quiet drone.

## Architecture

```
Scripts/
  Core/         JSON, the save format. No Unity dependencies.
  Components/   The box kinds and their logic.
  Circuit/      Compiles a circuit and simulates it tick by tick.
  Puzzles/      Puzzle and chapter definitions, the loader, auto layout, the solver,
                progress rules and PuzzleSession (one attempt at one circuit).
  Game/         BlackboxApp (startup, screens, saving), input, audio, tweens.
  UI/Void/      The visual kit: shapes, wires, glyphs, text, buttons, sliders.
  UI/Board/     The circuit on screen, the truth table, the timeline, probe and naming.
  UI/Screens/   Title, chapter map, circuit, and overlays (pause, settings, help...).
  Editor/       The BLACKBOX menu.
Tests/EditMode/ NUnit tests for everything in Core, Components, Circuit and Puzzles.
```

* **Core, Components, Circuit and Puzzles** compile into one assembly, `Blackbox.Core`, which doesn't reference the engine. The whole play loop (setting boxes, running, solving, unlocking) can be tested without Unity, and the tests include a full playthrough of both chapters.
* **The interface is built in code.** `BlackboxApp` creates the camera, the event system and one canvas with a fixed 1920×1080 frame. The canvas scales to fit any screen. Screens are laid out in frame coordinates (x from the left, y down from the top).
* **No scene setup is needed.** If the open scene has no `BlackboxApp`, one is created when it loads, so pressing Play in any scene starts the game. **BLACKBOX → Create Main Scene** makes a proper scene for builds.
* **Screens** live on a stack. Opening a screen replaces the stack with a fade. Overlays (pause, settings, confirm, help, a chapter's title card) sit on top, behind a dark veil. Only the top screen gets keys.
* **Input** works with either backend: the Input System package or the old Input Manager.
* **Saving** writes `blackbox-save.json` to `Application.persistentDataPath`. It goes through a temporary file, so a crash can't leave half a save. It stores solved circuits and best test counts, each circuit's last settings, the player's names, which chapter cards have been seen, and preferences.

### Simulation

A circuit is compiled once into an evaluation order. Each tick:

1. Inputs take their values for the tick.
2. **Registered** boxes (delays) output what they stored on earlier ticks. This is what lets loops work: a loop must pass through at least one delay, or the circuit is rejected when it loads.
3. Every other box is evaluated in topological order. Edge detectors, toggles and latches keep state between ticks but respond in the same tick.
4. Outputs read their drivers.

All logic resets at the start of each case.

### Adding a chapter

1. Write `chapter-3-<name>.json` next to the others (see [PuzzleFormat.md](PuzzleFormat.md)).
2. Add its file name to `chapters.json`.
3. Give it `"unlock": { "after": "<previous chapter id>", "solved": N }`.
4. Run **BLACKBOX → Validate Puzzles**, then the EditMode tests.

New kinds of box go in `ComponentKind.cs` (the catalog) and `BoxLogic.cs` (the behaviour), with tests in `ComponentTests.cs`.

## Spoilers: the rules

The game shows only a box's kind code (K1 to K9), its ports and its setting dots. Port 0 is the top input.

| Kind | Inputs | Settings, in order |
|---|---|---|
| K1 | 1 | invert, pass |
| K2 | 1 | always 1, invert, always 0, pass |
| K3 | 2 | or, and |
| K4 | 2 | xor, and, top and not bottom, or |
| K5 | 3 | at least 1 on, at least 2 on, all 3 on |
| K6 | 1 | delay 1, 2 or 3 ticks |
| K7 | 1 | on when the input changes, rises, or falls |
| K8 | 1 | flips every tick the input is on, flips when the input rises |
| K9 | 2 | latch: top sets and bottom clears, or the other way round. Both on holds |
