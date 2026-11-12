# BLACKBOX

A 2D puzzle game about signals and circuits, built in Unity.

## Concept

Every puzzle gives you some inputs, some blocks and some outputs, and you **wire them up** so the outputs do what their targets show. Only the outputs count. **You don't know what any of the blocks do**: each keeps a hidden rule, and the only way to learn it is to wire it into something and run it.

Two rules hold everywhere, and the goal line says them: **MATCH ALL OUTPUTS. USE ALL PROVIDED BLOCKS.** Every block must lead to an output, and **every input of a block needs a wire**, or the block sends nothing. Inputs may go unused, so you can try any of them, and a run names whatever breaks a rule. A block's label says what type of signal it sends, B1 a bit (0 or 1), D1 a digit (0 to 9), W1 a wave, but never what it does until you solve its circuit; then each block's rule is written under it.

The core loop is experimentation:

1. **Wire** – drag from a dot to a dot. One signal can feed many wires.
2. **Run** – send the signals through. Only a run shows what each block sends and what each output does, beside its target.
3. **Deduce** – form a theory about each block's rule, and write it on the circuit with the pen or the text tool.
4. **Solve** – wire it so every output matches in one run, keeping both rules.

Every output is a display that shows the signal reaching it (lights, speakers, digit displays, fans and servos are set aside for now). Signals are bits, digits and waves, drawn as waveforms: a bit is a line above or below the middle, a changing digit a stepped line at its height from 0 to 9 with its numbers on it, and a wave itself. A steady digit is just its number. Some change as the loop plays. Every signal can be heard as well as seen.

The game has three parts:

* **Learn** teaches one kind of signal per chapter, with its four blocks. In each chapter the first circuit comes wired (move one wire), the second half wired (finish it), and from the third on you draw every wire.
* **Combine** mixes kinds: every block reads every signal, and three mixing blocks gate, count and compare.
* **Build** is the main game: bigger circuits with every kind of signal, and blocks with no clues at all.

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
| Drag from a dot to a dot, or click one and then the other | Draw a wire. Dots on the right of inputs and blocks send; dots on the left of blocks and outputs take one wire each |
| Drag from a port that has a wire | Pick that wire up and move it |
| Click a wire, or right-click a dot | Take wires off |
| Drag a block by its body | Move it; its wires follow. The circuit remembers where you put it, and RESET in the menu puts every block back |
| RUN, or Space | Run it: the signal flows for a few seconds, every block shows what it sends, and each output shows what reaches it beside its target |
| Click a signal or a target | Hear it |
| Z or Backspace, CLEAR | Undo a wire change, or take every wire off |
| D, E | Pen and eraser, with the colours showing while a pen or text tool is out. Right-drag rubs out while the pen is on |
| T | Text tool: click anywhere and type a note. Enter finishes it, Shift+Enter starts a new line, and dragging a note moves it |
| Enter | Next circuit, once solved |
| H | How it works |
| Esc | Finish typing, put the pen down, drop a wire, or open the menu |

Part One has four chapters: *First Light* (bits, 7 circuits, starting with steady 0s and 1s before bits that switch), *Tally* (steady whole digits, 5), *Ticker* (digits that change as the loop plays, 6) and *Resonance* (waves, 7, including one on which input goes on top). Shapes and their chapter, *Forms*, and stepped waves and theirs, *Grain*, are set aside for now (see the end of Design.md). Part Two has *Patch* (5 circuits), *Junction* (6), where two kinds of signal meet in one block, and *Blend* (8), and Part Three *Unmarked* (7) and *Blackbox* (10), where the blocks with three inputs turn up late: 61 circuits in all. Circuits are named by their chapter and number, such as TALLY 1. Each part opens when the one before it is finished; **DEBUG: OPEN EVERYTHING** in Settings opens them all for testing. See [Docs/Design.md](Docs/Design.md) for how the game works and what each circuit teaches.

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
    Puzzles/Shelved/                     Chapters set aside for now (Grain), which the game never loads
    Fonts/Resources/BlackboxFonts/       Sora (SIL Open Font License)
    Tests/EditMode/        NUnit tests: rules, circuits, every shipped circuit, a full playthrough
    Prefabs/ Scenes/ Art/ Audio/ Materials/
Packages/                  Unity package manifest (generated by Unity)
ProjectSettings/           Unity project settings (generated by Unity)
Docs/                      Design notes and the puzzle format
```

Everything on screen is built in code from procedural meshes, and every sound is synthesised at startup, so the game needs no sprites, prefabs or audio files yet. Third-party assets from the Asset Store go outside `_Project` so they stay separate from our own code and content.

## Making circuits

Circuits are JSON files. [Docs/PuzzleFormat.md](Docs/PuzzleFormat.md) describes the format. After changing one, run **BLACKBOX → Validate Puzzles**. It tries every way to wire each circuit and reports any whose solution doesn't work, that start solved, or where an input wired straight to an output already meets its target. The same checks run in the EditMode tests (**Window → General → Test Runner**). The tests only compile when the **Test Framework** package is installed, so a project without it still plays.

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
