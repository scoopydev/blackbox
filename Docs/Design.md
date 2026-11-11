# BLACKBOX design notes

How the game plays, what each chapter teaches, and how the code is put together.
The block rules are listed near the end, under a spoiler heading.

## The loop

Every circuit gives the player **inputs** on the left, **blocks** in the middle and **outputs** on the right. The player **draws the wires** so every output does what its target shows. Only the outputs count. Each block hides a rule; the player never reads it, they work it out by wiring the block into something and running it.

1. **Read the goal.** The goal is one short sentence above the board ("Make display X do what its target shows."), with the two rules in small letters under it and a count of the outputs met beside it. Every output shows its target beside the live one, with = or ≠ between them, so the player always knows what finished looks like.
2. **Wire.** Drag from a dot to a dot, or click one dot and then the other. Dots on the right of inputs and blocks send; dots on the left of blocks and outputs take one wire each. One source can feed many wires. Grabbing a port that already has a wire picks that wire up to move it. Clicking a wire, or right-clicking a dot, takes wires off. A wire that would make a loop is refused, with the reason. Undo and Clear work on wires. Dragging a block by its body moves it, and its wires follow, so the player can untangle a circuit; a block can't be dropped on another card. The circuit remembers where its blocks were left, and RESET in the menu puts them back.
3. **Run.** Nothing shows until **RUN** (Space). For a few seconds the light runs along the wires, each block shows what it sends, and the outputs show what reaches them, top to bottom, each playing its sound. Any change hides the results again, so each run shows one wiring.
4. **Keep the rules.** Every block must be used: it must lead to an output, through other blocks or straight there. Every input of a block needs a wire, and a block with an empty input sends nothing. Inputs may go unused. The verdict after a run names the first broken rule, such as "block g has an input with no wire" or "block h isn't used".
5. **Listen.** Every signal has a sound as well as a look (see below). Clicking any signal or target plays it.
6. **Take notes.** The pen (D) draws anywhere on the circuit in one of three inks. The text tool (T) types instead: click anywhere and type, Enter finishes the note, Shift+Enter starts a new line, and long lines wrap. Clicking a typed note edits it and dragging it moves it. The eraser (E) or a right-drag rubs out strokes and typed notes alike. Notes are kept with the circuit.
7. **Solve.** When a run meets every output and keeps both rules, the whole circuit lights up and the next circuit is offered. Each block's rule is then written under it, in a few words, and pointing at the block puts the whole rule in the status line.

The circuit screen keeps its text down: the pen colours show only while a pen or text tool is out, CLEAR NOTES only once there are notes, and an input's type is named on its card only in chapters that mix types.

Blocks have no settings. Each kind of block does **one simple thing**, the same in every circuit, so what a player learns about it carries over. The rules are chosen to be easy to put into words and to work out by experiment, with no exceptions: digits never wrap, there are no ties to break, and no block refuses a signal. Every block is labelled **K1, K2, K3...** in its circuit, counted down each column of the board in turn. The label only tells blocks apart, so the player can remember and talk about them; it never says what a block does, and K1 is a different block in every circuit. The player never sees a block's kind (such as "MIRROR"): a block shows only its label and its ports.

### Devices

**For now every output is a plain display** (see "Set aside: output devices" at the end). The table is what each device does when it is in play.

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
| II Tally | decimal | 0 to 9 | Digits |
| III Forms | shape | circle, triangle, square or hexagon; hollow or solid; cyan, pink, amber or green | The shape itself, outlined or filled, in its colour (cyan in the signal gradient) |
| IV Resonance | wave (analog) | sine, square or saw, at frequency F1 to F7 and amplitude A1 to A7, starting 0, ¼, ½ or ¾ of a cycle late | The waveform, with more cycles for a higher frequency and taller for a higher amplitude, its settings written under it ("F3 A5 +1/4") |

### Signals over time

Every signal is a **trace**: its level at each of the 288 steps of one loop of a run (`Components/Trace.cs`). An input can be a single value or a pattern, like `binary 0110`, held for an equal share of the loop each. Waves rise and fall.

