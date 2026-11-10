# BLACKBOX design notes

How the game plays, what each chapter teaches, and how the code is put together.
The block rules are listed near the end, under a spoiler heading.

## The loop

Every circuit has **inputs** on the left, **outputs** on the right, and **blocks** in between, joined by wires. Each block hides a rule. The player never reads the rule; they work it out.

1. **Read the goal.** The goal is written in one or two sentences above the board, with a count of the targets met. Every target also sits beside the thing it belongs to, with a tick or a cross, so the player always knows what finished looks like.
2. **Change things.** Clicking a block steps it to its next state, clicking one of its dots jumps to that state, and right-clicking or scrolling goes the other way. Inputs with arrows can be changed the same way. Fixed inputs are drawn dashed, and a locked block has a padlock and a dashed outline: neither can be changed.
3. **Probe.** Wires hide their signals. Clicking a wire puts a probe on it, which reads the signal there and keeps reading it live as things change. Each circuit allows a set number of probes, from none to two. Clicking a probe takes it off.
4. **Listen.** Every signal has a sound as well as a look (see below). Clicking any value on the board plays it. **HEAR GOAL** (G) plays every target in the order the goal names them, lighting each one as it sounds, and **HEAR NOW** (N) plays what the outputs give now, so the two can be compared by ear. While playing, each change sounds out the input that was set and every output it reached.
5. **Take notes.** The pen (D) draws anywhere on the circuit in one of three inks, and the eraser (E) or a right-drag rubs notes out. Notes are kept with the circuit, so a player can label blocks and write down what they've learned.
6. **Solve.** The moment every target is met, the whole circuit lights up and the next circuit is offered.

Every change sends a pulse of light along the wires downstream of what changed, so the player can see what it reaches, though not what it carries.

### Kinds of target

| Target on | Shown as | Why |
|---|---|---|
| An output | A card with the output's value now, `=` or `≠`, and the target value | The usual goal. |
| A block | A **SENDS** chip above the block with the wanted value and a tick or cross. The block's actual value isn't shown. | Pins down an intermediate signal, so there is only one fair route through the circuit. |
| An input | An **END AT** chip below the input | **Return** circuits: an adjustable input starts on its target. The player has to change it to learn what the blocks do, then put it back. |

### Signal media

Each chapter uses a different kind of signal, and its blocks work on that kind only.

| Chapter | Medium | Values | Drawn as |
|---|---|---|---|
| I First Light | binary | 0 and 1 | Digits |
| II Tally | decimal | 0 to 9, wrapping round | Digits |
| III Forms | shape | circle, triangle, square or hexagon, hollow or solid | The shape itself, outlined or filled |
| IV Resonance | wave | sine, square or saw, at a low, mid or high pitch | The waveform, with more cycles for higher pitches |

### Signal sounds

Each medium is heard in its own way, so a player can follow a circuit by ear as well as by eye. The sounds are synthesised in `Components/SignalSound.cs`.

| Medium | Sounds like |
|---|---|
| binary | A 1 is a clear beep. A 0 is only a soft, dull tap, so the player knows it played. |
| decimal | One held tone that lasts as many beats as the digit, gently re-struck on each beat so it can be counted. 0 is the tap. |
| shape | One click per corner: a circle is a single smooth note, a triangle three clicks, a square four and a hexagon six. Solid shapes are low and warm, hollow ones high and thin. |
| wave | The wave itself: a sine, square or saw at A3, E4 or A4. |

Signal sounds can be turned off for changes in Settings (**SIGNAL SOUNDS**); clicking a value or the HEAR buttons always plays. Each chapter's title card plays a few of its signals, so the player learns the sounds before the first circuit.

## Progression

