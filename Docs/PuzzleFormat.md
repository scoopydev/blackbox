# Puzzle format

Circuits are plain JSON files in `Assets/_Project/Puzzles/Resources/BlackboxPuzzles/`.
`//` comments are allowed. The loader's error messages name the file, the circuit and the problem, and the game shows them on screen if a file can't be read.

After editing, run **BLACKBOX → Validate Puzzles** in Unity, or the EditMode tests. Both solve every circuit by brute force.

## chapters.json

The chapters, in play order, by file name without `.json`:

```json
{ "chapters": ["chapter-1-first-light", "chapter-2-afterglow"] }
```

## A chapter file

```json
{
  "id": "afterglow",
  "numeral": "II",
  "title": "Afterglow",
  "tagline": "Some boxes remember.",
  "unlock": { "after": "first-light", "solved": 6 },
  "circuits": [ ... ]
}
```

| Key | Required | Meaning |
|---|---|---|
| `id` | yes | Stable id. Saves refer to it, so don't rename it once players have it. |
| `title` | yes | Shown on the chapter map and the title card. |
| `numeral` | no | Defaults to the chapter's position in Roman numerals. |
| `tagline` | no | One line under the title. |
| `unlock` | no | The chapter opens once `solved` circuits of chapter `after` are solved. Leave it out for a chapter that is open from the start. |
| `circuits` | yes | The circuits, easiest first. |

## A circuit

```json
{
  "id": "first-light-03",
  "name": "Meeting",
  "hint": "Turn on Probe and hover a box to see what goes in and what comes out.",
  "inputs": { "A": [0, 0], "B": [0, 2] },
  "boxes": {
    "g": { "kind": "K3", "setting": 0, "at": [1, 1] },
    "n": { "kind": "K1", "setting": 1, "at": [2, 1] }
  },
  "outputs": { "X": [3, 1] },
  "wires": ["A -> g.0", "B -> g.1", "g -> n", "n -> X"],
  "table": ["00 > 1", "01 > 1", "10 > 1", "11 > 0"]
}
```

| Key | Required | Meaning |
|---|---|---|
| `id` | yes | Unique across all chapters. Saves refer to it. |
| `name` | no | Shown in the header and on the chapter map. |
| `hint` | no | One line under the board. Use it to teach a control, not to give away a rule. |
| `inputs` | yes | `id: [column, row]`. Input ids are shown as `IN A`, so keep them to one or two letters. |
| `boxes` | no | `id: { kind, setting, locked, at }`. `setting` is where the box starts, counting from 0. `locked: true` draws the box dashed and stops the player changing it. |
| `outputs` | yes | `id: [column, row]`. |
| `wires` | yes | See below. Every box port and every output needs exactly one wire into it. |
| `table` or `trace` | yes, one of them | The test cases. See below. |

### Positions

`[column, row]` places a node on the board grid. Columns run left to right and rows run top to bottom. Halves are fine, such as `[1, 0.5]`. The board scales to fit, so only the relative positions matter.

Positions are optional. If any node has no position, the circuit is laid out automatically: each node goes one column after the furthest node that feeds it. Hand-placed nodes keep their place.

### Wires

```
"A -> g.0"         from A into port 0 (the top port) of g
"g -> n"           a one-input box or an output doesn't need a port
{ "from": "n", "to": "d", "via": 2.6 }
```

A wire that runs backwards, from a later column to an earlier one, is drawn as a loop under (or over) the board. `via` sets the row it runs along. Without it, the wire runs just below the lower of its two ends.

A loop must pass through a delay box (K6). A loop of logic alone has no stable answer, so the circuit won't load.

### Truth tables (`table`)

For circuits without memory. Each row is one test: the input values in the order the inputs are listed, then `>`, then the wanted outputs in order.

```json
"table": ["00 > 01", "01 > 10", "10 > 10", "11 > 01"]
```

Spaces are ignored. A `-` in the outputs means any value is fine.

### Traces (`trace`)

For circuits with memory. One string per input and output, one character per tick. Every lane must be the same length.

```json
"trace": {
  "A": "0110 1110 1100",
  "X": "0010 0110 0100"
}
```

Spaces are ignored, so group ticks in fours for readability. A `-` or `.` in an output lane means any value is fine at that tick, which is useful while a delay is still filling. Use `"traces": [ {...}, {...} ]` to give a circuit several separate runs. Every box resets at the start of each one.

## Box kinds

The player sees only a kind's code. The rules are listed in [Design.md](Design.md#spoilers-the-rules).

| Kind | Inputs | Settings |
|---|---|---|
| K1 | 1 | 2 |
| K2 | 1 | 4 |
| K3 | 2 | 2 |
| K4 | 2 | 4 |
| K5 | 3 | 3 |
| K6 | 1 | 3 |
| K7 | 1 | 3 |
| K8 | 1 | 2 |
| K9 | 2 | 2 |

## What makes a good circuit

* **It must not be solved at the start.** The validator checks this.
* **Prefer one solution.** If there are several, make sure each one is a fair reading of the rules.
* **Make every row count.** A table that can be matched without understanding the box teaches nothing. Add the row that rules out the wrong idea.
* **Introduce one thing at a time.** A new kind should first appear where it is the only unknown.
* **Keep the hint about controls.** The circuit itself should teach the rule.