* **Frequency** F1 to F7 fits 1, 2, 3, 4, 6, 8 or 12 cycles into a loop. Those are octaves and fifths of each other, so the pitches are in tune, and every cycle, half and quarter cycle lands exactly on a step.
* **Amplitude** A1 to A7 is the peak, 1 to 7 levels from the middle.
* **Phase** moves an analog wave a quarter cycle at a time. A sine half a cycle late is the same sine upside down, so the two cancel when added; a sine and a cosine (a quarter apart) don't.

Bit, digit and shape blocks work step by step through the loop, and read any signal as their own kind:

| Read as | From a bit | From a digit | From a shape | From a wave |
|---|---|---|---|---|
| bit | itself | 1 unless 0 | 1 when solid | 1 while above the middle |
| digit | 0 or 1 | itself | its corners | its height, 0 (bottom) to 9 (top) |
| shape | a cyan circle, solid when 1 | a cyan circle, solid unless 0 | itself | a cyan circle, solid while above the middle |
| analog wave | top when 1, bottom when 0 | a flat level, 0 at the bottom and 9 at the top | top when solid, bottom when hollow | itself |

Wave blocks read any signal as a wave (the last row) and send a wave, so they chain in any order and never refuse a signal.

### Signal sounds

Each medium is heard in its own way, so a player can follow a circuit by ear as well as by eye. The sounds are synthesised in `Components/SignalSound.cs`.

| Medium | Sounds like |
|---|---|
| binary | A 1 is a clear beep. A 0 is only a soft, dull tap, so the player knows it played. |
| decimal | One short tone whose pitch is the digit, climbing a major scale: 0 is C4 (do), 7 is C5, 9 is E5. Every digit lasts as long. |
| shape | One click per corner: a circle is a single smooth note, a triangle three clicks, a square four and a hexagon six. Solid shapes are low and warm, hollow ones high and thin. Colour lifts the pitch: pink a third, amber a fifth, green a sixth above cyan. |
| wave | The wave itself. Its frequency is its pitch (F1 is D3, F3 is A4, F7 is A6) and its amplitude its loudness, 2.5 dB a step. Phase can't be heard. |

A signal that changes through the loop plays each of its values in turn. Signal sounds can be turned off in Settings (**SIGNAL SOUNDS**): runs then show each output with a soft blip instead. Clicking a signal always plays it. Each chapter's title card plays a few of its signals, so the player learns the sounds before the first circuit.

## Progression

* Three parts. **Learn** has four chapters, one per medium, with six circuits each, and teaches each signal and its four blocks. **Combine** (Patch, Junction, Blend) teaches how kinds of signal read each other, how two kinds meet in one block, and the three mixing blocks. **Build** (Unmarked, Blackbox) is the main game. Each later part opens when every circuit of the part before it is solved; the chapter screen lists every part and its chapters down the left, with a padlock while a part is locked and a solved count for each chapter.
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

*Every block keeps a rule. Wire it to find out.* Bits: NOT, AND, OR, XOR.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | First Light | Pre-wired: move one wire, and NOT | 2 | 1 |
| 02 | Switch | Half wired: an empty input sends nothing, and AND | 4 | 2 |
| 03 | Under the Lid | Wiring it all, a chain, and OR | 27 | 2 |
| 04 | Fork | One input feeding two blocks, and XOR | 27 | 2 |
| 05 | Relay | Three blocks in a row | 3,125 | 24 |
| 06 | Tangle | Four blocks, two outputs | 279,936 | 224 |

AND and OR give the same answer either way round, and so does XOR, which is why bit circuits have more solutions.

### Chapter II: Tally

*Count what changes.* Digits: MIRROR, MAX, MIN, DIFF.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Reflection | Pre-wired, and MIRROR | 2 | 1 |
| 02 | Balance | Half wired, and DIFF | 4 | 2 |
| 03 | Peak | MAX, and order matters | 27 | 6 |
| 04 | Fork | MIN, and one input feeding two blocks | 1,024 | 44 |
| 05 | Retrace | A pattern of digits | 27 | 2 |
| 06 | Ledger | Four blocks, two outputs | 279,936 | 24 |

