# BLACKBOX design notes

How the game plays, what each chapter teaches, and how the code is put together.
The block rules are listed near the end, under a spoiler heading.

## The loop

Every circuit has **inputs** on the left, **outputs** on the right, and **blocks** in between, joined by wires. Each block hides a rule. The player never reads the rule; they work it out.

1. **Read the goal.** The goal is written in one or two sentences above the board, with a count of the targets met. Every target also sits beside the thing it belongs to, so the player always knows what finished looks like.
2. **Change things.** Clicking a block steps it to its next state, clicking one of its dots jumps to that state, and right-clicking or scrolling goes the other way. Inputs with arrows can be changed the same way. Fixed inputs are drawn dashed, and a locked block has a padlock and a dashed outline: neither can be changed.
3. **Probe.** Wires hide their signals. Clicking a wire puts a probe on it, which reads the signal there on every run. Each circuit allows a set number of probes, from none to two. Clicking a probe takes it off.
4. **Run.** Nothing that comes out shows until the player presses **RUN** (Space). For a few seconds the signal flows from the inputs, block by block, and each probe reads its wire as the light passes. Then the outputs show what arrived, top to bottom, each playing its sound, and every target gets its tick or cross. Any change stops a run and hides its results again, so each run shows one setting of the circuit.
5. **Listen.** Every signal has a sound as well as a look (see below). A run plays each output, setting an input plays its new value, and clicking any value on the board plays it again.
6. **Take notes.** The pen (D) draws anywhere on the circuit in one of three inks. The text tool (T) types instead: click anywhere and type, Enter finishes the note, Shift+Enter starts a new line, and long lines wrap. Clicking a typed note edits it and dragging it moves it. The eraser (E) or a right-drag rubs out strokes and typed notes alike, and undo takes away the newest. Notes are kept with the circuit, so a player can label blocks and write down what they've learned.
7. **Solve.** When a run meets every target, the whole circuit lights up and the next circuit is offered.

Every change sends a quick pulse of light along the wires downstream of what changed, so the player can see what it reaches, though not what it carries.

### Kinds of target

| Target on | Shown as | Why |
|---|---|---|
| An output | A card with what arrived on the latest run (a `?` before one), `=` or `≠`, and the target value | The usual goal. |
| A block | A **SENDS** chip above the block with the wanted value, and a tick or cross after a run. The block's actual value isn't shown. | Pins down an intermediate signal, so there is only one fair route through the circuit. |
| An input | An **END AT** chip below the input, ticked after a run | **Return** circuits: an adjustable input starts on its target. The player has to change it to learn what the blocks do, then put it back. |

### Signal media

Each chapter uses a different kind of signal, and its blocks work on that kind only.

| Chapter | Medium | Values | Drawn as |
|---|---|---|---|
| I First Light | binary | 0 and 1 | Digits |
| II Tally | decimal | 0 to 9, wrapping round | Digits |
| III Forms | shape | circle, triangle, square or hexagon, hollow or solid | The shape itself, outlined or filled |
| IV Resonance | wave (analog) | sine, square or saw, at frequency F1 to F7 and amplitude A1 to A7, starting 0, ¼, ½ or ¾ of a cycle late | The waveform, with more cycles for a higher frequency and taller for a higher amplitude, its settings written under it ("F3 A5 +1/4") |
| V Grain | sampled wave | an analog wave read 3, 6 or 12 times a cycle, each reading rounded to a whole level and held, at F1 to F7 and A1 to A7 | The waveform as a staircase, settings under it ("F3 A5 S6") |

### Signals over time

Every signal is also a **trace**: its level at each of the 288 steps of one loop of a run (`Components/Trace.cs`). Bits, digits and shapes are flat traces. Waves rise and fall.

* **Frequency** F1 to F7 fits 1, 2, 3, 4, 6, 8 or 12 cycles into a loop. Those are octaves and fifths of each other, so the pitches are in tune, and every cycle, half and quarter cycle lands exactly on a step.
* **Amplitude** A1 to A7 is the peak, 1 to 7 levels from the middle.
* **Phase** moves an analog wave a quarter cycle at a time. A sine half a cycle late is the same sine upside down, so the two cancel when added; a sine and a cosine (a quarter apart) don't.
* A **sampled** wave reads the analog form at the middle of each of its 3, 6 or 12 steps a cycle and rounds the reading to a whole level. Two sampled waves that trace the same staircase (a square read 6 or 12 times, say) are the same signal, so either meets a target that names the other.

The trace also defines how one kind of signal reads as another, for the wiring puzzles to come. These rules are provisional:

| Read as | From a bit | From a digit | From a shape | From a wave |
|---|---|---|---|---|
| bit | itself | 1 unless 0 | 1 when solid | 1 while above the middle |
| digit | 0 or 1 | itself | its corners | its height, 0 (bottom) to 9 (top) |
| shape | a circle, solid when 1 | a circle, solid unless 0 | itself | a circle, solid while above the middle |
| analog wave | top when 1, bottom when 0 | a flat level, 0 at the bottom and 9 at the top | top when solid, bottom when hollow | itself |
| sampled wave | as for analog, rounded to whole levels at every step | | | |

### Signal sounds

Each medium is heard in its own way, so a player can follow a circuit by ear as well as by eye. The sounds are synthesised in `Components/SignalSound.cs`.

