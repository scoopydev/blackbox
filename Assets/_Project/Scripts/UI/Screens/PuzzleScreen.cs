using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using Blackbox.UI.Board;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>
    /// One circuit. The goal is written above the board and every target sits
    /// beside the thing it belongs to with a live tick or cross. The player
    /// changes adjustable inputs and unlocked blocks, places probes on wires to
    /// read the signals inside, and keeps notes with the pen. The circuit is
    /// solved the moment every target matches.
    /// </summary>
    public sealed class PuzzleScreen : GameScreen, IBoardHost
    {
        public PuzzleDef Puzzle;

        PuzzleSession _session;
        BoardView _board;
        InkLayer _ink;

        VoidText _title, _record, _status, _moves;
        VoidText _goal, _matchedValue, _matchedLabel;
        PillButton _pen, _eraser, _undo, _reset;
        readonly List<ShapeGraphic> _swatches = new List<ShapeGraphic>();
        RectTransform _probeRow;
        readonly List<ShapeGraphic> _probeDots = new List<ShapeGraphic>();
        CanvasGroup _controls, _solved;
        VoidText _solvedStats;
        PillButton _next, _circuits;

        bool _solvedShown;
        string _hint;
        PuzzleDef _nextPuzzle;

        // Keyboard: the item the arrows change. Hover wins over Tab focus.
        BoardItem _hoverItem;
        int _hoverIndex = -1;
        BoardItem _focusItem;
        int _focusIndex = -1;

        // Layout, in frame coordinates from the top-left.
        const float Left = 90f;
        const float Right = 1830f;
        const float HeaderRule = 122f;
        const float GoalY = 166f;
        const float StatusY = 888f;
        const float FooterRule = 912f;
        const float ToolsY = 962f;
        const float HintsY = 1036f;

        Medium Medium => Puzzle.Medium;

        protected internal override void Build()
        {
            var p = Puzzle;
            Game.Save.Circuits.TryGetValue(p.Id, out var resume);
            _session = new PuzzleSession(p, resume);
            _session.Changed += OnChanged;
            _session.Reset += OnSessionReset;
            _session.ProbesChanged += OnProbesChanged;

            BuildHeader();
            BuildGoal();
            _board = BoardView.Create(Root, _session, this);

            _ink = InkLayer.Create(Root, Game.Save.InkFor(p.Id), new Rect(0f, HeaderRule + 1f, VoidFrame.Width, FooterRule - HeaderRule - 2f));
            _ink.Changed = () => { Game.MarkDirty(); RefreshTools(); };

            _hint = p.Hint ?? "";
            _status = UIKit.Text(Root, _hint, TextStyle.Body.WithSize(19), TextAnchor.MiddleCenter, "Status");
            _status.rectTransform.At(VoidFrame.Width * 0.5f, StatusY, 1500f, 30f);
            UIKit.Rule(Root, Left, Right, FooterRule, Palette.Rule);

            BuildTools();
            BuildControls();
            BuildSolvedPanel();

            RefreshCounters();
            RefreshTools();
            if (_session.Solved) SetSolvedShown(true, quiet: true);
        }

        // ------------------------------------------------------------------ layout

        void BuildHeader()
        {
            var p = Puzzle;
            Chrome.Wordmark(Root, Left, 62f, 22, OpenPause);
            var where = UIKit.Text(Root, $"{p.Chapter.Numeral}  ·  {p.Chapter.Title}  ·  {p.Label}", TextStyle.Label.WithSize(13).WithColor(Palette.Faint));
            where.rectTransform.At(VoidFrame.Width * 0.5f, 44f, 700f, 20f);
            _title = UIKit.Text(Root, p.Name, TextStyle.Heading.WithSize(34).WithWeight(200), TextAnchor.MiddleCenter, "Title");
            _title.rectTransform.At(VoidFrame.Width * 0.5f, 82f, 900f, 46f);

            Chrome.Counter(Root, "MOVES", Right, 62f, out _moves);
            _record = UIKit.Text(Root, "", TextStyle.Label.WithSize(11).WithTracking(0.2f).WithColor(Palette.Faint), TextAnchor.MiddleRight, "Record");
            _record.rectTransform.At(Right - 150f, 92f, 300f, 16f);
            if (Game.Progress.IsSolved(p))
            {
                Game.Save.BestMoves.TryGetValue(p.Id, out int best);
                _record.text = best > 0 ? $"SOLVED BEFORE  ·  BEST {best} MOVES" : "SOLVED BEFORE";
            }
            UIKit.Rule(Root, Left, Right, HeaderRule, Palette.Rule);
        }

        /// <summary>The goal, in words, right under the header: what finished looks like.</summary>
        void BuildGoal()
        {
            var label = UIKit.Text(Root, "GOAL", TextStyle.Label.WithSize(13).WithTracking(0.34f).WithColor(Color.white), TextAnchor.MiddleLeft, "Goal label");
            label.SignalFill = true;
            label.rectTransform.At(Left + 40f, GoalY, 80f, 20f);

            string sentence = GoalText.For(Puzzle);
            _goal = UIKit.Text(Root, sentence, TextStyle.Body.WithSize(sentence.Length > 110 ? 21 : 24).WithColor(Palette.Text), TextAnchor.MiddleLeft, "Goal");
            _goal.horizontalOverflow = HorizontalWrapMode.Wrap;
            _goal.LineHeight = 1.3f;
            _goal.rectTransform.At(Left + 110f + 640f, GoalY, 1280f, 64f);

            _matchedValue = UIKit.Text(Root, "", TextStyle.Value.WithSize(34).WithWeight(200), TextAnchor.MiddleRight, "Matched");
            _matchedValue.rectTransform.At(Right - 100f, GoalY - 8f, 200f, 44f);
            _matchedLabel = UIKit.Text(Root, "TARGETS MET", TextStyle.Label.WithSize(11).WithTracking(0.3f), TextAnchor.MiddleRight, "Matched label");
            _matchedLabel.rectTransform.At(Right - 100f, GoalY + 22f, 200f, 16f);
        }

        void BuildTools()
        {
            _pen = PillButton.Create(Root, "PEN", Left + 66f, ToolsY, 132f, 46f, false, "pen");
            _pen.Click.ClickSound = Sfx.Hover;
            _pen.Click.Clicked = () => SetTool(_ink.Tool == InkTool.Pen ? InkTool.Off : InkTool.Pen);
            _eraser = PillButton.Create(Root, "ERASE", Left + 132f + 14f + 75f, ToolsY, 150f, 46f, false, "eraser");
            _eraser.Click.ClickSound = Sfx.Hover;
            _eraser.Click.Clicked = () => SetTool(_ink.Tool == InkTool.Eraser ? InkTool.Off : InkTool.Eraser);

            for (int i = 0; i < InkLayer.Inks.Length; i++)
            {
                int index = i;
                var rt = UIKit.Rect(Root, "Swatch " + i).At(Left + 346f + i * 36f, ToolsY, 32f, 46f);
                var dot = UIKit.Shape(rt, "Dot", ShapeKind.Circle);
                dot.rectTransform.Centered(0f, 0f, 18f, 18f);
                _swatches.Add(dot);
                UIKit.Hit(rt).rectTransform.Stretch();
                var c = rt.gameObject.AddComponent<Clickable>();
                c.HoverSound = false;
                c.ClickSound = Sfx.Tick;
                c.Clicked = () =>
                {
                    _ink.Colour = index;
                    if (_ink.Tool != InkTool.Pen) SetTool(InkTool.Pen);
                    RefreshTools();
                };
            }

            Chrome.TextButton(Root, "CLEAR NOTES", Left + 464f, ToolsY, false, () =>
            {
                if (_ink.StrokeCount == 0) { Say("There are no notes on this circuit yet. Press D for the pen.", 3f); return; }
                Game.Confirm("Rub out every note on this circuit?", "CLEAR", true, () => { _ink.Clear(); Say("Notes cleared.", 2.5f); });
            });

            Chrome.KeyHints(Root, Left, HintsY, false, "D", "PEN", "E", "ERASE", "RIGHT-DRAG", "RUB OUT");
        }

        /// <summary>How many probes the circuit has, and how many are placed: one dot each, lit when in use.</summary>
        void BuildProbes(Transform parent)
        {
            int n = Puzzle.Probes;
            _probeRow = UIKit.Rect(parent, "Probes").At(VoidFrame.Width * 0.5f, ToolsY, 0f, 0f);
            var label = UIKit.Text(_probeRow, n == 0 ? "NO PROBES ON THIS CIRCUIT" : "PROBES", TextStyle.Label.WithSize(13).WithColor(n == 0 ? Palette.Faint : Palette.Dim), TextAnchor.MiddleLeft, "Label");
            float labelWidth = label.MeasureWidth();
            const float gap = 18f, pitch = 26f, dot = 14f;
            float total = labelWidth + (n > 0 ? gap + (n - 1) * pitch + dot : 0f);
            float x = -total * 0.5f;
            label.rectTransform.Centered(x + labelWidth * 0.5f + 2f, 0f, labelWidth + 4f, 20f);
            x += labelWidth + gap + dot * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var d = UIKit.Shape(_probeRow, "Probe " + i, ShapeKind.Circle);
                d.rectTransform.Centered(x + i * pitch, 0f, dot, dot);
                d.raycastTarget = false;
                _probeDots.Add(d);
            }
        }

        void BuildControls()
        {
            var rt = UIKit.Rect(Root, "Controls").Stretch();
            _controls = rt.Group();
            BuildProbes(rt);
            _undo = PillButton.Create(rt, "UNDO", Right - 75f - 166f, ToolsY, 150f, 46f, false, "undo");
            _undo.Click.ClickSound = Sfx.Hover;
            _undo.Click.Clicked = Undo;
            _reset = PillButton.Create(rt, "RESET", Right - 75f, ToolsY, 150f, 46f, false, "reset");
            _reset.Click.ClickSound = Sfx.Hover;
            _reset.Click.Clicked = AskReset;
            Chrome.KeyHints(rt, Right, HintsY, true, "TAB", "SELECT", "LEFT RIGHT", "CHANGE", "Z", "UNDO", "H", "HELP", "ESC", "MENU");
        }

        void BuildSolvedPanel()
        {
            var rt = UIKit.Rect(Root, "Solved").Stretch();
            _solved = rt.Group();
            _solved.alpha = 0f;
            _solved.blocksRaycasts = false;

            var label = UIKit.Text(rt, "CIRCUIT COMPLETE", TextStyle.Label.WithSize(15).WithTracking(0.34f).WithColor(Color.white), TextAnchor.MiddleRight, "Solved");
            label.SignalFill = true;
            label.rectTransform.At(Right - 420f - 160f, ToolsY - 12f, 320f, 24f);
            _solvedStats = UIKit.Text(rt, "", TextStyle.Label.WithSize(12).WithColor(Palette.Dim), TextAnchor.MiddleRight, "Stats");
            _solvedStats.rectTransform.At(Right - 420f - 160f, ToolsY + 16f, 320f, 18f);

            _circuits = PillButton.Create(rt, "CIRCUITS", Right - 230f - 16f - 75f, ToolsY, 150f, 46f, false);
            _circuits.Click.Clicked = () => Game.GoChapters(Puzzle.Chapter);
            _next = PillButton.Create(rt, "NEXT", Right - 115f, ToolsY, 230f, 46f, true, "arrow");
            _next.Click.Clicked = GoNext;
            Chrome.KeyHints(rt, Right, HintsY, true, "ENTER", "NEXT", "Z", "UNDO", "ESC", "MENU");
        }

        // ------------------------------------------------------------------ showing state

        void RefreshCounters()
        {
            _moves.text = _session.Moves.ToString("00");
            int matched = _session.MatchedTargets, total = Puzzle.Targets.Count;
            _matchedValue.text = $"{matched}/{total}";
            bool all = matched == total;
            _matchedValue.SignalFill = all;
            _matchedValue.color = all ? Color.white : Palette.Text;
            _matchedLabel.color = all ? Palette.Dim : Palette.Faint;
            _undo.Enabled = _session.CanUndo;
            _reset.Enabled = !_session.AtStart;
        }

        void RefreshTools()
        {
            _pen.Active = _ink.Tool == InkTool.Pen;
            _eraser.Active = _ink.Tool == InkTool.Eraser;
            for (int i = 0; i < _swatches.Count; i++)
            {
                bool chosen = i == _ink.Colour;
                var c = InkLayer.Inks[i];
                _swatches[i].Style(Paint.Solid, chosen ? c : c.WithAlpha(0.45f), chosen ? Paint.Solid : Paint.None, Palette.Text, 1.4f);
                _swatches[i].Inset = chosen ? -2f : 2f;
            }
            int used = _session.Probes.Count;
            for (int i = 0; i < _probeDots.Count; i++)
            {
                bool placed = i < used;
                _probeDots[i].Style(placed ? Paint.Signal : Paint.Solid, placed ? Color.white : Color.black, placed ? Paint.None : Paint.Solid, Palette.Dim, 1.4f);
                _probeDots[i].Glow = placed ? 0.4f : 0f;
                _probeDots[i].GlowPaint = Paint.Signal;
                _probeDots[i].GlowRadius = 10f;
            }
        }

        void SetSolvedShown(bool on, bool quiet = false)
        {
            if (_solvedShown == on) return;
            _solvedShown = on;
            _board.SetSolved(on);
            _title.SignalFill = on;
            _solved.blocksRaycasts = on;
            _controls.blocksRaycasts = !on;
            if (on && quiet) FillSolvedPanel(false, null);
            float fromA = _solved.alpha, fromB = _controls.alpha;
            Tweener.Run(this, "solved panel", quiet ? 0.01f : 0.35f, t =>
            {
                _solved.alpha = Mathf.Lerp(fromA, on ? 1f : 0f, t);
                _controls.alpha = Mathf.Lerp(fromB, on ? 0f : 1f, t);
            }, Ease.OutCubic, on && !quiet ? 0.3f : 0f);
        }

        void Say(string message, float seconds = 4.5f)
        {
            _status.text = message;
            _status.color = Palette.Text;
            Tweener.Run(this, "say", 0.6f, t => _status.color = Color.Lerp(Palette.Text, Palette.Dim, t), Ease.Linear, seconds, () =>
            {
                _status.text = _hint;
                _status.color = TextStyle.Body.Color;
            });
        }

        // ------------------------------------------------------------------ session events

        void OnChanged(ChangeKind kind, int index)
        {
            int node = kind == ChangeKind.Input ? index : _session.Circuit.BlockNode(index);
            if (kind == ChangeKind.Input) _board.Inputs[index].SetValue(_session.InputValue(index), true);
            else _board.Blocks[index].SetState(_session.State(index), true);
            int newlyMatched = _board.Refresh(true);
            _board.Pulse(node);
            if (Medium == Medium.Wave && kind == ChangeKind.Input) PlayValue(_session.InputValue(index));

            Game.Save.Circuits[Puzzle.Id] = _session.Snapshot();
            Game.MarkDirty();
            RefreshCounters();

            if (_session.Solved)
            {
                if (!_solvedShown) OnSolved();
            }
            else
            {
                if (_solvedShown) SetSolvedShown(false);
                if (newlyMatched > 0) App.Sound(Sfx.Match, 1f, 0.7f);
            }
        }

        void OnSessionReset()
        {
            _board.Refresh(false);
            for (int i = 0; i < _board.Inputs.Count; i++) _board.Pulse(i);
            Game.Save.Circuits[Puzzle.Id] = _session.Snapshot();
            Game.MarkDirty();
            RefreshCounters();
            if (_solvedShown && !_session.Solved) SetSolvedShown(false);
            Say("Every input and block is back where it started. Probes stay where they are.", 3.5f);
        }

        void OnProbesChanged()
        {
            _board.RebuildProbes();
            _board.Refresh(false);
            Game.Save.Circuits[Puzzle.Id] = _session.Snapshot();
            Game.MarkDirty();
            RefreshTools();
        }

        void OnSolved()
        {
            var p = Puzzle;
            var library = Game.Library;
            bool first = !Game.Progress.IsSolved(p);
            var unlockedBefore = new List<bool>();
            foreach (var c in library.Chapters) unlockedBefore.Add(Game.Progress.IsUnlocked(c));

            Game.Save.BestMoves.TryGetValue(p.Id, out int previousBest);
            Game.Save.RecordSolve(p.Id, _session.Moves);
            Game.Save.Circuits[p.Id] = _session.Snapshot();
            Game.SaveNow();

            ChapterDef opened = null;
            for (int i = 0; i < library.Chapters.Count; i++)
                if (!unlockedBefore[i] && Game.Progress.IsUnlocked(library.Chapters[i])) opened = library.Chapters[i];

            FillSolvedPanel(first, previousBest > 0 ? previousBest : (int?)null);
            _record.text = "";
            SetSolvedShown(true);

            Tweener.After(this, "solved sound", 0.3f, () => App.Sound(Sfx.Solved));
            if (opened != null)
            {
                Tweener.After(this, "unlock", 1.6f, () => App.Sound(Sfx.Unlock));
                Say($"Solved. Chapter {opened.Numeral} is open: {opened.Title}.", 6f);
            }
            else Say(first ? "Solved. Every target is met." : "Solved again.", 3.5f);
        }

        void FillSolvedPanel(bool first, int? previousBest)
        {
            int moves = _session.Moves;
            string stats = moves == 1 ? "1 MOVE" : $"{moves} MOVES";
            if (!first && previousBest.HasValue && moves > 0) stats += moves < previousBest.Value ? "  ·  NEW BEST" : $"  ·  BEST {previousBest.Value}";
            if (moves == 0) stats = "SOLVED";
            _solvedStats.text = stats;
            _nextPuzzle = Game.Progress.NextAfter(Puzzle);
            _next.SetText(_nextPuzzle == null ? "CIRCUITS" : _nextPuzzle.Chapter != Puzzle.Chapter ? "NEXT CHAPTER" : "NEXT CIRCUIT");
            _circuits.gameObject.SetActive(_nextPuzzle != null);
        }

        void GoNext()
        {
            if (_nextPuzzle != null) Game.GoPuzzle(_nextPuzzle);
            else Game.GoChapters(Puzzle.Chapter);
        }

        // ------------------------------------------------------------------ actions

        void Undo()
        {
            if (_session.Undo()) App.Sound(Sfx.Dial, 0.85f);
            else App.Sound(Sfx.Miss, 1.5f, 0.3f);
        }

        void AskReset()
        {
            if (_session.AtStart) return;
            Game.Confirm("Put every input and block back where it started? Probes and notes stay.", "RESET", false, ResetCircuit);
        }

        public void ResetCircuit() => _session.ResetToStart();

        void SetTool(InkTool tool)
        {
            if (_ink.Tool == tool) return;
            _ink.SetTool(tool);
            RefreshTools();
            App.Sound(tool == InkTool.Off ? Sfx.Close : Sfx.Open, 1.5f, 0.5f);
            if (tool == InkTool.Pen) Say("Pen: draw and write anywhere on the circuit. Right-drag rubs out. Press D or Esc to put it down.", 4.5f);
            else if (tool == InkTool.Eraser) Say("Eraser: drag over a note to rub it out. Press E or Esc to put it down.", 4f);
        }

        string NameOfInput(int i) => Puzzle.Inputs[i].Id;
        string NameOfBlock(int b) => Puzzle.Blocks[b].Id;

        // ------------------------------------------------------------------ IBoardHost

        public void InputStep(int input, int direction)
        {
            if (!_session.CanChangeInput(input))
            {
                App.Sound(Sfx.Miss, 1.6f, 0.35f);
                if (Medium == Medium.Wave) PlayValue(_session.InputValue(input));
                Say($"Input {NameOfInput(input)} is fixed. Only inputs with arrows can be changed.", 3.5f);
                return;
            }
            _session.CycleInput(input, direction);
        }

        public void BlockStep(int block, int direction)
        {
            var def = Puzzle.Blocks[block];
            if (def.Locked)
            {
                App.Sound(Sfx.Miss, 1.6f, 0.35f);
                Say($"Block {NameOfBlock(block)} is locked: its state can't be changed. Probe around it to see what it does.", 4f);
                return;
            }
            if (!_session.CanChangeBlock(block)) return;
            _session.CycleState(block, direction);
        }

        public void BlockPip(int block, int state)
        {
            if (_session.CanChangeBlock(block)) _session.SetState(block, state);
        }

        public bool CanPlaceProbe => _session.ProbesLeft > 0 && _ink.Tool == InkTool.Off;

        public void WireClicked(int wire, float at)
        {
            if (Puzzle.Probes == 0)
            {
                Say("This circuit has no probes. Work it out from the inputs and outputs.", 3.5f);
                return;
            }
            if (_session.ProbesLeft <= 0)
            {
                App.Sound(Sfx.Miss, 1.5f, 0.35f);
                Say(Puzzle.Probes == 1 ? "Your probe is in use. Click it to take it off, then place it again." : "Every probe is in use. Click one to take it off, then place it again.", 4f);
                return;
            }
            _session.AddProbe(wire, at);
            App.Sound(Sfx.Open, 1.6f, 0.6f);
            if (_session.Probes.Count == 1 && !Game.Progress.IsSolved(Puzzle) && Puzzle.Number < 4)
                Say("The probe reads the signal on that wire. Click it to take it off.", 3.5f);
        }

        public void ProbeClicked(int probe)
        {
            if (_session.RemoveProbe(probe)) App.Sound(Sfx.Close, 1.4f, 0.6f);
        }

        public void PlayValue(int value)
        {
            if (Medium == Medium.Wave && Media.IsValid(Medium, value)) Game.Audio.PlayWave(value);
        }

        public void Hover(BoardItem item, int index, bool on)
        {
            if (on)
            {
                _hoverItem = item;
                _hoverIndex = index;
            }
            else if (_hoverItem == item && _hoverIndex == index) _hoverIndex = -1;
        }

        // ------------------------------------------------------------------ keys

        /// <summary>The adjustable inputs and unlocked blocks, in the order Tab visits them.</summary>
        List<(BoardItem item, int index)> Changeables()
        {
            var list = new List<(BoardItem, int)>();
            for (int i = 0; i < Puzzle.Inputs.Count; i++) if (_session.CanChangeInput(i)) list.Add((BoardItem.Input, i));
            for (int b = 0; b < Puzzle.Blocks.Count; b++) if (_session.CanChangeBlock(b)) list.Add((BoardItem.Block, b));
            return list;
        }

        void SetFocus(BoardItem item, int index)
        {
            foreach (var v in _board.Inputs) v.Focused = false;
            foreach (var v in _board.Blocks) v.Focused = false;
            _focusItem = item;
            _focusIndex = index;
            if (index < 0) return;
            if (item == BoardItem.Input) _board.Inputs[index].Focused = true;
            else if (item == BoardItem.Block) _board.Blocks[index].Focused = true;
        }

        void FocusNext(int direction)
        {
            var list = Changeables();
            if (list.Count == 0) { Say("Nothing in this circuit can be changed by keyboard.", 3f); return; }
            int at = list.FindIndex(x => x.item == _focusItem && x.index == _focusIndex);
            at = at < 0 ? (direction > 0 ? 0 : list.Count - 1) : ((at + direction) % list.Count + list.Count) % list.Count;
            SetFocus(list[at].item, list[at].index);
            App.Sound(Sfx.Hover);
        }

        void StepKeyboardTarget(int direction)
        {
            BoardItem item;
            int index;
            if (_hoverIndex >= 0 && (_hoverItem == BoardItem.Input || _hoverItem == BoardItem.Block)) { item = _hoverItem; index = _hoverIndex; }
            else if (_focusIndex >= 0) { item = _focusItem; index = _focusIndex; }
            else { Say("Press Tab to choose an input or block, then Left and Right to change it.", 3.5f); return; }
            bool changeable = item == BoardItem.Input ? _session.CanChangeInput(index) : _session.CanChangeBlock(index);
            if (item == BoardItem.Input) InputStep(index, direction);
            else BlockStep(index, direction);
            if (changeable) App.Sound(Sfx.Dial, direction > 0 ? 1.05f : 0.95f);
        }

        public override void HandleInput()
        {
            if (GameInput.Down(Key.Escape))
            {
                if (_ink.Tool != InkTool.Off) SetTool(InkTool.Off);
                else if (_focusIndex >= 0) SetFocus(_focusItem, -1);
                else OpenPause();
                return;
            }
            if (_solvedShown && GameInput.Down(Key.Enter))
            {
                App.Sound(Sfx.Click);
                GoNext();
                return;
            }
            if (GameInput.Down(Key.D)) SetTool(_ink.Tool == InkTool.Pen ? InkTool.Off : InkTool.Pen);
            if (GameInput.Down(Key.E)) SetTool(_ink.Tool == InkTool.Eraser ? InkTool.Off : InkTool.Eraser);
            if (GameInput.Down(Key.Backspace) || GameInput.Down(Key.Z))
            {
                if (_ink.Tool != InkTool.Off)
                {
                    if (_ink.UndoStroke()) App.Sound(Sfx.Close, 1.6f, 0.4f);
                }
                else Undo();
            }
            if (GameInput.Down(Key.Tab)) FocusNext(GameInput.Held(Key.Shift) ? -1 : 1);
            if (GameInput.Down(Key.Right) || GameInput.Down(Key.Up) || GameInput.Down(Key.R)) StepKeyboardTarget(+1);
            else if (GameInput.Down(Key.Left) || GameInput.Down(Key.Down)) StepKeyboardTarget(-1);
            if (GameInput.Down(Key.H)) OpenHelp();
        }

        void OpenPause()
        {
            var pause = Game.Screens.Create<PauseOverlay>();
            pause.Owner = this;
            Game.Screens.Push(pause);
        }

        void OpenHelp()
        {
            var help = Game.Screens.Create<HelpOverlay>();
            help.Chapter = Puzzle.Chapter;
            Game.Screens.Push(help);
        }

        public override void OnUncovered()
        {
            _hoverIndex = -1;
            RefreshCounters();
        }

        public override void OnClosing()
        {
            Tweener.Kill(this);
            if (_ink != null) _ink.SetTool(InkTool.Off);
            if (_session != null)
            {
                Game.Save.Circuits[Puzzle.Id] = _session.Snapshot();
                Game.MarkDirty();
            }
        }
    }
}