### Chapter III: Forms

*Every shape is a message.* Shapes: FILL, NEXT, TAKEFILL, TAKECOLOUR.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Fill | Pre-wired, and FILL | 2 | 1 |
| 02 | Graft | Half wired, and TAKEFILL | 4 | 1 |
| 03 | Repaint | TAKECOLOUR, on a pattern of shapes | 4 | 1 |
| 04 | Turn | NEXT, beside FILL on the same input | 9 | 1 |
| 05 | Homecoming | Colour stays with the form | 625 | 16 |
| 06 | Lattice | Four blocks, three outputs | 46,656 | 22 |

### Chapter IV: Resonance

*Listen closely.* Analog waves: FLIP, FASTER, LATER, MIX.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Hum | Pre-wired, and FASTER | 2 | 1 |
| 02 | Splice | Half wired, and MIX | 4 | 2 |
| 03 | Timbre | LATER, and order matters | 9 | 1 |
| 04 | Offset | FLIP, feeding an output and a block at once | 27 | 2 |
| 05 | Echo | Two outputs, by ear | 64 | 2 |
| 06 | Chorus | Four blocks, two outputs | 7,776 | 2 |

Every circuit is checked by trying every way to wire it, in the tests and from **BLACKBOX → Validate Puzzles**. Each one's solution must work, it must not be solved as it starts, and no input wired straight to an output may already meet that output's target.

### Chapter V: Patch

*Every block reads every signal.* Blocks of one kind, fed signals of another.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Cross Talk | AND reads a shape as a bit; one input into both ports | 9 | 1 |
| 02 | Corner Count | MIRROR reads a shape as its corners | 3 | 1 |
| 03 | Branch | One shape feeding a shape block and a digit block | 27 | 4 |
| 04 | Bit Shapes | NEXT reads a bit as a circle | 2 | 1 |
| 05 | Crossover | Each block fed the other kind of signal | 9 | 1 |

### Chapter VI: Junction

*Where kinds of signal meet.* Two kinds of signal into one block of a Part One kind, the step between Patch (one kind read as another) and Blend (the mixing blocks). No mixing blocks yet; a test checks that every solution feeds some block two kinds at once.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Corners | DIFF of a digit and a shape: the shape counts as its corners | 9 | 2 |
| 02 | Solid State | TAKEFILL: a shape's form with a bit's fill | 9 | 1 |
| 03 | Cut Off | XOR of a digit and a wave | 9 | 2 |
| 04 | Staircase | MAX of a wave read as a digit and a steady digit | 9 | 2 |
| 05 | Flicker | A wave giving a shape its fill, so it flickers | 9 | 1 |
| 06 | Junction | A shape, a digit and a wave, three blocks, three outputs | 15,625 | 8 |

### Chapter VII: Blend

*Signals of different kinds, mixed together.* Wave blocks meet other kinds, then the three mixing blocks: GATE, COUNT and COMPARE.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Pulse | A wave into AND blinks a bit | 9 | 3 |
| 02 | Silence | MIX: a wave and its half-cycle twin cancel | 9 | 2 |
| 03 | Square Up | FASTER reads a bit pattern as a square wave | 2 | 1 |
| 04 | Late | LATER: a quarter loop is half a cycle of a twice-a-loop wave | 27 | 2 |
| 05 | Burst | GATE lets a wave through a pattern | 4 | 1 |
| 06 | Count | COUNT counts switch-ons, through a gate | 27 | 2 |
| 07 | Threshold | COMPARE against a digit makes a pulse | 9 | 1 |
| 08 | Counter | COMPARE, then COUNT the pulses | 64 | 1 |

### Chapter VIII: Unmarked

*Every block you've met, all mixed up.* Two blocks each, any kind of signal and any block met so far.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Nameless | Order matters: MAX then MIRROR | 27 | 4 |
| 02 | Matchmaker | Which block goes with which input | 9 | 1 |
| 03 | Sweep | A wave sped up, then delayed | 9 | 1 |
| 04 | Strobe | Two outputs from one chain | 64 | 6 |
| 05 | Countdown | A count, mirrored | 9 | 1 |
| 06 | Colours | One block feeding an output and another block | 27 | 1 |

