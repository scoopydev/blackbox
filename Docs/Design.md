# BLACKBOX design notes

How the game plays, what each chapter teaches, and how the code is put together.
The block rules are listed near the end, under a spoiler heading.

## The loop

Every circuit gives the player **inputs** on the left, **blocks** in the middle and **outputs** on the right. The player **draws the wires** so every output does what its target shows. Only the outputs count. Each block hides a rule; the player never reads it, they work it out by wiring the block into something and running it.

1. **Read the goal.** The goal is one sentence above the board, with a count of the outputs met. Every output shows its target device beside the live one, with = or ≠ between them, so the player always knows what finished looks like.
2. **Wire.** Drag from a dot to a dot, or click one dot and then the other. Dots on the right of inputs and blocks send; dots on the left of blocks and outputs take one wire each. One source can feed many wires. Grabbing a port that already has a wire picks that wire up to move it. Clicking a wire, or right-clicking a dot, takes wires off. A wire that would make a loop is refused, with the reason. Undo and Clear work on wires.
3. **Run.** Nothing shows until **RUN** (Space). For a few seconds the light runs along the wires, each block shows what it sends, and the outputs work their devices top to bottom, each playing its sound. Any change hides the results again, so each run shows one wiring.
4. **Keep the rules.** Every block must be used: it must lead to an output, through other blocks or straight there. Every input of a block needs a wire, and a block with an empty input sends nothing. Inputs may go unused. The verdict after a run names the first broken rule, such as "block g has an input with no wire" or "block h isn't used".
5. **Listen.** Every signal has a sound as well as a look (see below). Clicking any signal or target plays it.
6. **Take notes.** The pen (D) draws anywhere on the circuit in one of three inks. The text tool (T) types instead: click anywhere and type, Enter finishes the note, Shift+Enter starts a new line, and long lines wrap. Clicking a typed note edits it and dragging it moves it. The eraser (E) or a right-drag rubs out strokes and typed notes alike. Notes are kept with the circuit.
7. **Solve.** When a run meets every output and keeps both rules, the whole circuit lights up and the next circuit is offered.

Blocks are fixed: the player never changes a block's setting. A **named** block shows its kind's code (such as "B3") and a dot for each of the kind's settings, with the one it is fixed in filled, so two blocks of one kind that behave differently never look the same. In Part Three blocks are **unmarked**: no code and no dots, only their ports.

### Devices

| Device | Does | Matches its target when |
|---|---|---|
| Display | Shows the signal itself, as a value, a strip of values or a graph | it is the same signal |
| Light | Lit on a 1; brighter for a bigger digit (9 is full); follows a wave above its middle and is dark below it; lit in a shape's colour, whatever its form or fill | it is as bright, in the same colour, at every moment |
| Speaker | Plays the signal | it is the same signal |
| Digit display | Shows a number: a digit, a bit, a shape's corners, a wave's height rounded | it shows the same numbers |
| Fan | Spins faster for more signal; a wave below its middle spins it backwards | it spins the same way at the same speed |
| Servo | Swings its arm from left (nothing, or a wave's bottom) to right (full, or a wave's top) | its arm is in the same place |

A device matches on what it does, not on the signal, so a digit display wanting 3 is met by the digit 3 or by a triangle.

### Signal media

| Chapter | Medium | Values | Drawn as |
|---|---|---|---|
| I First Light | binary | 0 and 1 | Digits |
| II Tally | decimal | 0 to 9, wrapping round | Digits |
| III Forms | shape | circle, triangle, square or hexagon; hollow or solid; cyan, pink, amber or green | The shape itself, outlined or filled, in its colour (cyan in the signal gradient) |
| IV Resonance | wave (analog) | sine, square or saw, at frequency F1 to F7 and amplitude A1 to A7, starting 0, ¼, ½ or ¾ of a cycle late | The waveform, with more cycles for a higher frequency and taller for a higher amplitude, its settings written under it ("F3 A5 +1/4") |
| V Grain | sampled wave | an analog wave read 3, 6 or 12 times a cycle, each reading rounded to a whole level and held, at F1 to F7 and A1 to A7 | The waveform as a staircase, settings under it ("F3 A5 S6") |

