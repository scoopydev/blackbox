# BLACKBOX design notes

How the game plays, what each chapter teaches, and how the code is put together.
The block rules are listed near the end, under a spoiler heading.

## The loop

Every circuit gives the player **inputs** on the left, **blocks** in the middle and **outputs** on the right. The player **draws the wires** so every output does what its target shows. Only the outputs count. Each block hides a rule; the player never reads it, they work it out by wiring the block into something and running it.

1. **Read the goal.** Above the board, beside GOAL, the same line for every circuit: "MATCH ALL OUTPUTS. USE EVERYTHING WITH A SOLID OUTLINE." (`GoalText.WiringRules`), with a count of the outputs met at the right. Every output shows its target beside the live one, with = or ≠ between them, so the player always knows what finished looks like.
2. **Wire.** Drag from a dot to a dot, or click one dot and then the other. Dots on the right of inputs and blocks send; dots on the left of blocks and outputs take one wire each. One source can feed many wires. Grabbing a port that already has a wire picks that wire up to move it. Clicking a wire, or right-clicking a dot, takes wires off. A wire that would make a loop is refused, with the reason. Undo and Clear work on wires. Dragging a block by its body moves it, and its wires follow, so the player can untangle a circuit; a block can't be dropped on another card. The circuit remembers where its blocks were left, and RESET in the menu puts them back.
3. **Run.** Nothing shows until **RUN** (Space). For a few seconds the light runs along the wires, each block shows what it sends, and the outputs show what reaches them, top to bottom, each playing its sound. An output lights up as met whenever its signal does what its target does, whatever wiring made it: the designer's solution is only one way there. A blip of up to 3 of the loop's 288 steps (about 1%), too short to see or hear, doesn't count against it (`Trace.LooksLike`); such blips come where a wave is read as a bit at the very step it crosses the middle, so two wirings can disagree there and nowhere else. A change to the wires hides the outputs again, except an output that was solved on the last run with nothing feeding it rewired since: it stays solved, and the next run leaves it as it is and doesn't play it again, so only the outputs still in question sound (`WiringSession.OutputsUnchanged`). Each block keeps the signal it showed until the next run, unless its own input wires change or a block feeding it changes; rewiring where a block's signal goes leaves it showing (`WiringSession.BlocksUnchanged`). So every signal on show is still what that block sends as wired now.
4. **Keep the rules.** Everything with a solid outline must be used: each solid block must lead to an output, through other blocks or straight there, and so must each solid input. Every input of a block needs a wire, and a block with an empty input sends nothing. **Inputs are optional by default** (12 Nov 2026) and drawn with a dotted outline, so a circuit can offer more signals than it needs and the player is free to try any of them; a circuit marks an input `"required": true` to make it solid, and the answer always uses a solid input. **Blocks are solid by default**; one marked `"optional": true` is dotted, and the answer may use it or leave it out, so the outline never says which. An optional block may go unused, wired or not, and then shows no note; once it leads to an output it is a block like any other and needs every input wired. The solver counts an optional block left out, or wired but going nowhere, as one answer (`ContentTests.SolidOutlinesMarkWhatHasToBeUsed`). Outline, in Part Two, is the chapter that teaches both; FIRST LIGHT 1's hint and How to Play explain the dotted outline. The verdict after a run names every broken rule and every block or input that broke it, such as "Block B2 isn't used: its signal doesn't reach an output. Every block with a solid outline has to lead to an output." or "Input A isn't used, and its solid outline means it has to be." (`GoalText.BrokenRules`); the run marks each unused block NOT USED and turns an unused solid input's outline violet.
5. **Listen.** Every signal has a sound as well as a look (see below). Clicking any signal or target plays it. A **listen-only** block (`"listen": true`, Earshot) never shows what it sends: a speaker beside its label marks it, and after a run a speaker sits over it in place of its signal, which plays when clicked. Everything wired after it, blocks and outputs, however far on, shows a speaker too, with no mark of its own, until the circuit is solved; then every signal is drawn (`PuzzleDef.Heard(node, drivers)`, 12 Nov 2026, from fella). In a **dark** chapter (`"dark": true`, Lights Out) every signal is drawn that way, inputs, blocks, outputs and targets, and a run always plays each output, whatever the sound setting. The = or ≠ beside each output still shows. A wave sounds the same started later, turned upside down or raised (`SignalSound.SoundsAlike`), so in every circuit with a heard signal nothing the player could wire to an output sounds like its target without being it, and the answer is still the only one (`ContentTests.HeardCircuitsCanBeSolvedByEar`, which uses the solver's by-ear search).
6. **Take notes.** The pen (D) draws anywhere on the circuit in one of three inks. The text tool (T) types instead: click anywhere and type, Enter finishes the note, Shift+Enter starts a new line, and long lines wrap. Clicking a typed note edits it and dragging it moves it. The eraser (E) or a right-drag rubs out strokes and typed notes alike. Notes are kept with the circuit.
7. **Solve.** When a run meets every output and keeps both rules, the whole circuit lights up and the next circuit is offered. Each block's rule is then written under it, in a few words, and pointing at the block puts the whole rule in the status line.

The circuit screen keeps its text down: the pen colours show only while a pen or text tool is out, CLEAR NOTES only once there are notes, and an input's type is named on its card only in chapters that mix types.

Blocks have no settings. Each kind of block does **one simple thing**, the same in every circuit, so what a player learns about it carries over. The rules are chosen to be easy to put into words and to work out by experiment, with no exceptions: digits never wrap, there are no ties to break, and no block refuses a signal. Every block sends one type of signal, whatever reaches it: a **bit block** only ever sends 0 or 1, a **digit block** a digit from 0 to 9, and a **wave block** a wave, with its own height (amplitude) and speed (frequency). A block's label says which: **B** for a bit block, **D** for a digit block, **W** for a wave block, numbered per letter in its circuit (B1, B2, D1, W1...), counted down each column of the board in turn (`PuzzleLoader.LabelBlocks`). So the label tells the player what a block will send before they wire it, but not what it does with its inputs: B1 is a different block in every circuit. The player never sees a block's kind (such as "MIRROR"): a block shows only its label and its ports.

### Devices

**For now every output is a plain display** (see "Set aside: output devices" at the end). The table is what each device does when it is in play.

| Device | Does | Matches its target when |
|---|---|---|
| Display | Shows the signal itself, as a value, a strip of values or a graph | it is the same signal |
| Light | Lit on a 1; brighter for a bigger digit (9 is full); follows a wave above its middle and is dark below it; lit in a shape's colour, whatever its form or fill (shapes are set aside too) | it is as bright, in the same colour, at every moment |
| Speaker | Plays the signal | it is the same signal |
| Digit display | Shows a number: a digit, a bit, a shape's corners, a wave's height rounded | it shows the same numbers |
| Fan | Spins faster for more signal; a wave below its middle spins it backwards | it spins the same way at the same speed |
| Servo | Swings its arm from left (nothing, or a wave's bottom) to right (full, or a wave's top) | its arm is in the same place |

A device matches on what it does, not on the signal, so a digit display wanting 3 is met by the digit 3 or by a triangle.

### Signal media

The game has three kinds of signal, and **every signal is drawn as a waveform**: a line through the loop, even a steady bit. The one exception is a steady digit, which is just its number, written large. Shapes are set aside for now (see "Set aside: shapes" at the end).

| Chapter | Medium | Values | Drawn as |
|---|---|---|---|
| I First Light | binary | 0 and 1 | A line above a faint midline for 1 and below it for 0, square where it switches |
| II Tally, III Ticker | decimal | 0 to 9 | A steady digit (Tally) is its number, written large. A digit that changes (Ticker) is a stepped line whose height is the digit, 0 at the bottom up to 9 at the top, with each step's number written on it |
| IV Resonance | wave (analog) | sine, square or saw, at frequency F1 to F7 and amplitude A1 to A9, starting 0, ¼, ½ or ¾ of a cycle late | The waveform, with more cycles for a higher frequency and taller for a higher amplitude, its settings written under it ("F3 A5 +1/4") |

### Signals over time

Every signal is a **trace**: its level at each of the 288 steps of one loop of a run (`Components/Trace.cs`). An input can be a single value or a pattern, like `binary 0110`, held for an equal share of the loop each. Waves rise and fall.

* **Frequency** F1 to F7 fits 1, 2, 3, 4, 6, 8 or 12 cycles into a loop. Those are octaves and fifths of each other, so the pitches are in tune, and every cycle, half and quarter cycle lands exactly on a step.
* **Amplitude** A1 to A9 is the peak, 1 to 9 levels from the middle, so a digit block reads a wave's peak as its amplitude (12 Nov 2026, from fella: "let amplitudes go up to 9"; it was A1 to A7). A bit or digit read as a wave fills the whole height, -9 to 9.
* **Phase** moves an analog wave a quarter cycle at a time. A sine half a cycle late is the same sine upside down, so the two cancel when added; a sine and a cosine (a quarter apart) don't.

Bit and digit blocks work step by step through the loop, and read any signal as their own kind:

| Read as | From a bit | From a digit | From a wave |
|---|---|---|---|
| bit | itself | 1 unless 0 | 1 while above the middle |
| digit | 0 or 1 | itself | its height above the middle, rounded, and 0 below it: its peak is its amplitude (A7 peaks at 7) |
| analog wave | top when 1, bottom when 0 | a flat level, 0 at the bottom and 9 at the top | itself |

Wave blocks read any signal as a wave (the last row) and send a wave, so they chain in any order and never refuse a signal. Because every signal is drawn as a line, these readings can be seen: a digit read as a wave is the same line, and a wave read as a digit is its top half in steps, flat at 0 below the middle. A wave's amplitude is the digit its peak reads as (12 Nov 2026, from fella: "a wave's amplitude determines its digital amplitude"; it used to read 0 at the bottom up to 9 at the top).

Once the player has seen a block send something, pointing at a wire into it says how that block reads the wire when the kinds differ, such as "D1 reads this wave as a digit: its height above the middle, and 0 below it." (`Media.HowRead`, `BlockKind.Reads`). It waits for a run so it never gives away more than the label already says.

A steady bit is a flat line above or below the midline; a steady digit is just its number, as big as the box allows. A signal that changes is drawn across the loop: bits as a square line about the midline, with 1 and 0 written at its left; digits as a stepped line, each step with its number written on it (above the line in its lower half, below it in its upper half; the step playing now is white), or, when it changes more than eight times, with only its highest and lowest digits written at its left; waves as themselves, with their frequency and amplitude written under them ("F2 A7") wherever they appear: inputs, what blocks send, outputs and targets. A wave a block makes reads as the settings of the wave it is, if it is one (a flipped sine is the same sine half a cycle on, "+1/2"); otherwise as what can be measured, F for how many times it repeats a loop and A for how far it swings each way from its middle, to the nearest half (`Waves.Describe`). Numbers written on a line are in plain light grey at regular weight, as large as the line's box has room for, so they stay readable.

### Signal sounds

Each medium is heard in its own way, so a player can follow a circuit by ear as well as by eye. The sounds are synthesised in `Components/SignalSound.cs`.

| Medium | Sounds like |
|---|---|
| binary | A 1 is a clear beep. A 0 is only a soft, dull tap, so the player knows it played. |
| decimal | One short tone whose pitch is the digit, climbing a major scale: 0 is C4 (do), 7 is C5, 9 is E5. Every digit lasts as long. |
| wave | The wave itself. Its frequency is its pitch (F1 is D3, F3 is A4, F7 is A6) and its amplitude its loudness: the taller, the louder, A9 the loudest. Phase can't be heard. |

A signal that changes through the loop plays each of its values in turn. Signal sounds can be turned off in Settings (**SIGNAL SOUNDS**): runs then show each output with a soft blip instead. Clicking a signal always plays it. Each chapter's signal guide plays its main example, and every signal on it can be clicked, so the player learns the sounds before the first circuit.

## Progression

* Three parts. **Learn** has four chapters: bits, steady digits, digits that change, and waves. It teaches each signal and its four blocks. **Combine** (Patch, Junction, Outline, Blend, Earshot) teaches how kinds of signal read each other, how two kinds meet in one block, solid and dotted outlines, the three mixing blocks, and listen-only blocks, and gets harder all the way through (12 Nov 2026, after play-testing found Blend's openers too easy): Patch chains two blocks, Junction mostly three, and Blend three or four, with a spare input or two to rule out. Inside each chapter no circuit has fewer blocks or fewer ways to wire it than the one before, and each chapter opens and closes on a bigger circuit than the last (`ContentTests.PuzzlesGetHarderAfterLearn`). Every circuit has one answer, except Blend 3, whose two MIX blocks can swap jobs. **Build** (Unmarked, Blackbox, Lights Out) is the main game, with every kind of signal, and ends in the dark: Lights Out draws no signal at all. It climbs inside, and opens bigger than Blackbox, but needn't close bigger than Blackbox's three-input finale, since hearing a circuit through is the hard part. Unmarked keeps climbing from where Blend ends, and the first half of Blackbox climbs from there; late in Blackbox come the four blocks with three inputs, on smaller boards so the new block is the puzzle. Every circuit after Learn is solved by its own idea, a trick no other circuit needs (12 Nov 2026: a wave cancelling itself, a pattern meeting its own late copy, two delays standing in for a flip, a count read as a bit), listed under each chapter below. No two of them hand the player the same set of blocks (`ContentTests.NoTwoPuzzlesAfterLearnUseTheSameBlocks`). Hints nudge towards the trick without giving it away. Each later part opens when every circuit of the part before it is solved; the chapter screen lists every part and its chapters down the left, with a padlock while a part is locked and a solved count for each chapter.
* **DEBUG: OPEN EVERYTHING** in Settings opens every part, chapter and circuit, for testing.
* Within an open part, every chapter is open, with its first circuit open, so a player can begin with any medium.
* Inside a chapter, solving circuits opens the next ones, and there is always **one spare** open, so a player who is stuck can try the next circuit instead.
* **Continue** on the title screen goes on from the last circuit played, or to the earliest chapter with a circuit still to solve.
* The first time a player enters a chapter, its title card plays, then its **signal guide**: the chapter's kinds of signal drawn as the board draws them, with notes pointing at what to look for. Every signal on it plays when clicked, with a playhead running through the loop. Most guides are one page; Resonance's has two, and a click or Enter turns the page (Escape closes it). **SIGNAL GUIDE** in the pause menu (or G) opens it again.
  * First Light, **reading a bit**: a steady 1 and 0, then a bit that switches, with notes on the line above or below the middle (a beep or a soft tap), the faint middle line, and the loop playing left to right.
  * Tally, **reading a digit**: a steady digit, large, shown as just its number, and 0, 3, 6 and 9 in a row, each a higher tone.
  * Ticker, **digits that change**: a steady digit for comparison, a busy one with only its highest and lowest written, and a stepped line with a 0-to-9 scale beside it and each step's digit written on it.
  * Resonance, **reading a wave**: one cycle bracketed (F), the height from the middle to a peak bracketed (A), where F and A are written, the three forms, and a wave starting a quarter cycle late over the same wave on time. Then a second page, **which way round**: the same two waves wired into a block's top and bottom inputs one way and then swapped, with a question mark for what comes out; some blocks treat both inputs alike, others give each its own job, so a run that is close but wrong may only need the wires swapped. It shows no block's rule and no result.
  * Patch, Junction, Outline and Blend, **how a block reads each kind**: a bit, a digit and a wave, each drawn as a bit block, a digit block and a wave block reads it, with the reading written under it.
  * Earshot, **listen-only blocks**: a listen-only block as the board draws it, the speaker beside its label and the speaker over it that plays what it sends; a chain from it through a block with no mark to an output, every one a speaker, since everything after it is only heard until the circuit is solved; then how each kind sounds and what can't be heard (when a wave starts, and which way up it is).
  * Unmarked and Blackbox, **every kind of signal**: the three kinds side by side, with what to look for in each and how each kind of block reads the others.
  * Lights Out, **by ear alone**: nothing is drawn, so three steps to try (listen to the targets, listen to the inputs, wire and run), what can't be heard, and the promise that an output that sounds like its target is its target.
* Every circuit is named by its chapter and its number in it, such as **TALLY 1**, on the circuit screen, the chapter map, the pause menu and the title screen's Continue line (`PuzzleLoader.NameOf`). The files give circuits no names.
* Every Learn chapter follows the same arc, so the media are the thing that changes. First Light has one more circuit: its first two use steady bits, and the third brings in bits that switch. Resonance has one more too: its third, which comes wired, teaches that which input goes on top can matter. Digits come in two chapters, steady whole digits in Tally and digits that change in Ticker; Tally has five circuits, and Ticker, which brings no new blocks, starts straight in with the player drawing every wire.
  1. **Pre-wired.** Two inputs and one block, every wire drawn, but the block's wire starts at the wrong input. The player moves it to the other one.
  2. **Half wired.** A two-input block with one input empty. The player finishes it.
  3. From here the player draws every wire: two blocks in a chain, with a spare input or the order of the blocks to work out.
  4. One source feeding two blocks, and a second output.
  5. A two-input block where order matters, or three blocks.
  6. The **tangle**: four blocks, three inputs and two or three outputs.
* Saves from before the wiring game (version 2 and older) keep only their preferences.

* **No circuit solves itself** (12 Nov 2026, from fella: tutorial circuits were so easy a player could solve them without learning anything). After the first two circuits of each chapter, every circuit makes the player decide something: which input, which block comes first, which way round. Counted over the wirings that keep the rules (`WiringSolver.Analyse` with `countLegal`), there are at least 12, they send at least 4 different things, and at most one in eight solves it (`ContentTests.CircuitsAfterTheFirstTwoNeedADecision`). The block listed first in a chain's file goes on top of the board, so a chain's blocks are listed last first: the board's order never gives away the chain's.

The tables give the number of ways to wire the blocks' inputs, and how many complete wirings solve the circuit. Swapping the wires into a block that treats its inputs alike (AND, OR, XOR, MAX, MIN, DIFF, MIX, MAJORITY, MIDDLE, and BETWEEN's two bounds) counts as the same answer (`BlockKind.AlikeFrom`).

### Chapter I: First Light

*Every block keeps a rule. Wire it to find out.* Bits: NOT, AND, OR, XOR.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | A steady bit. Pre-wired: move one wire to the other input, and NOT | 2 | 1 |
| 2 | Steady bits. Half wired: an empty input sends nothing, and AND | 4 | 2 |
| 3 | Bits that switch through the loop, a block working on every moment. Wiring it all: OR then NOT, with a spare input | 64 | 1 |
| 4 | Three patterns, AND and OR: which two go into AND, and which block comes first | 256 | 1 |
| 5 | One input feeding two blocks, and XOR | 27 | 1 |
| 6 | Three blocks in a row | 3,125 | 8 |
| 7 | Four blocks, two outputs | 279,936 | 37 |

The game starts with **steady bits**, each 0 or 1 for the whole loop, and moves on to bit patterns (such as 0011, 0 for half the loop and then 1) from Sequence on, so a new player meets one bit at a time first. A steady bit target can only be 0 or 1, so an input may already match it; those circuits are excused from the straight-wire check below, and the every-block rule still stops a straight wire solving them. Bit circuits still have more solutions than the others: AND or OR of a signal with itself is that signal, and a steady 1 or 0 can stand in for many things.

### Chapter II: Tally

*Every digit holds steady.* Digits: MIRROR, MAX, MIN, DIFF, on whole digits that stay the same all loop, each shown as just its number. Steady digits leave few different answers, so the later circuits here have several ways through.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | Pre-wired: move one wire to the other digit, and MIRROR | 2 | 1 |
| 2 | Half wired, and DIFF | 4 | 1 |
| 3 | MAX, then DIFF with the third digit; the order matters | 256 | 5 |
| 4 | MIN, and one input feeding two blocks | 1,024 | 14 |
| 5 | Three blocks in a row, two outputs | 3,125 | 10 |

### Chapter III: Ticker

*Count what changes.* The same four digit blocks, on digits that change as the loop plays, drawn as a stepped line. No new blocks, so it skips the pre-wired and half-wired openers: the player draws every wire.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | A pattern of digits, MIRRORed step by step; a steady digit is the spare | 2 | 1 |
| 2 | MAX of a pattern and a steady digit: a floor | 4 | 1 |
| 3 | MIN of two patterns, then MIRROR; a third pattern is spare | 64 | 1 |
| 4 | One pattern feeding two blocks, two outputs | 27 | 1 |
| 5 | A longer pattern, MIN with its own MIRROR: one input in two places, beside another long pattern and a steady digit | 64 | 1 |
| 6 | Four blocks, two outputs | 279,936 | 3 |

### Chapter IV: Resonance

*Listen closely.* Analog waves: FLIP, FASTER, LATER, MIX, and GATE for one lesson. Its third circuit, TURNAROUND, comes wired with GATE's two inputs the wrong way round, and FASTER not used yet, to teach that which input goes on top can matter (the answer swaps them, with FASTER on the way to the top): MIX, met just before, treats its inputs alike, and so do the other Part One blocks with two inputs, so this is the only place in Part One a mixing block appears (a test allows just this one). GATE is a wave block; here both its inputs are waves, and it comes back in Blend with a bit pattern on its bottom input.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | Pre-wired: move one wire to the other wave, and FASTER | 2 | 1 |
| 2 | Half wired, and MIX | 4 | 1 |
| 3 | Pre-wired the wrong way round, with FASTER unused: swap GATE's two inputs, because which input goes on top matters, and speed up the one going on top; a third wave is spare | 64 | 1 |
| 4 | LATER on one wave, then MIX with another; the order matters, and a third wave is spare | 64 | 1 |
| 5 | FLIP, feeding an output and a block at once | 27 | 1 |
| 6 | Two outputs, by ear | 64 | 2 |
| 7 | Four blocks, two outputs | 7,776 | 1 |

Every circuit is checked by trying every way to wire it, in the tests and from **BLACKBOX → Validate Puzzles**. Each one's solution must work, it must not be solved as it starts, and no input wired straight to an output may already meet that output's target.

### Chapter V: Patch

*Every block reads every signal.* Blocks of one kind, fed signals of another. Each circuit has three or four inputs of mixed kinds, one or two of them spare, and two blocks in a chain, so the player has to work out which signal each block reads and what it reads it as. The finale has three blocks and two outputs.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | AND reads a pattern of digits as bits, then NOT; a steady digit is the spare | 64 | 1 |
| 2 | MIRROR reads a saw by its height, then MIN with a digit | 64 | 1 |
| 3 | FLIP reads digits as levels, then MIX with a steady 9 lifts them to the top half | 64 | 1 |
| 4 | LATER reads bits as top and bottom and delays them, then MIX with a saw. A decoy: one optional (dotted) input already shows LATER's result, but every block must be used, so it has to be built | 125 | 1 |
| 5 | The two readings Patch hasn't shown: NOT reads a wave as bits (then AND with a pattern of digits), and MIRROR reads bits as the digits 0 and 1 | 625 | 1 |

### Chapter VI: Junction

*Where kinds of signal meet.* Two kinds of signal into one block of a Part One kind, the step between Patch (one kind read as another) and Blend (the mixing blocks). No mixing blocks yet; a test checks that every solution feeds some block two kinds at once. The opener chains two blocks; every other circuit chains three, with a spare input or two.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | AND of a pattern of digits and a pattern of bits, then XOR with a square wave | 256 | 1 |
| 2 | MIX of a wave and bits, then LATER, then FASTER: the order matters | 1,296 | 1 |
| 3 | DIFF of a digit and a bit takes 0 or 1 off, then MIRROR, then MIN with a saw read as a rising digit | 3,125 | 1 |
| 4 | XOR of a pattern and its own LATER copy lights where the pattern changed, then AND with a saw read as bits | 3,125 | 1 |
| 5 | MAX of a saw read as digits and a steady digit, then DIFF with that digit again (one input feeding two blocks), then MIRROR | 7,776 | 1 |
| 6 | A bit, a digit and a wave, three blocks, three outputs | 15,625 | 1 |

### Chapter VII: Outline

*Solid has to be used. Dotted may not be.* Required (solid) inputs and optional (dotted) blocks, each circuit with its own way of reading the outlines. Every circuit here has a solid input or a dotted block; outside this chapter every input is dotted and no block is.

| # | The trick | Wirings | Solutions |
|---|---|---|---|
| 1 | The one solid input is the place to start: MIN of it and a wave, then DIFF | 625 | 1 |
| 2 | Two dotted blocks: LATER is in the answer, FLIP isn't, and the outlines look the same | 900 | 1 |
| 3 | The solid digit pattern doesn't fit MAX as it is; the dotted MIRROR turns it round first, and the dotted NOT is a decoy | 900 | 1 |
| 4 | Both dotted blocks are needed, LATER then FASTER, and the order matters; two solid inputs | 63,504 | 1 |
| 5 | Two solid waves: a dotted FASTER is used, a dotted NOT isn't, and the MIX feeds both an output and AND | 63,504 | 1 |

### Chapter VIII: Blend

*Signals of different kinds, mixed together.* Wave blocks meet other kinds, then the three mixing blocks: GATE, first met in Resonance with two waves, now with a bit pattern; then COUNT and COMPARE, each met beside a block the player already knows.

| # | Teaches | Wirings | Solutions |
|---|---|---|---|
| 1 | FASTER and FLIP read bit patterns as square waves; one mixed with a sine | 1,296 | 1 |
| 2 | A wave into AND blinks a bit, FLIP turns the bits back into a wave, and MIX with a digit lifts it | 3,125 | 1 |
| 3 | A wave and its own copy half a cycle later (LATER) cancel in MIX, and the silence halves a bit pattern in a second MIX (either MIX can do either job) | 7,776 | 2 |
| 4 | No MIRROR, but FLIP turns a digit read as a level upside down, which is the same thing: MIN of a saw and the flipped digit, then MIX with bits | 7,776 | 1 |
| 5 | GATE lets a sped-up saw through a pattern made by XOR | 7,776 | 1 |
| 6 | COUNT counts the switch-ons of a gated wave, and DIFF takes the count from a digit; the gate feeds an output too | 7,776 | 1 |
| 7 | COMPARE against a mirrored digit makes a pulse, and AND cuts it with bits | 7,776 | 1 |
| 8 | Four blocks: COMPARE makes pulses that COUNT counts and that GATE lets a sped-up saw through | 117,649 | 1 |

### Chapter IX: Earshot

*Some blocks you can only hear.* Listen-only blocks, after Blend so they can be mixing blocks too. Whatever a listen-only block feeds is only heard as well, so most of each circuit is worked out by ear until it's solved. Every circuit has one or two in its answer, and each sends something that sounds different from what goes in, except the one LATER taught as what can't be heard (`ContentTests.ListenOnlyBlocksAreHeardAndMarked`).

| # | The trick | Wirings | Solutions |
|---|---|---|---|
| 1 | A listen-only MIRROR: the tune turned upside down, then MAX with a digit; a dotted FLIP decoy | 1,512 | 1 |
| 2 | A listen-only MIX straight into an output, so the output is heard too: compare it with the target by ear; FASTER feeds the MIX and MAX | 7,776 | 1 |
| 3 | A listen-only COUNT climbs like a scale; DIFF from 9 and MAX | 7,776 | 1 |
| 4 | Two listen-only blocks: FASTER can be heard, LATER can't, so it has to be worked out from what it does further on, where MAX and MIN read the delayed wave as a tune that starts in a new place | 117,649 | 1 |
| 5 | A listen-only GATE and MIRROR, worked out by ear before wiring the rest | 823,543 | 1 |

### Chapter X: Unmarked

*Every block you've met, all mixed up.* The chapter climbs on from where Blend ends, from three blocks to five (12 Nov 2026, after play-testing found the old two-block Unmarked easier than late Blend), and each circuit turns on its own trick.

| # | The trick | Wirings | Solutions |
|---|---|---|---|
| 1 | A signal meets its own MIRROR: MIN of the two is a rise and fall, MAX a fall and rise | 3,125 | 1 |
| 2 | One input feeds both ends of a chain: COMPARE squares a sine off, FLIP reads the bits as a wave, and MIX adds the sine back | 3,125 | 1 |
| 3 | FASTER before COMPARE, so the beats come at the target's rate, and AND with a pattern pairs them | 7,776 | 2 |
| 4 | MIN cuts off the top and MAX the bottom; COMPARE shares MIN's limit | 15,625 | 2 |
| 5 | No FLIP: two LATERs put a one-cycle sine half a loop late, which is the same as upside down, then MIX and COMPARE | 117,649 | 2 |
| 6 | One signal in both of GATE's inputs keeps only its top half; again on the flipped wave, and MIX adds the two | 279,936 | 2 |
| 7 | Three separate machines, five blocks: sort out which blocks belong together | 2,097,152 | 2 |

The circuits with two solutions have two alike blocks that can swap, or one block whose inputs can swap without changing what it sends.

### Chapter XI: Blackbox

*Everything at once.* 01 to 05 climb in size from bigger than Unmarked's opener. From 06 on, each circuit brings in a block with three inputs, on a smaller board so the new block is the puzzle, and the finale uses one.

| # | The trick | Wirings | Solutions |
|---|---|---|---|
| 1 | A count read as a bit is 0 until the first switch-on and 1 after it, so COUNT drives GATE and AND | 7,776 | 1 |
| 2 | A count read as a level by MIX climbs in steps; MAX holds it at a floor | 7,776 | 1 |
| 3 | DIFF of two waves read as digits is how far apart they are; COMPARE and COUNT read the gap | 7,776 | 1 |
| 4 | A quiet sine sped up twice rides on a slow one in MIX, and COMPARE lights while the sum is high | 117,649 | 2 |
| 5 | One saw read three ways: NOT then AND, XOR, and DIFF from 9 | 279,936 | 2 |
| 6 | MAJORITY of two bit patterns and COMPARE's pulse, then NOT | 46,656 | 1 |
| 7 | MIDDLE holds a saw between a digit and its MIRROR (Unmarked 4's two blocks in one), then FLIP | 7,776 | 1 |
| 8 | BETWEEN with the steady level on top and a wave and its FLIP as the bounds: lit while the wave is far from the middle; COUNT counts | 7,776 | 1 |
| 9 | SWITCH chooses between a sine and its sped-up copy, by a pattern NOT turns over | 7,776 | 1 |
| 10 | The higher of two waves (COMPARE choosing for SWITCH), how many times they cross, and a gated saw | 1,679,616 | 2 |

### Chapter XII: Lights Out

*Nothing is drawn. Listen.* The whole board is heard, never seen: inputs, blocks, outputs and targets are speakers. The circuits use mostly bits and digits, whose order through the loop can be heard, and waves mainly where a bit or digit block reads them. They were found by a search for circuits with one answer and nothing that sounds right but isn't, then picked for clear tunes and rhythms.

| # | The trick | Wirings | Solutions |
|---|---|---|---|
| 1 | Hear a rising tune turned upside down and held at a floor; a rhythm cut by a wave's top half | 16,807 | 1 |
| 2 | A loud square wave heard as a digit is 7 then 0: DIFF with a tune, and a rhythm XORed then clipped by it | 117,649 | 1 |
| 3 | Two inputs each go two places: MIN of a tune and a square, and a mirrored tune turned to a beat | 117,649 | 1 |
| 4 | Both targets follow one input's rhythm: COMPARE then MIRROR, and NOT then DIFF | 262,144 | 1 |
| 5 | One target climbs step by step: start there. The other is a falling tune turned over by MIRROR, heard as beats by AND | 823,543 | 1 |

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
* **Screens** live on a stack. Opening a screen replaces the stack with a fade. Overlays (pause, settings, confirm, help, a chapter's title card and signal guide) sit on top, behind a dark veil. Only the top screen gets keys.
* **Input** works with either backend: the Input System package or the old Input Manager.
* **Saving** writes `blackbox-save.json` to `Application.persistentDataPath`. It goes through a temporary file, so a crash can't leave half a save. It stores solved circuits and best move counts, the wires the player left on each circuit, their notes (pen strokes and typed text), which chapter cards have been seen, and preferences. Saves from before the wiring game keep only their preferences.

### Evaluation

Values are small whole numbers, one range per medium: 2 for bits, 10 for digits, 756 analog waves (((form × 7 + frequency) × 9 + amplitude) × 4 + phase), and, set aside for now, 32 shapes ((colour × 4 + form) × 2 + fill) and 567 sampled waves (((form × 7 + frequency) × 9 + amplitude) × 3 + steps). Inputs are traces: 288 steps of one loop, each a level of the signal's medium. `WiringSession` evaluates the wires from the inputs on, and `WiringSolver` tries every way to wire a circuit's blocks (skipping loops), then every source each output could take, to count the solutions. Circuits have no memory and no loops; a wire that would make one is refused.

### Adding a chapter

1. Write `chapter-13-<name>.json` next to the others (see [PuzzleFormat.md](PuzzleFormat.md)).
2. Add its file name to a part in `chapters.json`.
3. To make it wait for an earlier chapter, give it `"unlock": { "after": "<chapter id>", "solved": N }`. Leave that out and it is open from the start, like the shipped chapters.
4. Run **BLACKBOX → Validate Puzzles**, then the EditMode tests.

New kinds of block go in `Components/BlockKinds.cs`, with tests in `ComponentTests.cs` and `WiringTests.cs`. Each kind belongs to one medium and has one rule: no settings, and nothing a player would call an exception. A new medium needs a value range in `Media.cs`, a way to draw it in `UI/Board/ValueView.cs`, and words for goal sentences in `GoalText.cs`.

## Spoilers: the rules

A block shows only its label (B1, D1, W1...) and its ports. The kind ids below are for puzzle files and designers; players never see them. Port 0 is the top input. Once a circuit is solved, each of its blocks has its rule written under it (`BlockKind.ShortRule`, the short form below, in plain words that fit two short lines; pointing at the block shows the whole rule), and keeps it whenever that circuit is opened again. For testing, **DEBUG: BLOCK RULES** in Settings (off by default) writes every block's rule under it, solved or not.

Each kind of signal has four blocks, and each set starts with one that flips the signal, so a player who has learned one set has a head start on the next. The shape blocks (FILL, NEXT, TAKEFILL, TAKECOLOUR) are set aside with shapes, and no circuit uses them for now.

| Kind | Signal | Inputs | Rule | Short form |
|---|---|---|---|---|
| NOT | bit | 1 | 1 becomes 0, 0 becomes 1 | swaps 0 and 1 |
| AND | bit | 2 | 1 only while both are 1 | 1 only when both inputs are 1 |
| OR | bit | 2 | 1 while either is 1 | 1 when either input is 1 |
| XOR | bit | 2 | 1 while they are different | 1 when the two inputs differ |
| MIRROR | digit | 1 | 9 minus the digit: 0 and 9 swap, 1 and 8, 2 and 7 | 9 minus the digit: 2 becomes 7 |
| MAX | digit | 2 | the larger digit | the larger of the two digits |
| MIN | digit | 2 | the smaller digit | the smaller of the two digits |
| DIFF | digit | 2 | how far apart they are: the larger minus the smaller | the larger digit minus the smaller |
| FILL (set aside) | shape | 1 | hollow becomes solid, solid becomes hollow | swaps solid and hollow |
| NEXT (set aside) | shape | 1 | the next shape along: circle, triangle, square, hexagon, then circle again; fill and colour kept | next shape: circle, triangle, square, hexagon |
| TAKEFILL (set aside) | shape | 2 | the top shape, with the bottom shape's fill | top shape, filled like the bottom one |
| TAKECOLOUR (set aside) | shape | 2 | the top shape, in the bottom shape's colour | top shape, coloured like the bottom one |
| FLIP | wave | 1 | the wave upside down | turns the wave upside down |
| FASTER | wave | 1 | twice as fast, so it repeats twice as often | plays the wave twice as fast |
| LATER | wave | 1 | a quarter of the loop later | delays the wave by a quarter loop |
| MIX | wave | 2 | the two waves added together at half height (their average at every moment) | adds the two waves, at half height |
| GATE | wave | 2 | the top signal, read as a wave, while the bottom is on; otherwise a flat line | lets the top through while the bottom is on |
| COUNT | digit | 1 | how many times its input has switched on so far this loop, stopping at 9 | counts each switch-on, up to 9 |
| COMPARE | bit | 2 | 1 while the top is higher than the bottom, both read as waves; otherwise 0 | 1 while the top is higher than the bottom |
| MAJORITY | bit | 3 | 1 while at least two of the three are 1 | 1 when at least two inputs are 1 |
| MIDDLE | digit | 3 | the middle one of the three digits: neither the largest nor the smallest | the middle one of the three digits |
| BETWEEN | bit | 3 | 1 while the top is between the other two, all read as waves; otherwise 0 | 1 while the top is between the other two |
| SWITCH | wave | 3 | the top signal while the bottom is on; otherwise the middle one, both read as waves | top while the bottom is on, else the middle |

Bit and digit blocks work step by step, reading each input as their own kind. Wave blocks read each input as a wave, except the on/off input of GATE and SWITCH, read as a bit. COUNT reads its input as a bit. So every block always sends its own type, and its label says which (`ComponentTests.EveryBlockAlwaysSendsItsOwnKind`). Until 12 Nov 2026 GATE and SWITCH sent whatever type their top input was; they became wave blocks when labels started naming the type.

The four blocks with three inputs come last, late in Blackbox, one to a circuit at first: MAJORITY and MIDDLE are three-input cousins of the bit and digit blocks, BETWEEN reads all three as waves like COMPARE, and SWITCH is a wave block like GATE, reading its bottom input as a bit.

The few edges a player can bump into, all kept because they follow from the rule rather than add to it: COUNT stops at 9 (a digit has nowhere further to go), and a mixed or flipped wave is still a wave, so FASTER and LATER work on it.

Until 11 Nov 2026 blocks had settings: the old kinds (codes B1 to B4, D1 to D4, S1 to S4, W1 to W4, M1 to M8, nothing to do with today's labels) each had two to four rules, shown as dots on the block. They were replaced by the single-rule kinds above because a block the player can't change should do one thing they can name.

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

## Set aside: shapes

On 11 Nov 2026 shapes were taken out of the game for now, so that every signal can be drawn as a waveform: bits and digits as lines, steady or changing, beside the waves. A shape was a circle, triangle, square or hexagon, hollow or solid, in cyan, pink, amber or green, drawn as itself and heard as one click per corner. Shapes had Chapter III, *Forms* ("Every shape is a message."), and four blocks: FILL, NEXT, TAKEFILL and TAKECOLOUR. Forms' circuits were:

|# | Circuit | Teaches | Wirings | Solutions |
|---|---|---|---|---|
| 01 | Fill | Pre-wired, and FILL | 2 | 1 |
| 02 | Graft | Half wired, and TAKEFILL | 4 | 1 |
| 03 | Repaint | TAKECOLOUR, on a pattern of shapes | 4 | 1 |
| 04 | Turn | NEXT, beside FILL on the same input | 9 | 1 |
| 05 | Homecoming | Colour stays with the form | 625 | 16 |
| 06 | Lattice | Four blocks, three outputs | 46,656 | 22 |

The circuits in Patch, Junction, Unmarked and Blackbox that used shapes were rebuilt with bits, digits and waves: Patch 02 to 04 (*Corner Count*, *Branch* and *Bit Shapes* became *Reading*, *Height* and *Level*), Junction 01, 02 and 05 (*Corners*, *Solid State* and *Flicker* became *Both On*, *Step Down* and *Lift*), Unmarked 06 (*Colours* became *Reflections*), and new signals or blocks in Patch 01 and 05, Junction 04 and 06, Unmarked 02 and Blackbox 04 and 06. The chapters after Forms moved up a number, and their files were renamed to match. Commit 35d2202 is the last with shapes in play.

The code that makes, draws, sounds and reads shapes is all still there, with its tests. `KindCatalog.Shelved` lists the four shape blocks and `KindCatalog.MediumShelved` the medium, and the loader refuses any circuit that uses them. Forms' file is kept in `Assets/_Project/Puzzles/Shelved/`, outside `Resources`, so the game never loads it.

To bring them back: take shapes and their blocks out of both checks in `KindCatalog`, move Forms back into `BlackboxPuzzles`, list it in `chapters.json` after Tally, renumber the chapters after it, add a shape sample back to the HOW IT WORKS overlay (`Overlays.cs`), and check every circuit with **BLACKBOX → Validate Puzzles**.

## Set aside: output devices

On 11 Nov 2026 every output became a plain display, for now. Lights, speakers, digit displays, fans and servos are listed in `Devices.Shelved`, and the loader refuses a circuit that uses one. Their drawing, matching and sound code is all kept, with its tests, and the Devices table above says what each did. Commit aea4196 has every circuit with its device.

A display needs the very same signal, where a device matched on what it did, so some circuits now have fewer solutions. Every hint and chapter line that talked about a device was reworded. Four circuits were renamed because their names were about devices: Junction 03 *Lights Out* became *Cut Off*, Unmarked 02 *Dim* became *Matchmaker*, and Blackbox 03 became *Downhill*. Patch 05 *Moving Parts* became *Crossover*: it fed a shape to a fan and bits to a servo, which shows nothing on displays, so now each block is fed the other kind of signal.

To bring them back: empty `Devices.Shelved`, give the circuits their devices again (from commit aea4196, or new ones), restore the device lines in the hints, chapter lines and the HOW IT WORKS overlay, and check every circuit with **BLACKBOX → Validate Puzzles**.