### Chapter IX: Blackbox

*Everything at once.* Three to five blocks, two or three outputs.

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Metronome | Speed up twice, compare, count the blinks | 7,776 | 2 |
| 02 | Mixing Desk | Mix two waves, gate the mix, flip the gate | 3,125 | 2 |
| 03 | Downhill | A flipped saw, sped up, beside a digit | 125 | 2 |
| 04 | Signal Box | Shapes, bits and digits at once, three outputs | 279,936 | 104 |
| 05 | Pulse Width | A threshold from a difference, three readers of one pulse | 46,656 | 2 |
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

Values are small whole numbers, one range per medium: 2 for bits, 10 for digits, 32 shapes ((colour × 4 + form) × 2 + fill), 588 analog waves (((form × 7 + frequency) × 7 + amplitude) × 4 + phase) and 441 sampled waves (((form × 7 + frequency) × 7 + amplitude) × 3 + steps), which are set aside for now. Inputs are traces: 288 steps of one loop, each a level of the signal's medium. `WiringSession` evaluates the wires from the inputs on, and `WiringSolver` tries every way to wire a circuit's blocks (skipping loops), then every source each output could take, to count the solutions. Circuits have no memory and no loops; a wire that would make one is refused.

### Adding a chapter

1. Write `chapter-10-<name>.json` next to the others (see [PuzzleFormat.md](PuzzleFormat.md)).
2. Add its file name to a part in `chapters.json`.
3. To make it wait for an earlier chapter, give it `"unlock": { "after": "<chapter id>", "solved": N }`. Leave that out and it is open from the start, like the shipped chapters.
4. Run **BLACKBOX → Validate Puzzles**, then the EditMode tests.

New kinds of block go in `Components/BlockKinds.cs`, with tests in `ComponentTests.cs` and `WiringTests.cs`. Each kind belongs to one medium and has one rule: no settings, and nothing a player would call an exception. A new medium needs a value range in `Media.cs`, a way to draw it in `UI/Board/ValueView.cs`, and words for goal sentences in `GoalText.cs`.

## Spoilers: the rules

A block shows only its label (K1, K2...) and its ports. The kind ids below are for puzzle files and designers; players never see them. Port 0 is the top input. Once a circuit is solved, each of its blocks has its rule written under it (`BlockKind.ShortRule`, the short form below; pointing at the block shows the whole rule), and keeps it whenever that circuit is opened again. For testing, **DEBUG: BLOCK RULES** in Settings (off by default) writes every block's rule under it, solved or not.

Each kind of signal has four blocks, and each set starts with one that flips the signal, so a player who has learned one set has a head start on the next.

| Kind | Signal | Inputs | Rule | Short form |
|---|---|---|---|---|
| NOT | bit | 1 | 1 becomes 0, 0 becomes 1 | flips 0 and 1 |
| AND | bit | 2 | 1 only while both are 1 | 1 when both are 1 |
| OR | bit | 2 | 1 while either is 1 | 1 when either is 1 |
| XOR | bit | 2 | 1 while they are different | 1 when they differ |
| MIRROR | digit | 1 | 9 minus the digit: 0 and 9 swap, 1 and 8, 2 and 7 | 9 minus the digit |
| MAX | digit | 2 | the larger digit | the larger digit |
| MIN | digit | 2 | the smaller digit | the smaller digit |
| DIFF | digit | 2 | how far apart they are: the larger minus the smaller | how far apart they are |
| FILL | shape | 1 | hollow becomes solid, solid becomes hollow | flips the fill |
| NEXT | shape | 1 | the next form: circle, triangle, square, hexagon, then circle again; fill and colour kept | the next form |
| TAKEFILL | shape | 2 | the top shape, with the bottom shape's fill | top shape, bottom's fill |
| TAKECOLOUR | shape | 2 | the top shape, in the bottom shape's colour | top shape, bottom's colour |
| FLIP | wave | 1 | the wave upside down | upside down |
| FASTER | wave | 1 | twice as fast, so it repeats twice as often | twice as fast |
| LATER | wave | 1 | a quarter of the loop later | a quarter loop later |
| MIX | wave | 2 | halfway between the two waves at every moment | halfway between them |
| GATE | any | 2 | the top signal while the bottom is on; otherwise off (0, a flat line or a hollow circle) | top, while bottom is on |
| COUNT | digit | 1 | how many times its input has switched on so far this loop, stopping at 9 | counts the switch-ons |
| COMPARE | bit | 2 | 1 while the top is higher than the bottom, both read as waves; otherwise 0 | 1 while top is higher |

