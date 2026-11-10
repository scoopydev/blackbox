# BLACKBOX

A 2D puzzle game about signals and circuits, built in Unity.

## Concept

Every puzzle is a circuit you don't understand yet. The goal is written above it: the values its outputs, and sometimes its blocks and inputs, must end at. Between the inputs and outputs sits a tangled web of blocks, and **you don't know what any of them do**.

The core loop is experimentation:

1. **Change** – switch a block to another state, or change an input that isn't fixed.
2. **Probe** – place one of a few probes on a wire to read the signal inside.
3. **Run** – send the signals through and watch them flow. Only a run shows what comes out, and each output plays its sound as it arrives.
4. **Deduce** – form a theory about each block's hidden rule, and write it on the circuit with the pen or the text tool.
5. **Solve** – use what you've learned to meet every target in one run.

Each chapter uses a different kind of signal: bits, digits, shapes, analog waves and sampled waves, and the first circuit of every chapter is open from the start. Every signal can be heard as well as seen.

That is **Part One: Learn**. Once it's done, the game turns round. In **Part Two: Combine** every circuit starts with no wires: the player gets fixed inputs, fixed blocks and outputs, and **draws the wires** so the outputs do what their targets show. Every block must be used, and every block input must be connected. Outputs are devices now (displays, lights, speakers, digit displays, fans and servos), signals of different kinds mix, and eight new mixing blocks add, flip, delay, sample, gate, count, speed up and compare them. **Part Three: Build** is the main game: bigger circuits with every kind of signal, and blocks that no longer say what kind they are.

There is no tutorial text explaining the parts. Understanding *is* the puzzle.

## Design pillars

- **Learn by doing.** Every rule is discoverable through play; nothing is told to the player.
- **Readable signals.** What a run shows should be easy to read and hear, so cause and effect is clear.
- **Fair mysteries.** Each component behaves consistently, so careful observation always pays off.
- **Rules carry over.** Knowledge from earlier puzzles builds toward later, more complex circuits.

## Playing it

Open the project in Unity and press **Play** in any scene. If the scene has no `BlackboxApp`, the game creates one, so there's nothing to set up. For builds, run **BLACKBOX → Create Main Scene** once. It makes `Assets/_Project/Scenes/Main.unity` and puts it first in Build Settings.

