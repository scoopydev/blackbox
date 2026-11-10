# Puzzle format

Circuits are plain JSON files in `Assets/_Project/Puzzles/Resources/BlackboxPuzzles/`.
`//` comments are allowed. The loader's error messages name the file, the circuit and the problem, and the game shows them on screen if a file can't be read.

After editing, run **BLACKBOX → Validate Puzzles** in Unity, or the EditMode tests. Both solve every circuit by brute force.

## chapters.json

The chapters, in play order, by file name without `.json`:

```json
{ "chapters": ["chapter-1-first-light", "chapter-2-tally", "chapter-3-forms", "chapter-4-resonance"] }
```

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
| `medium` | yes | The kind of signal: `binary`, `decimal`, `shape` or `wave`. Every circuit in the chapter uses it. |
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
| `wave` | `sine`, `square` or `saw`, then `low`, `mid` or `high` | `"square high"` |

### Positions

`[column, row]` places a node on the board grid. Columns run left to right and rows run top to bottom. Halves are fine, such as `[1, 0.5]`. The board scales to fit, so only the relative positions matter.

Positions are optional. If any node has no position, the circuit is laid out automatically: each node goes one column after the furthest node that feeds it. Hand-placed nodes keep their place.

### Wires

```
"A -> g.0"         from A into port 0 (the top port) of g
"g -> k"           a one-input block or an output doesn't need a port
```

One source can feed any number of wires. Signals flow one way, from inputs to outputs, so a loop is rejected when the circuit loads. Wires that skip columns are routed along a free lane between rows, so they never pass behind a block.

## Block kinds

The player sees only a kind's code. The rules are listed in [Design.md](Design.md#spoilers-the-rules).

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
| wave | W4 | 2 | 2 |

## What makes a good circuit

* **The goal must be clear.** Every target is written into the goal sentence and shown on the board. If the sentence gets long, the circuit is probably asking too much at once.
* **It must not be solved at the start.** The validator checks this.
* **Prefer one solution**, especially in a chapter's last circuit. Targets on blocks and locked blocks are the tools for ruling out the routes you didn't mean.
* **Make the player look inside.** If a circuit can be solved by trying every state of every block, give it more blocks and fewer probes, or targets that depend on each other.
* **Introduce one thing at a time.** A new kind should first appear where it is the only unknown, or where the player can change an input that reaches it.
* **Keep the hint about controls.** The circuit itself should teach the rule.
