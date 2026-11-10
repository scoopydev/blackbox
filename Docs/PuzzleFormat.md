# Puzzle format

Circuits are plain JSON files in `Assets/_Project/Puzzles/Resources/BlackboxPuzzles/`.
`//` comments are allowed. The loader's error messages name the file, the circuit and the problem, and the game shows them on screen if a file can't be read.

After editing, run **BLACKBOX → Validate Puzzles** in Unity, or the EditMode tests. Both solve every circuit by brute force: settings circuits by trying every input and block state, wired circuits by trying every way to wire them.

There are two kinds of circuit. In a **settings** circuit (Part One) the wires are drawn already and the player changes inputs and block states. In a **wired** circuit (Parts Two and Three) every block is fixed and the player draws the wires. Wired circuits are described [further down](#wired-circuits).

## chapters.json

The game in parts, each a list of chapter files (names without `.json`) in play order:

```json
{
  "parts": [
    { "id": "learn", "title": "Learn", "tagline": "PART ONE  ·  WHAT EACH SIGNAL AND BLOCK DOES",
      "chapters": ["chapter-1-first-light", "chapter-2-tally"] },
    { "id": "combine", "title": "Combine", "tagline": "PART TWO  ·  WIRING, AND HOW SIGNALS COMBINE",
      "afterPrevious": true, "chapters": ["chapter-6-patch", "chapter-7-blend"] }
  ]
}
```

| Key | Required | Meaning |
|---|---|---|
| `id` | yes | Stable id. |
| `title` | yes | The part's tab on the chapter map. |
| `tagline` | no | One line under the tabs. |
| `afterPrevious` | no | `true` keeps the part locked until every circuit of the part before it is solved. Inside an open part, every chapter's first circuit is open. |
| `chapters` | yes | Chapter file names, in play order. |

A file with a plain `"chapters": [...]` list instead of `"parts"` is read as one part.

## A chapter file

```json
{
  "id": "tally",
  "numeral": "II",
  "title": "Tally",
  "medium": "decimal",
  "tagline": "Count what changes.",
  "signals": "Signals here are digits from 0 to 9. They wrap round, so one more than 9 is 0.",
  "circuits": [ ... ]
}
```

| Key | Required | Meaning |
|---|---|---|
| `id` | yes | Stable id. Saves refer to it, so don't rename it once players have it. |
| `title` | yes | Shown on the chapter map and the title card. |
| `medium` | yes, for settings chapters | The kind of signal: `binary`, `decimal`, `shape`, `wave` (analog) or `sampled`. Every circuit in the chapter uses it. Wired chapters mix signals and leave it out. |
| `mode` | no | `"wiring"` for a chapter of wired circuits. The default is `"settings"`. |
| `marked` | no | `false` hides the kind code on every block, as in Part Three. Wired chapters only. |
| `numeral` | no | Defaults to the chapter's position in Roman numerals. |
| `tagline` | no | One line under the title. |
| `signals` | no | One line about the medium, shown on the title card and in the help. |
| `unlock` | no | `{ "after": "first-light", "solved": 4 }` makes the chapter wait until `solved` circuits of chapter `after` are solved. Leave it out for a chapter that is open from the start, as all the shipped chapters are. |
| `circuits` | yes | The circuits, easiest first. |

## A circuit

```json
{
  "id": "first-light-03",
  "name": "Under the Lid",
  "hint": "You can't see what a wire carries. Click a wire to put a probe on it.",
  "probes": 1,
  "inputs": {
    "A": { "at": [0, 0], "value": 0, "adjustable": true },
    "B": { "at": [0, 2], "value": 0, "adjustable": true }
  },
  "blocks": {
    "g": { "kind": "B2", "state": 0, "at": [1, 1], "target": 1 },
    "k": { "kind": "B3", "state": 0, "at": [2, 1] }
  },
  "outputs": {
    "X": { "at": [3, 1], "target": 0 }
  },
  "wires": ["A -> g.0", "B -> g.1", "g -> k", "k -> X"]
}
```

| Key | Required | Meaning |
|---|---|---|
| `id` | yes | Unique across all chapters. Saves refer to it. |
| `name` | no | Shown in the header and on the chapter map. |
| `hint` | no | One line under the board. Use it to teach a control, not to give away a rule. |
| `goal` | no | The goal sentence. Leave it out and it is written from the targets, which is almost always what you want. |
| `probes` | no | How many probes the player may place. Defaults to 0. |
| `medium` | no | Overrides the chapter's medium for this circuit. |
| `inputs` | yes | See below. Input ids are shown beside the input, so keep them to one or two letters. |
| `blocks` | no | See below. |
| `outputs` | yes | See below. |
| `wires` | yes | See below. Every block port and every output needs exactly one wire into it. |

There must be at least one target somewhere, or the circuit can never be solved. The validator checks this.

### Inputs

```json
"A": { "at": [0, 0], "value": 3, "adjustable": true, "target": 3 }
"B": [0, 1]
```

| Key | Meaning |
|---|---|
| `at` | Position, `[column, row]`. |
| `value` | Where the input starts. Defaults to the medium's first value. |
| `adjustable` | `true` gives the input arrows so the player can change it. Inputs are fixed by default. |
| `options` | The values an adjustable input steps through, in order, such as `["sine f1 a7", "sine f2 a7", "sine f3 a7"]`. Without it the input steps through every value of the medium. Wave and sampled inputs have hundreds, so an adjustable one must list its options: two to five that differ in the one setting the circuit is about. `value` and any `target` must be among them. |
| `target` | The value the input must end at. Give it the same value as `value` for a **return** circuit, where the player has to change the input to learn, then put it back. |

The short form `[column, row]` is a fixed input at the first value.

### Blocks

```json
"g": { "kind": "B2", "state": 0, "locked": false, "at": [1, 1], "target": 1 }
```

| Key | Meaning |
|---|---|
| `kind` | The block kind, such as `B2`. It must belong to the circuit's medium. See the table below. |
| `state` | The state the block starts in, counting from 0. |
| `locked` | `true` draws a padlock and stops the player changing the block. |
| `at` | Position, `[column, row]`. |
| `target` | The value the block must send. The board shows the wanted value and a tick or cross, but not what the block sends now. |

### Outputs

```json
"X": { "at": [3, 1], "target": 0 }
"Y": [3, 2]
```

An output with a `target` shows its value now beside the target. One without a target just shows its value, which makes it a free window into the circuit.

### Values

Write values the way the player sees them:

| Medium | Values | Examples |
|---|---|---|
| `binary` | 0 and 1 | `1` |
| `decimal` | 0 to 9 | `7` |
| `shape` | `hollow` or `solid`, then `circle`, `triangle`, `square` or `hexagon` | `"solid triangle"` |
| `wave` | `sine`, `square` or `saw`, then `f1` to `f7` (frequency) and `a1` to `a7` (amplitude), and optionally `p1`, `p2` or `p3` (quarter cycles late), in any order | `"sine f3 a5"`, `"square f1 a7 p2"` |
| `sampled` | the same form, frequency and amplitude, then `s3`, `s6` or `s12` (steps a cycle) | `"saw f2 a6 s6"` |

Two sampled waves can trace the same staircase: a square is a square whether it is read 6 or 12 times a cycle, and a low sine read 3 times rounds the same way at A3 and A4. Either meets a target that names the other. Write targets as the intended solution makes them, so the target's caption matches the output's; a test checks this.

### Positions

`[column, row]` places a node on the board grid. Columns run left to right and rows run top to bottom. Halves are fine, such as `[1, 0.5]`. The board scales to fit, so only the relative positions matter.

Positions are optional. If any node has no position, the circuit is laid out automatically: each node goes one column after the furthest node that feeds it. Hand-placed nodes keep their place.

### Wires

```
"A -> g.0"         from A into port 0 (the top port) of g
"g -> k"           a one-input block or an output doesn't need a port
```

One source can feed any number of wires. Signals flow one way, from inputs to outputs, so a loop is rejected when the circuit loads. Wires that skip columns are routed along a free lane between rows, so they never pass behind a block.

## Wired circuits

A chapter with `"mode": "wiring"` holds wired circuits. The player gets the inputs, the blocks and the outputs, with no wires, and wires them so every output does what its target shows. A wired circuit is solved when, in one run:

* every output's device does the same as its target does (see [Devices](#devices)),
* every input port of every block has a wire, and
* every block leads to an output, through other blocks or straight there.

Inputs may go unused. Blocks are fixed: the player never changes a state, so every block is locked and keeps the state the file gives it.

```json
{
  "id": "blend-01",
  "name": "Pulse",
  "hint": "A wave into a bit block turns on and off as the wave rises and falls.",
  "inputs": {
    "A": { "signal": "binary 1" },
    "B": "wave sine f2 a7",
    "C": "wave sine f3 a7"
  },
  "blocks": {
    "g": { "kind": "B2", "state": 0 }
  },
  "outputs": {
    "X": { "device": "light" }
  },
  "solution": ["A -> g.0", "C -> g.1", "g -> X"]
}
```

| Key | Meaning |
|---|---|
| `inputs` | Each input is a signal (below), written alone or as `{ "signal": ..., "at": [col, row] }`. Inputs are fixed: no `value`, `adjustable` or `target`. |
| `blocks` | Each block is `{ "kind": "B2", "state": 0, "at": [col, row] }`. Any bit, digit, shape or M kind can be wired. The wave-setting kinds (W and Q) can't, because they change a wave's settings rather than its signal. |
| `outputs` | Each output is a device name, or `{ "device": "fan", "at": [col, row] }`. The default device is `display`. |
| `solution` | The intended wiring, as wires. It must keep the rules above. The targets come from it: each output wants what the solution sends it. |
| `goal` | Optional. The default goal names every output and its device, and states the rules. |

There are no probes in a wired circuit: to see what a block sends, the player wires it to an output. Leave out `at` and the loader lays the board out: inputs on the left, blocks in one column (up to 3 blocks), two (up to 8) or three, in file order, and outputs on the right. Keep a circuit to 8 blocks or fewer.

### Signals

A signal is its type and its value, or a pattern of values played in turn through the loop:

```
"binary 1"                          a steady 1
"binary 0110"                       0 for a quarter of the loop, then 1, 1, 0
"decimal 3141"                      digits; a run of digits is one per step
"shape solid triangle, hollow circle"
"wave sine f2 a7"                   an analog wave: form, frequency f1 to f7, height a1 to a7
"wave sine f2 a7 p2"                the same wave half a cycle later (p1 to p3 are quarter cycles)
"sampled saw f1 a7 s6"              a sampled wave, read 3, 6 or 12 times a cycle
```

A pattern must divide the loop evenly: 1, 2, 3, 4, 6, 8, 9, 12 or 16 values (and others that divide 288). Waves are always steady: their settings don't change.

### Devices

| Device | `device` | Does | Matches its target when |
|---|---|---|---|
| Display | `display` | Shows the signal itself, as a value, a strip of values or a graph | it is the same signal |
| Light | `light` | Lit on a 1. Brighter for a bigger digit (9 is full). Follows a wave above its middle and is dark below it. A shape lights it in that form's colour, full for solid and half for hollow | it is as bright, in the same colour, at every moment |
| Speaker | `speaker` | Plays the signal | it is the same signal |
| Digit display | `digits` | Shows a number: a digit, a bit, a shape's corners, a wave's height rounded | it shows the same numbers |
| Fan | `fan` (or `motor`) | Spins faster for more signal; a wave below its middle spins it backwards | it spins the same way at the same speed |
| Servo | `servo` | Swings its arm from left (nothing, or a wave's bottom) to right (full, or a wave's top) | its arm is in the same place |

Devices compare what they do, not the signal, so a digit display wanting 3 is met by the digit 3 or by a triangle.

### Mixing kinds

Bit, digit and shape blocks read any signal as their own kind, at every step of the loop: a digit is 1 unless it's 0, a shape is 1 when solid, a wave is 1 above its middle; a shape read as a digit is its corners, a wave its height from 0 to 9; a bit, digit or wave read as a shape is a circle, solid when it would read as 1. The M kinds work on any signal; their rules are in [Design.md](Design.md#spoilers-the-rules).

## Block kinds

The player sees only a kind's code (and in an unmarked chapter, not even that). The rules are listed in [Design.md](Design.md#spoilers-the-rules).

| Medium | Kind | Inputs | States |
|---|---|---|---|
| binary | B1 | 1 | 2 |
| binary | B2 | 2 | 2 |
| binary | B3 | 1 | 4 |
| binary | B4 | 2 | 4 |
| decimal | D1 | 1 | 3 |
| decimal | D2 | 2 | 2 |
| decimal | D3 | 1 | 3 |
| decimal | D4 | 2 | 2 |
| shape | S1 | 1 | 2 |
| shape | S2 | 1 | 2 |
| shape | S3 | 2 | 2 |
| shape | S4 | 2 | 2 |
| wave | W1 | 1 | 2 |
| wave | W2 | 1 | 3 |
| wave | W3 | 2 | 2 |
| wave | W4 | 1 | 2 |
| sampled | Q1 | 1 | 2 |
| sampled | Q2 | 1 | 2 |
| sampled | Q3 | 2 | 2 |
| sampled | Q4 | 2 | 2 |
| any | M1, M5, M8 | 2 | 1 |
| any | M2, M3, M4, M6, M7 | 1 | 1 |

## What makes a good circuit

* **The goal must be clear.** Every target is written into the goal sentence and shown on the board. If the sentence gets long, the circuit is probably asking too much at once.
* **It must not be solved at the start.** The validator checks this.
* **Prefer one solution**, especially in a chapter's last circuit. Targets on blocks and locked blocks are the tools for ruling out the routes you didn't mean.
* **Make the player look inside.** If a circuit can be solved by trying every state of every block, give it more blocks and fewer probes, or targets that depend on each other.
* **Introduce one thing at a time.** A new kind should first appear where it is the only unknown, or where the player can change an input that reaches it.
* **Keep the hint about controls.** The circuit itself should teach the rule.

For wired circuits:

* **No input should already do the job.** If wiring an input straight to an output meets its target, the player is told the circuit isn't solved for a reason they can't see. The validator warns about it.
* **Fewer ways to solve it is better**, but a few are fine. The validator and the `WiringTests` count them.
* **Name a block before hiding it.** Every kind an unmarked circuit uses should appear by name earlier. A test checks this.