| Medium | Sounds like |
|---|---|
| binary | A 1 is a clear beep. A 0 is only a soft, dull tap, so the player knows it played. |
| decimal | One short tone whose pitch is the digit, climbing a major scale: 0 is C4 (do), 7 is C5, 9 is E5. Every digit lasts as long. |
| shape | One click per corner: a circle is a single smooth note, a triangle three clicks, a square four and a hexagon six. Solid shapes are low and warm, hollow ones high and thin. |
| wave | The wave itself. Its frequency is its pitch (F1 is D3, F3 is A4, F7 is A6) and its amplitude its loudness, 2.5 dB a step. Phase can't be heard. |
| sampled wave | The same pitches, but held in steps exactly as drawn, so it buzzes; the fewer the steps the harsher. Loudness follows the levels. |

Signal sounds can be turned off in Settings (**SIGNAL SOUNDS**): runs then show each output with a soft blip instead, and setting an input stays quiet. Clicking a value always plays it. Each chapter's title card plays a few of its signals, so the player learns the sounds before the first circuit.

## Progression

* Five chapters, one per medium, with six circuits each.
* Every chapter is open from the start, with its first circuit open, so a player can begin with any medium.
* Inside a chapter, solving circuits opens the next ones, and there is always **one spare** open, so a player who is stuck can try the next circuit instead.
* **Continue** on the title screen goes on from the last circuit played, or to the earliest chapter with a circuit still to solve.
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

*Listen closely.* Signals are analog waves. Adjustable inputs step through a short list of waves given in the file.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | Hum | Frequency and amplitude, and W1 | 4 | 1 |
| 02 | Timbre | W2, locked | 12 | 1 |
| 03 | Splice | W3 | 8 | 1 |
| 04 | Offset | Phase, and W4, locked | 48 | 1 |
| 05 | Echo | A return circuit | 144 | 1 |
| 06 | Chorus | The tangle | 2,592 | 1 |

### Chapter V: Grain

*Up close, every curve is a staircase.* Signals are sampled waves.

| # | Circuit | Teaches | Combinations | Solutions |
|---|---|---|---|---|
| 01 | Stairs | Steps a cycle, and Q1 | 4 | 1 |
| 02 | Level | Q2, locked | 6 | 1 |
| 03 | Graft | Q3 | 6 | 1 |
| 04 | Measure | Q4, locked | 24 | 1 |
| 05 | Replay | A return circuit | 96 | 1 |
| 06 | Mosaic | The tangle | 288 | 1 |

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

All sound is synthesised when the game starts: soft sine blips for clicks and changes, a rising sweep and a tick for each column as a run flows, a chord when a circuit is solved, filtered noise for menus, a slow, quiet drone, and the signal sounds above.

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
* **Saving** writes `blackbox-save.json` to `Application.persistentDataPath`. It goes through a temporary file, so a crash can't leave half a save. It stores solved circuits and best move counts, where the player left each circuit (inputs, block states and probes), their notes (pen strokes and typed text), which chapter cards have been seen, and preferences. Saves from before the redesign keep only their preferences.

### Evaluation

Values are small whole numbers, one range per medium: 2 for bits, 10 for digits, 8 shapes (form × 2 + fill), 588 analog waves (((form × 7 + frequency) × 7 + amplitude) × 4 + phase) and 441 sampled waves (((form × 7 + frequency) × 7 + amplitude) × 3 + steps). Targets on waves are met by any value that traces the same line (`Media.Same`). A circuit is compiled once into an order where every node comes after the nodes that feed it, and the whole circuit is evaluated again after every change. Circuits have no memory and no loops; a loop is rejected when the circuit loads.

### Adding a chapter

1. Write `chapter-6-<name>.json` next to the others (see [PuzzleFormat.md](PuzzleFormat.md)).
2. Add its file name to `chapters.json`.
3. To make it wait for an earlier chapter, give it `"unlock": { "after": "<chapter id>", "solved": N }`. Leave that out and it is open from the start, like the shipped chapters.
4. Run **BLACKBOX → Validate Puzzles**, then the EditMode tests.

New kinds of block go in `Components/BlockKinds.cs`, with tests in `ComponentTests.cs`. Each kind belongs to one medium. A new medium needs a value range in `Media.cs`, a way to draw it in `UI/Board/ValueView.cs`, and words for goal sentences in `GoalText.cs`.

## Spoilers: the rules

The game shows only a block's kind code, its ports and its state dots. Port 0 is the top input. For testing, **DEBUG: BLOCK RULES** in Settings (off by default) writes the current state's rule from this table under every block on the circuit screen.

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
| W1 | 1 | one frequency step faster, one slower (wrapping) |
| W2 | 1 | make square, make sine, make saw (frequency, amplitude and phase kept) |
| W3 | 2 | top's wave at bottom's amplitude, bottom's wave at top's amplitude |
| W4 | 1 | a quarter cycle later, half a cycle later (wrapping) |
| Q1 | 1 | more steps a cycle, fewer (3, 6, 12, wrapping) |
| Q2 | 1 | one level taller, one shorter (A1 to A7, wrapping) |
| Q3 | 2 | top's wave with bottom's steps, bottom's wave with top's steps |
| Q4 | 2 | the taller, the shorter (top on a tie) |