* Four chapters, one per medium, with six circuits each.
* Inside a chapter, the first circuit is open. Solving circuits opens the next ones, and there is always **one spare** open, so a player who is stuck can try the next circuit instead.
* A chapter opens once 4 circuits of the chapter before are solved.
* The first time a player enters a chapter, its title card plays, with a line about its signals.
* Every chapter follows the same arc, so the media are the thing that changes:
  1. The new medium and one kind of block.
  2. A second kind, a locked block, and an input the player can change.
  3. A probe and a two-input kind.
  4. The fourth kind, often locked, with one block feeding two others.
  5. A **return** circuit: the inputs start on their targets and there are no probes.
  6. The **tangle**: eight blocks, some locked, two probes, and exactly one solution.

The tables give the number of combinations of input values and block states a player could try, and how many of them solve the circuit.

### Chapter I: First Light

*Every block keeps a rule. Find it.* Signals are bits.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | First Light | Click a block to change what it does | 2 | 1 |
| 02 | Switch | Adjustable inputs and locked blocks | 4 | 1 |
| 03 | Under the Lid | Probes, a target on a block, and B2 | 32 | 8 |
| 04 | Fork | One block feeding two, and B4 | 32 | 1 |
| 05 | Return | Inputs that start where they must end | 512 | 1 |
| 06 | Tangle | Eight blocks, three locked | 256 | 1 |

### Chapter II: Tally

*Count what changes.* Signals are digits that wrap round.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | Count | Digits wrap round, and D1 | 9 | 1 |
| 02 | Reflection | D3, locked | 30 | 1 |
| 03 | Balance | D2, and a target on a block | 60 | 2 |
| 04 | Larger | D4 | 600 | 1 |
| 05 | Retrace | A return circuit | 48,000 | 1 |
| 06 | Ledger | The tangle | 108,000 | 1 |

### Chapter III: Forms

*Every shape is a message.* Signals are shapes.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | Fill | Shapes, and S1 | 4 | 1 |
| 02 | Turn | S2, locked | 16 | 1 |
| 03 | Graft | S3 | 16 | 2 |
| 04 | Corners | S4, locked | 256 | 1 |
| 05 | Homecoming | A return circuit | 1,024 | 1 |
| 06 | Lattice | The tangle | 16,384 | 1 |

### Chapter IV: Resonance

*Listen closely.* Signals are sounds.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | Hum | Waves you can hear, and W1 | 4 | 1 |
| 02 | Timbre | W2, locked | 27 | 3 |
| 03 | Splice | W3 | 18 | 3 |
| 04 | Interval | W4, locked | 324 | 1 |
| 05 | Echo | A return circuit | 1,944 | 1 |
| 06 | Chorus | The tangle | 15,552 | 1 |

Every circuit is checked by brute force in the tests and from **BLACKBOX → Validate Puzzles**. Each one must have at least one solution and must not be solved at the start.

## Look and sound

The look is called **Void**. The background is pure black, and nothing glows except the signal.

* Wires are dark hairlines, because their values are hidden. A change sends a pulse of the signal gradient along them, cyan on the left of the screen fading to violet on the right.
* Probes, inputs and outputs are the only places values show, and they glow.
* A violet `≠` or cross marks a target that isn't met. Nothing in the game is red.
* When a circuit is solved, every wire lights, light drifts along them, and the circuit's title fills with the gradient.
* Type is Sora at light weights, with widely tracked uppercase labels.
* Everything is drawn as procedural meshes with anti-aliased edges and vertex-alpha glow, so it is crisp at any resolution and needs no sprites or post-processing.
* Reduce Motion (in Settings) stops pulses, bumps and drifting light.

All sound is synthesised when the game starts: soft sine blips for clicks and changes, a chord when a circuit is solved, filtered noise for menus, a slow, quiet drone, and the signal sounds above.

## Architecture