| Control | Does |
|---|---|
| Click a block or an input with arrows, or scroll over it | Change it (clicking a block's dot jumps to that state) |
| Right-click | Change it the other way |
| Click a wire | Place a probe, which reads the wire on every run. Click the probe's dot to take it off |
| RUN, or Space | Run the circuit: the signal flows through for a few seconds, then the outputs show and sound, and the targets are checked |
| Click any value | Hear it: every signal has a sound |
| D, E | Pen and eraser. Right-drag rubs out while the pen is on |
| T | Text tool: click anywhere and type a note. Enter finishes it, Shift+Enter starts a new line, and dragging a note moves it |
| Z or Backspace | Undo (with a note tool on, take away the last note) |
| Tab, then Left and Right | Choose a block or input, then change it, without the mouse |
| Enter | Next circuit, once solved |
| H | How to play |
| Esc | Finish typing, put the pen down, or open the menu |

On a wired circuit (Parts Two and Three):

| Control | Does |
|---|---|
| Drag from a dot to a dot, or click one and then the other | Draw a wire. Dots on the right of inputs and blocks send; dots on the left of blocks and outputs take one wire each |
| Drag from a port that has a wire | Pick that wire up and move it |
| Click a wire, or right-click a dot | Take wires off |
| RUN, or Space | Run it: every block shows what it sends, and each output works its device beside its target |
| Click a signal or a target | Hear it |
| Z or Backspace, CLEAR | Undo a wire change, or take every wire off |

Part One has five chapters of six circuits: *First Light* (bits), *Tally* (digits), *Forms* (shapes), *Resonance* (analog waves) and *Grain* (sampled waves). Part Two has *Patch* (7 circuits) and *Blend* (8), and Part Three *Unmarked* (6) and *Blackbox* (6). Each part opens when the one before it is finished; **DEBUG: OPEN EVERYTHING** in Settings opens them all for testing. See [Docs/Design.md](Docs/Design.md) for how the game works and what each circuit teaches.

## Project layout

```
Assets/
  _Project/                All game-specific content lives here
    Scripts/
      Core/                JSON and the save format (assembly Blackbox.Core, no engine code)
      Components/          The signal media and the block kinds' hidden rules (Blackbox.Core)
      Circuit/             Compiles and evaluates circuits (Blackbox.Core)
      Puzzles/             Loader, auto layout, solvers, goal sentences, progress, play and wiring sessions (Blackbox.Core)
      Game/                BlackboxApp (startup, screens, saving), input, audio, tweens
      UI/Void/             The visual kit: glowing wires, shapes, glyphs, type, buttons
      UI/Board/            The circuit on screen: nodes, targets, wires, probes, pen and typed notes; the wiring board and devices
      UI/Screens/          Title, chapter map, circuit and wiring screens, overlays
      Editor/              The BLACKBOX menu
    Puzzles/Resources/BlackboxPuzzles/   The circuits, as JSON
    Fonts/Resources/BlackboxFonts/       Sora (SIL Open Font License)
    Tests/EditMode/        NUnit tests: rules, circuits, every shipped circuit, a full playthrough
    Prefabs/ Scenes/ Art/ Audio/ Materials/
Packages/                  Unity package manifest (generated by Unity)
ProjectSettings/           Unity project settings (generated by Unity)
Docs/                      Design notes and the puzzle format
```

Everything on screen is built in code from procedural meshes, and every sound is synthesised at startup, so the game needs no sprites, prefabs or audio files yet. Third-party assets from the Asset Store go outside `_Project` so they stay separate from our own code and content.

## Making circuits

Circuits are JSON files. [Docs/PuzzleFormat.md](Docs/PuzzleFormat.md) describes the format. After changing one, run **BLACKBOX → Validate Puzzles**. It solves every circuit by brute force and reports any that can't be solved, are solved at the start, have no targets, or have unwired parts. Wired circuits are checked by trying every way to wire them. The same checks run in the EditMode tests (**Window → General → Test Runner**). The tests only compile when the **Test Framework** package is installed, so a project without it still plays.

**BLACKBOX → Save** has tools for testing: show the save file, erase it, or mark every circuit solved.

## Getting started

### Requirements

- Unity 6 (6000.x LTS) with the 2D template
- [Git LFS](https://git-lfs.com/) – binary assets (images, audio) are stored in LFS. The interface fonts are small and stored as ordinary files, so the game runs even without LFS.

### First-time setup

```bash
git lfs install
git clone https://github.com/scoopydev/blackbox.git
```

Then in Unity Hub: **New project → 2D (URP) template**, and set the location to the cloned `blackbox` folder (or create the project elsewhere and move `Assets/`, `Packages/` and `ProjectSettings/` into the repo). Commit the generated `Packages/` and `ProjectSettings/` folders, and the `.meta` files Unity creates next to the scripts, fonts and circuits.

The game works with either input setting (**Project Settings → Player → Active Input Handling**): the Input System package, the old Input Manager, or both.

### Recommended Unity settings

These are on by default in recent Unity versions, but check them under **Edit → Project Settings → Editor**:

- **Version Control → Mode:** Visible Meta Files
- **Asset Serialization → Mode:** Force Text

### Smart Merge (optional)

To merge scenes and prefabs more cleanly, add Unity's YAML merge tool to your git config:

```ini
[merge "unityyamlmerge"]
    name = Unity SmartMerge
    driver = '<path to Unity>/Editor/Data/Tools/UnityYAMLMerge' merge -p %O %B %A %A
    recursive = binary
```