### Signals over time

Every signal is a **trace**: its level at each of the 288 steps of one loop of a run (`Components/Trace.cs`). An input can be a single value or a pattern, like `binary 0110`, held for an equal share of the loop each. Waves rise and fall.

* **Frequency** F1 to F7 fits 1, 2, 3, 4, 6, 8 or 12 cycles into a loop. Those are octaves and fifths of each other, so the pitches are in tune, and every cycle, half and quarter cycle lands exactly on a step.
* **Amplitude** A1 to A7 is the peak, 1 to 7 levels from the middle.
* **Phase** moves an analog wave a quarter cycle at a time. A sine half a cycle late is the same sine upside down, so the two cancel when added; a sine and a cosine (a quarter apart) don't.
* A **sampled** wave reads the analog form at the middle of each of its 3, 6 or 12 steps a cycle and rounds the reading to a whole level. Two sampled waves that trace the same staircase (a square read 6 or 12 times, say) are the same signal.

Bit, digit and shape blocks work step by step through the loop, and read any signal as their own kind:

| Read as | From a bit | From a digit | From a shape | From a wave |
|---|---|---|---|---|
| bit | itself | 1 unless 0 | 1 when solid | 1 while above the middle |
| digit | 0 or 1 | itself | its corners | its height, 0 (bottom) to 9 (top) |
| shape | a cyan circle, solid when 1 | a cyan circle, solid unless 0 | itself | a cyan circle, solid while above the middle |
| analog wave | top when 1, bottom when 0 | a flat level, 0 at the bottom and 9 at the top | top when solid, bottom when hollow | itself |
| sampled wave | as for analog, rounded to whole levels at every step | | | |

Wave and sampled-wave blocks change a wave's **settings** (frequency, form, height, phase, steps), so they only read a plain wave of their own kind: one straight from an input or from another such block. Given anything else, they send nothing, and the block says **CAN'T READ THAT SIGNAL**.

### Signal sounds

Each medium is heard in its own way, so a player can follow a circuit by ear as well as by eye. The sounds are synthesised in `Components/SignalSound.cs`.

| Medium | Sounds like |
|---|---|
| binary | A 1 is a clear beep. A 0 is only a soft, dull tap, so the player knows it played. |
| decimal | One short tone whose pitch is the digit, climbing a major scale: 0 is C4 (do), 7 is C5, 9 is E5. Every digit lasts as long. |
| shape | One click per corner: a circle is a single smooth note, a triangle three clicks, a square four and a hexagon six. Solid shapes are low and warm, hollow ones high and thin. Colour lifts the pitch: pink a third, amber a fifth, green a sixth above cyan. |
| wave | The wave itself. Its frequency is its pitch (F1 is D3, F3 is A4, F7 is A6) and its amplitude its loudness, 2.5 dB a step. Phase can't be heard. |
| sampled wave | The same pitches, but held in steps exactly as drawn, so it buzzes; the fewer the steps the harsher. Loudness follows the levels. |

A signal that changes through the loop plays each of its values in turn. Signal sounds can be turned off in Settings (**SIGNAL SOUNDS**): runs then show each output with a soft blip instead. Clicking a signal always plays it. Each chapter's title card plays a few of its signals, so the player learns the sounds before the first circuit.

## Progression