```
Scripts/
  Core/         JSON, the save format. No Unity dependencies.
  Components/   The four signal media and the block kinds.
  Circuit/      Compiles a circuit into an evaluation order and evaluates it.
  Puzzles/      Puzzle and chapter definitions, the loader, auto layout, the solver,
                goal sentences, progress rules and PuzzleSession (one attempt at one circuit).
  Game/         BlackboxApp (startup, screens, saving), input, audio, tweens.
  UI/Void/      The visual kit: shapes, wires, waves, ink, glyphs, text, buttons.
  UI/Board/     The circuit on screen: nodes, targets, wires, probes and the pen.
  UI/Screens/   Title, chapter map, circuit, and overlays (pause, settings, help...).
  Editor/       The BLACKBOX menu.
Tests/EditMode/ NUnit tests for everything in Core, Components, Circuit and Puzzles.
```

* **Core, Components, Circuit and Puzzles** compile into one assembly, `Blackbox.Core`, which doesn't reference the engine. The whole play loop (changing inputs and blocks, probing, solving, unlocking) can be tested without Unity, and the tests include a full playthrough of every chapter.
* **The interface is built in code.** `BlackboxApp` creates the camera, the event system and one canvas with a fixed 1920×1080 frame. The canvas scales to fit any screen. Screens are laid out in frame coordinates (x from the left, y down from the top).
* **No scene setup is needed.** If the open scene has no `BlackboxApp`, one is created when it loads, so pressing Play in any scene starts the game. **BLACKBOX → Create Main Scene** makes a proper scene for builds.
* **Screens** live on a stack. Opening a screen replaces the stack with a fade. Overlays (pause, settings, confirm, help, a chapter's title card) sit on top, behind a dark veil. Only the top screen gets keys.
* **Input** works with either backend: the Input System package or the old Input Manager.
* **Saving** writes `blackbox-save.json` to `Application.persistentDataPath`. It goes through a temporary file, so a crash can't leave half a save. It stores solved circuits and best move counts, where the player left each circuit (inputs, block states and probes), their pen notes, which chapter cards have been seen, and preferences. Saves from before the redesign keep only their preferences.

### Evaluation

Values are small whole numbers, one range per medium: 2 for bits, 10 for digits, 8 shapes (form × 2 + fill) and 9 waves (form × 3 + pitch). A circuit is compiled once into an order where every node comes after the nodes that feed it, and the whole circuit is evaluated again after every change. Circuits have no memory and no loops; a loop is rejected when the circuit loads.

### Adding a chapter

1. Write `chapter-5-<name>.json` next to the others (see [PuzzleFormat.md](PuzzleFormat.md)).
2. Add its file name to `chapters.json`.
3. Give it `"unlock": { "after": "<previous chapter id>", "solved": N }`.
4. Run **BLACKBOX → Validate Puzzles**, then the EditMode tests.

New kinds of block go in `Components/BlockKinds.cs`, with tests in `ComponentTests.cs`. Each kind belongs to one medium. A new medium needs a value range in `Media.cs`, a way to draw it in `UI/Board/ValueView.cs`, and words for goal sentences in `GoalText.cs`.

## Spoilers: the rules

The game shows only a block's kind code, its ports and its state dots. Port 0 is the top input.

| Kind | Inputs | States, in order |
|---|---|---|
| B1 | 1 | invert, pass |
| B2 | 2 | and, or |
| B3 | 1 | always 1, invert, always 0, pass |
| B4 | 2 | xor, and, top and not bottom, or |
| D1 | 1 | add 1, add 2, add 3 (wrapping) |
| D2 | 2 | sum, difference (top − bottom), wrapping |
| D3 | 1 | double (wrapping), halve rounding down, mirror (9 − x) |
| D4 | 2 | the larger, the smaller |
| S1 | 1 | flip fill, pass |
| S2 | 1 | next form, previous form (circle, triangle, square, hexagon, round again) |
| S3 | 2 | form of top with fill of bottom, form of bottom with fill of top |
| S4 | 2 | the one with more corners, the one with fewer (top on a tie) |
| W1 | 1 | raise pitch, lower pitch (wrapping) |
| W2 | 1 | make square, make sine, make saw (pitch kept) |
| W3 | 2 | form of top with pitch of bottom, form of bottom with pitch of top |
| W4 | 2 | the higher, the lower (top on a tie) |