Bit, digit and shape blocks work step by step, reading each input as their own kind. Wave blocks read each input as a wave. GATE sends whatever kind its top input is; COUNT reads its input as a bit.

The few edges a player can bump into, all kept because they follow from the rule rather than add to it: NEXT goes round from hexagon to circle, COUNT stops at 9 (a digit has nowhere further to go), and a mixed or flipped wave is still a wave, so FASTER and LATER work on it.

Until 11 Nov 2026 blocks had settings: the old kinds (B1 to B4, D1 to D4, S1 to S4, W1 to W4, M1 to M8) each had two to four rules, shown as dots on the block. They were replaced by the single-rule kinds above because a block the player can't change should do one thing they can name.

## Set aside: stepped waves

On 11 Nov 2026 stepped (sampled) waves were taken out of the game for now. A stepped wave is an analog wave read 3, 6 or 12 times a cycle, each reading rounded to a whole level and held, drawn as a staircase with its settings under it ("F3 A5 S6") and heard as a buzzy, held version of the analog pitch. They had Chapter V, *Grain* ("Up close, every curve is a staircase"), four blocks of their own with settings (Q1 to Q4), and a mixing block that made them (M4, now STEPS). The Q blocks were removed when every block got one fixed rule; STEPS is kept, set aside. Grain's circuits were:

| # | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Stairs | Pre-wired, and Q1 | 2 | 1 |
| 02 | Graft | Half wired, and Q3 | 4 | 1 |
| 03 | Level | Q2, a servo in steps | 9 | 2 |
| 04 | Measure | Q4, by ear | 64 | 5 |
| 05 | Replay | One input feeding two chains | 256 | 3 |
| 06 | Mosaic | Four blocks, a servo and a speaker | 46,656 | 113 |

The code that makes, draws, sounds and checks stepped waves is all still there, with its tests. `KindCatalog.Shelved` lists STEPS and `KindCatalog.MediumShelved` the medium, and the loader refuses any circuit that uses them. Grain's file is kept in `Assets/_Project/Puzzles/Shelved/`, outside `Resources`, so the game never loads it.

To bring them back: empty both checks in `KindCatalog`, design four single-rule stepped-wave blocks, rewrite Grain's circuits with them (its file still uses the old Q kinds, which the loader no longer knows), move it back into `BlackboxPuzzles`, list it in `chapters.json` after Resonance, renumber the chapters after it, and teach STEPS somewhere in Blend.

## Set aside: output devices

On 11 Nov 2026 every output became a plain display, for now. Lights, speakers, digit displays, fans and servos are listed in `Devices.Shelved`, and the loader refuses a circuit that uses one. Their drawing, matching and sound code is all kept, with its tests, and the Devices table above says what each did. Commit aea4196 has every circuit with its device.

A display needs the very same signal, where a device matched on what it did, so some circuits now have fewer solutions. Every hint and chapter line that talked about a device was reworded. Four circuits were renamed because their names were about devices: Junction 03 *Lights Out* became *Cut Off*, Unmarked 02 *Dim* became *Matchmaker*, and Blackbox 03 became *Downhill*. Patch 05 *Moving Parts* became *Crossover*: it fed a shape to a fan and bits to a servo, which shows nothing on displays, so now each block is fed the other kind of signal.

To bring them back: empty `Devices.Shelved`, give the circuits their devices again (from commit aea4196, or new ones), restore the device lines in the hints, chapter lines and the HOW IT WORKS overlay, and check every circuit with **BLACKBOX → Validate Puzzles**.