* Three parts. **Learn** has five chapters, one per medium, with six circuits each, and teaches each signal and its four blocks. **Combine** (Patch, Blend) teaches how kinds of signal read each other and the eight mixing blocks. **Build** (Unmarked, Blackbox) is the main game. Each later part opens when every circuit of the part before it is solved; the chapter map has a tab for each part, with a padlock and a count while it's locked.
* **DEBUG: OPEN EVERYTHING** in Settings opens every part, chapter and circuit, for testing.
* Within an open part, every chapter is open, with its first circuit open, so a player can begin with any medium.
* Inside a chapter, solving circuits opens the next ones, and there is always **one spare** open, so a player who is stuck can try the next circuit instead.
* **Continue** on the title screen goes on from the last circuit played, or to the earliest chapter with a circuit still to solve.
* The first time a player enters a chapter, its title card plays, with a line about its signals.
* Every Learn chapter follows the same arc, so the media are the thing that changes:
  1. **Pre-wired.** One block, every wire drawn, but one runs from the wrong input. The player moves it.
  2. **Half wired.** A two-input block with one input empty. The player finishes it.
  3. From here the player draws every wire: two blocks in a chain.
  4. One source feeding two blocks, and a second output.
  5. A two-input block where order matters, or three blocks.
  6. The **tangle**: four blocks, three inputs and two or three outputs.
* Saves from before the wiring game (version 2 and older) keep only their preferences.

The tables give the number of ways to wire the blocks' inputs, and how many complete wirings solve the circuit.

### Chapter I: First Light

*Every block keeps a rule. Wire it to find out.* Bits, B1 to B4; lights and a display.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | First Light | Pre-wired: move one wire, and B1 | 2 | 1 |
| 02 | Switch | Half wired: an empty input sends nothing, and B2 | 4 | 2 |
| 03 | Under the Lid | Wiring it all, a chain, and B3 | 27 | 2 |
| 04 | Fork | One input feeding two blocks, and B4 | 27 | 2 |
| 05 | Top and Bottom | Which input is on top | 256 | 2 |
| 06 | Tangle | Four blocks, two lights | 279,936 | 224 |

### Chapter II: Tally

*Count what changes.* Digits, D1 to D4; digit displays, a fan and a servo.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Count | Pre-wired, and D1 | 2 | 1 |
| 02 | Balance | Half wired, and D2 | 4 | 2 |
| 03 | Reflection | D3, order matters, a fan | 9 | 1 |
| 04 | Larger | D4, a servo | 3,125 | 26 |
| 05 | Retrace | A pattern of digits | 9 | 1 |
| 06 | Ledger | Four blocks, two outputs | 279,936 | 12 |

### Chapter III: Forms

*Every shape is a message.* Shapes, S1 to S4; displays, a light and digit displays.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Fill | Pre-wired, and S1 | 2 | 1 |
| 02 | Graft | Half wired, and S3 | 4 | 1 |
| 03 | Corners | S4, and a light showing a shape's colour | 4 | 2 |
| 04 | Turn | S2, and a digit display counting corners | 9 | 1 |
| 05 | Homecoming | Colour stays with the form | 625 | 63 |
| 06 | Lattice | Four blocks, three outputs | 46,656 | 76 |

### Chapter IV: Resonance

*Listen closely.* Analog waves, W1 to W4; speakers, a display and servos.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Hum | Pre-wired, and W1 | 2 | 1 |
| 02 | Splice | Half wired, and W3 | 4 | 1 |
| 03 | Timbre | W2 and W4 | 9 | 2 |
| 04 | Offset | A servo following a wave | 27 | 2 |
| 05 | Echo | Two speakers | 64 | 2 |
| 06 | Chorus | Four blocks, a speaker and a servo | 7,776 | 4 |

### Chapter V: Grain

*Up close, every curve is a staircase.* Sampled waves, Q1 to Q4.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Stairs | Pre-wired, and Q1 | 2 | 1 |
| 02 | Graft | Half wired, and Q3 | 4 | 1 |
| 03 | Level | Q2, a servo in steps | 9 | 2 |
| 04 | Measure | Q4, by ear | 64 | 5 |
| 05 | Replay | One input feeding two chains | 256 | 3 |
| 06 | Mosaic | Four blocks, a servo and a speaker | 46,656 | 113 |

Every circuit is checked by trying every way to wire it, in the tests and from **BLACKBOX → Validate Puzzles**. Each one's solution must work, it must not be solved as it starts, and no input wired straight to an output may already meet that output's target.

### Chapter VI: Patch

*Every block reads every signal.* Named blocks of one kind, fed signals of another.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Cross Talk | A bit block reads a shape; one input into both ports | 9 | 1 |
| 02 | Corner Count | A digit block reads a shape as its corners | 3 | 1 |
| 03 | Branch | One source feeding two wires; a digit display counts corners | 4 | 2 |
| 04 | Bit Shapes | A shape block reads a bit as a circle | 2 | 1 |
| 05 | Moving Parts | A fan driven by a shape, a servo by bits | 9 | 1 |

### Chapter VII: Blend

*Signals of different kinds, mixed together.* The M kinds, one or two at a time.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Pulse | A wave into AND blinks a bit | 9 | 3 |
| 02 | Silence | M1: a wave and its half-cycle twin cancel, on a speaker | 9 | 2 |
| 03 | Mirror | M2: a signal plus its flip is nothing | 8 | 2 |
| 04 | Late | M3: a quarter loop later, which is a half cycle of a twice-a-loop wave | 27 | 2 |
| 05 | Steps | M4 and M7, in the right order | 9 | 1 |
| 06 | Burst | M5 gates a wave with a pattern | 4 | 1 |
| 07 | Count | M6 counts rises, through a gate | 27 | 2 |
| 08 | Threshold | M8 against a digit makes a pulse; a lower threshold keeps it on longer | 9 | 1 |

### Chapter VIII: Unmarked

*The blocks have lost their names.* Two blocks each, of every kind met so far.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Nameless | Order matters: double then add, not add then double | 4 | 1 |
| 02 | Dim | Light brightness from a digit, and a shape's colour | 9 | 1 |
| 03 | Sweep | A servo following a wave, delayed after speeding up | 9 | 1 |
| 04 | Strobe | Two outputs from one chain | 64 | 6 |
| 05 | Countdown | A count, mirrored | 9 | 1 |
| 06 | Colours | Shapes on a light and a digit display | 27 | 2 |

### Chapter IX: Blackbox

*Everything at once.* Three to five unnamed blocks, two or three outputs.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Metronome | Speed up twice, threshold, count the blinks | 7,776 | 6 |
| 02 | Mixing Desk | Add two waves, gate the sum, flip the gate onto a fan | 3,125 | 2 |
| 03 | Stepper | A servo in steps, beside a digit chain | 125 | 2 |
| 04 | Signal Box | Shapes, bits and digits at once, three outputs | 279,936 | 16 |
| 05 | Pulse Width | A threshold from a difference, three readers of one pulse | 46,656 | 1 |
| 06 | Black Box | Three separate machines in one circuit, five blocks | 2,097,152 | 2 |

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
  Components/   The signal media, signals over time (Trace), signal patterns (Signals),
                output devices (Devices) and the block kinds.
  Circuit/      Compiles a circuit into an evaluation order and evaluates it.
  Puzzles/      Puzzle, chapter and part definitions, the loader, auto layout, the solvers,
                goal sentences, progress rules and WiringSession (one attempt at a circuit).
                PuzzleSession runs the older pre-wired kind of circuit, now only the
                title screen's moving picture.
  Game/         BlackboxApp (startup, screens, saving), input, audio, tweens.
  UI/Void/      The visual kit: shapes, wires, waves, ink, glyphs, text, buttons.
  UI/Board/     The wiring board (WiringBoard) with its device and signal views, the pen and
                typed notes; BoardView draws the title screen's pre-wired picture.
  UI/Screens/   Title, chapter map, circuit, and overlays (pause, settings, help...).
  Editor/       The BLACKBOX menu.
Tests/EditMode/ NUnit tests for everything in Core, Components, Circuit and Puzzles.
```

* **Core, Components, Circuit and Puzzles** compile into one assembly, `Blackbox.Core`, which doesn't reference the engine. The whole play loop (wiring, running, the rules, solving, unlocking) can be tested without Unity, and the tests include a full playthrough of every chapter.
* **The interface is built in code.** `BlackboxApp` creates the camera, the event system and one canvas with a fixed 1920×1080 frame. The canvas scales to fit any screen. Screens are laid out in frame coordinates (x from the left, y down from the top).
* **No scene setup is needed.** If the open scene has no `BlackboxApp`, one is created when it loads, so pressing Play in any scene starts the game. **BLACKBOX → Create Main Scene** makes a proper scene for builds.
* **Screens** live on a stack. Opening a screen replaces the stack with a fade. Overlays (pause, settings, confirm, help, a chapter's title card) sit on top, behind a dark veil. Only the top screen gets keys.
* **Input** works with either backend: the Input System package or the old Input Manager.
* **Saving** writes `blackbox-save.json` to `Application.persistentDataPath`. It goes through a temporary file, so a crash can't leave half a save. It stores solved circuits and best move counts, the wires the player left on each circuit, their notes (pen strokes and typed text), which chapter cards have been seen, and preferences. Saves from before the wiring game keep only their preferences.

### Evaluation

Values are small whole numbers, one range per medium: 2 for bits, 10 for digits, 32 shapes ((colour × 4 + form) × 2 + fill), 588 analog waves (((form × 7 + frequency) × 7 + amplitude) × 4 + phase) and 441 sampled waves (((form × 7 + frequency) × 7 + amplitude) × 3 + steps). Inputs are traces: 288 steps of one loop, each a level of the signal's medium. A wave made straight from a value remembers it (`Trace.Value`), which is what wave blocks read. `WiringSession` evaluates the wires from the inputs on, and `WiringSolver` tries every way to wire a circuit's blocks (skipping loops), then every source each output could take, to count the solutions. Circuits have no memory and no loops; a wire that would make one is refused.

### Adding a chapter

1. Write `chapter-10-<name>.json` next to the others (see [PuzzleFormat.md](PuzzleFormat.md)).
2. Add its file name to a part in `chapters.json`.
3. To make it wait for an earlier chapter, give it `"unlock": { "after": "<chapter id>", "solved": N }`. Leave that out and it is open from the start, like the shipped chapters.
4. Run **BLACKBOX → Validate Puzzles**, then the EditMode tests.

New kinds of block go in `Components/BlockKinds.cs`, with tests in `ComponentTests.cs` and `WiringTests.cs`. Each kind belongs to one medium. A new medium needs a value range in `Media.cs`, a way to draw it in `UI/Board/ValueView.cs`, and words for goal sentences in `GoalText.cs`.

## Spoilers: the rules

A named block shows only its kind code, its ports and its state dots; an unmarked one only its ports. Port 0 is the top input. For testing, **DEBUG: BLOCK RULES** in Settings (off by default) writes each block's rule from this table under it.

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
| M1 | 2 | add them as waves, clipped at the top and bottom |
| M2 | 1 | upside down: a bit inverts, a digit becomes 9 − d, a shape swaps its fill, a wave flips |
| M3 | 1 | a quarter of the loop later |
| M4 | 1 | read as a wave 12 times a loop, each reading held and rounded (a sampled wave) |
| M5 | 2 | the top signal while the bottom reads 1, otherwise nothing (0, or a hollow circle) |
| M6 | 1 | how many times it has switched on so far this loop, as a digit wrapping after 9 |
| M7 | 1 | twice as fast, so it repeats twice a loop |
| M8 | 2 | 1 while the top is higher than the bottom, both read as waves; otherwise 0 |

The M kinds have one state each. Bit, digit and shape kinds in a wired circuit work step by step, reading each input as their own kind.
