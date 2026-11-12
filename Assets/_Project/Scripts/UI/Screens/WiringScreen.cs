using System;
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
    /// A circuit the player wires. The inputs, blocks and output devices are laid
    /// out with no wires between them; the player draws the wires. Every output
    /// shows its target beside it from the start. RUN sends the signals through:
    /// each block shows what it sends (or why it sends nothing), then each output
    /// does what reaches it. A run solves the circuit when every output does what
    /// its target shows, every block leads to an output, and every block input
    /// has a wire.
    /// </summary>
    public sealed class WiringScreen : GameScreen, IWiringHost
    {
        public PuzzleDef Puzzle;

        WiringSession _session;
        WiringBoard _board;
        InkLayer _ink;

        VoidText _title, _record, _status, _moves;
        VoidText _matchedValue, _matchedLabel;
        PillButton _pen, _eraser, _textTool, _undo, _clear;
        Clickable _clearNotes;
        readonly List<ShapeGraphic> _swatches = new List<ShapeGraphic>();
        CanvasGroup _controls, _solved;
        VoidText _solvedStats, _solvedLabel;
        PillButton _next, _circuits;

        bool _solvedShown;
        string _hint;
        PuzzleDef _nextPuzzle;

        PillButton _run;
        readonly List<(float at, Action act)> _runPlan = new List<(float, Action)>();
        float _runClock;
        bool _running;

        const float Left = 90f;
        const float Right = 1830f;
        const float HeaderRule = 122f;
        const float GoalY = 166f;
        const float StatusY = 888f;
        const float FooterRule = 912f;
        const float ToolsY = 962f;
        /// <summary>The pen colours sit in a row under the PEN (or TEXT) button while it's on.</summary>
        const float SwatchY = 1012f;
        const float SwatchPitch = 34f;
        const float HintsY = 1036f;

        protected internal override void Build()
        {
            var p = Puzzle;
            Game.Save.Circuits.TryGetValue(p.Id, out var resume);
            _session = new WiringSession(p, resume?.Wires);
            _session.Changed += OnChanged;

            BuildHeader();
            BuildGoal();
            Dictionary<string, Vector2> places = null;
            if (resume != null && resume.Places.Count > 0)
            {
                places = new Dictionary<string, Vector2>();
                foreach (var kv in resume.Places) places[kv.Key] = new Vector2(kv.Value.x, kv.Value.y);
            }
            _board = WiringBoard.Create(Root, _session, this, places);
            _board.SetShowRules(RulesShown);

            _ink = InkLayer.Create(Root, Game.Save.InkFor(p.Id), Game.Save.NotesFor(p.Id), new Rect(0f, HeaderRule + 1f, VoidFrame.Width, FooterRule - HeaderRule - 2f));
            _ink.Changed = () => { Game.MarkDirty(); RefreshTools(); };
            _ink.TypingChanged = typing =>
            {
                if (typing) Say("Typing a note. Enter finishes it, Shift+Enter starts a new line.", 600f);
                else if (_ink.Tool == InkTool.Text) Say("Click anywhere to start another note, or drag a note to move it.", 3f);
                else Hush();
            };

            _hint = p.Hint ?? "";
            if (string.IsNullOrEmpty(_hint) && _session.AtStart) _hint = "Drag from a dot to a dot to run a wire. Click a wire to take it off.";
            _status = UIKit.Text(Root, _hint, TextStyle.Body.WithSize(19), TextAnchor.MiddleCenter, "Status");
            _status.horizontalOverflow = HorizontalWrapMode.Wrap;
            _status.rectTransform.At(VoidFrame.Width * 0.5f, StatusY, 1600f, 30f);
            UIKit.Rule(Root, Left, Right, FooterRule, Palette.Rule);

            BuildTools();
            BuildControls();
            BuildSolvedPanel();

            // A circuit solved before, and left as it was, opens already run, so it shows as solved.
            if (Game.Progress.IsSolved(p) && _session.Check().Solved)
            {
                _session.Run();
                _ranWith = _session.Drivers;
                RememberMet(_session.LastRun);
                _board.ShowAll(_session.LastRun);
                SetSolvedShown(true, quiet: true);
            }
            RefreshCounters();
            RefreshTools();
        }

        // ------------------------------------------------------------------ layout

        void BuildHeader()
        {
            var p = Puzzle;
            var chapter = p.Chapter;

            // Left: the way back to this chapter, and its circuits as a row of dots.
            Chrome.TextButton(Root, $"{chapter.Numeral}  ·  {chapter.Title}", Left - 12f, 48f, false, LeaveToChapter, "back");
            BuildSteps(Left, 92f);

            // Centre: which circuit this is, and its name.
            var where = UIKit.Text(Root, $"CIRCUIT {p.Label}  OF  {chapter.Puzzles.Count:00}", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            where.rectTransform.At(VoidFrame.Width * 0.5f, 44f, 700f, 20f);
            _title = UIKit.Text(Root, p.Name, TextStyle.Heading.WithSize(34).WithWeight(200), TextAnchor.MiddleCenter, "Title");
            _title.rectTransform.At(VoidFrame.Width * 0.5f, 82f, 900f, 46f);

            // Right: help and the menu, as buttons that also show their keys; moves under them.
            Chrome.KeyButton(Root, "ESC", "MENU", Right, 48f, true, () => { App.Sound(Sfx.Open, 1.2f, 0.5f); OpenPause(); }, out float menuWidth);
            Chrome.KeyButton(Root, "H", "HELP", Right - menuWidth - 48f, 48f, true, OpenHelp);

            _moves = UIKit.Text(Root, "", TextStyle.Label.WithSize(11).WithTracking(0.3f).WithColor(Palette.Dim), TextAnchor.MiddleRight, "Moves");
            _moves.rectTransform.At(Right - 150f - 12f, 92f, 300f, 16f);
            _record = UIKit.Text(Root, "", TextStyle.Label.WithSize(11).WithTracking(0.2f).WithColor(Palette.Faint), TextAnchor.MiddleRight, "Record");
            _record.rectTransform.At(Right - 150f - 12f - 130f, 92f, 300f, 16f);
            if (Game.Progress.IsSolved(p))
            {
                Game.Save.BestMoves.TryGetValue(p.Id, out int best);
                _record.text = best > 0 ? $"SOLVED BEFORE  ·  BEST {best}" : "SOLVED BEFORE";
            }
            UIKit.Rule(Root, Left, Right, HeaderRule, Palette.Rule);
        }

        /// <summary>
        /// One dot for each circuit in the chapter: lit once solved, a ring for
        /// this one. Clicking an open circuit's dot goes straight to it.
        /// </summary>
        void BuildSteps(float left, float y)
        {
            var chapter = Puzzle.Chapter;
            const float pitch = 22f;
            for (int i = 0; i < chapter.Puzzles.Count; i++)
            {
                var other = chapter.Puzzles[i];
                bool here = other == Puzzle;
                bool solved = Game.Progress.IsSolved(other);
                bool open = Game.Progress.IsUnlocked(other);
                var rt = UIKit.Rect(Root, "Step " + other.Label).At(left + 6f + i * pitch, y, pitch, 26f);
                var dot = UIKit.Shape(rt, "Dot", ShapeKind.Circle);
                float size = here ? 12f : 8f;
                dot.rectTransform.Centered(0f, 0f, size, size);
                if (here) dot.Style(solved ? Paint.Signal : Paint.Solid, solved ? Color.white : Color.black, Paint.Signal, Color.white, 1.6f);
                else if (solved) dot.Style(Paint.Signal, Color.white, Paint.None, Color.clear);
                else dot.Style(Paint.None, Color.clear, Paint.Solid, open ? Palette.Dim : Palette.Outline, 1.2f);
                dot.Glow = here || solved ? 0.35f : 0f;
                dot.GlowPaint = Paint.Signal;
                dot.GlowRadius = 8f;
                if (here || !open) continue;
                UIKit.Hit(rt).rectTransform.Stretch();
                var c = rt.gameObject.AddComponent<Clickable>();
                c.HoverSound = false;
                c.HoverChanged = on =>
                {
                    dot.rectTransform.Centered(0f, 0f, on ? 11f : 8f, on ? 11f : 8f);
                    if (on) Say($"Circuit {other.Label}: {other.Name}{(solved ? ", solved" : "")}. Click to go to it.", 2.5f);
                };
                c.Clicked = () => Game.GoPuzzle(other);
            }
        }

        void LeaveToChapter()
        {
            App.Sound(Sfx.Close);
            Game.GoChapters(Puzzle.Chapter);
        }

        void BuildGoal()
        {
            // The goal is the same for every circuit: the targets beside the outputs say the rest.
            var label = UIKit.Text(Root, "GOAL", TextStyle.Label.WithSize(13).WithTracking(0.34f).WithColor(Color.white), TextAnchor.MiddleLeft, "Goal label");
            label.SignalFill = true;
            label.rectTransform.At(Left + 40f, GoalY, 80f, 20f);
            var goal = UIKit.Text(Root, GoalText.WiringRules, TextStyle.Body.WithSize(22).WithColor(Palette.Text), TextAnchor.MiddleLeft, "Goal");
            goal.rectTransform.At(Left + 110f + 640f, GoalY, 1280f, 30f);

            _matchedValue = UIKit.Text(Root, "", TextStyle.Value.WithSize(34).WithWeight(200), TextAnchor.MiddleRight, "Matched");
            _matchedValue.rectTransform.At(Right - 100f, GoalY - 8f, 200f, 44f);
            _matchedLabel = UIKit.Text(Root, "OUTPUTS MET", TextStyle.Label.WithSize(11).WithTracking(0.3f), TextAnchor.MiddleRight, "Matched label");
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
            _textTool = PillButton.Create(Root, "TEXT", Left + 132f + 14f + 150f + 14f + 66f, ToolsY, 132f, 46f, false, "text");
            _textTool.Click.ClickSound = Sfx.Hover;
            _textTool.Click.Clicked = () => SetTool(_ink.Tool == InkTool.Text ? InkTool.Off : InkTool.Text);

            for (int i = 0; i < InkLayer.Inks.Length; i++)
            {
                int index = i;
                var rt = UIKit.Rect(Root, "Swatch " + i).At(Left + 66f + (i - (InkLayer.Inks.Length - 1) * 0.5f) * SwatchPitch, SwatchY, SwatchPitch, 30f);
                var dot = UIKit.Shape(rt, "Dot", ShapeKind.Circle);
                dot.rectTransform.Centered(0f, 0f, 16f, 16f);
                _swatches.Add(dot);
                UIKit.Hit(rt).rectTransform.Stretch();
                var c = rt.gameObject.AddComponent<Clickable>();
                c.HoverSound = false;
                c.ClickSound = Sfx.Tick;
                c.Clicked = () =>
                {
                    _ink.Colour = index;
                    if (_ink.Tool != InkTool.Pen && _ink.Tool != InkTool.Text) SetTool(InkTool.Pen);
                    RefreshTools();
                };
            }

            _clearNotes = Chrome.TextButton(Root, "CLEAR NOTES", Left + 492f, ToolsY, false, () =>
            {
                _ink.FinishTyping();
                if (_ink.ItemCount == 0) { Say("There are no notes on this circuit yet. Press D for the pen, or T to type.", 3f); return; }
                Game.Confirm("Rub out every note on this circuit?", "CLEAR", true, () => { _ink.Clear(); Say("Notes cleared.", 2.5f); });
            });

        }

        void BuildControls()
        {
            var rt = UIKit.Rect(Root, "Controls").Stretch();
            _controls = rt.Group();
            // Bottom right, right to left: RUN, then CLEAR and UNDO. Each is as wide as its label.
            _run = PillButton.Create(rt, "RUNNING", Right - 100f, ToolsY, 200f, 50f, true, "play");
            _run.Click.ClickSound = Sfx.Hover;
            _run.Click.Clicked = StartRun;
            float x = Fit(_run, Right, ToolsY, 50f, 200f) - 18f;
            _run.SetText("RUN");
            _clear = PillButton.Create(rt, "CLEAR", 0f, ToolsY, 150f, 46f, false, "reset");
            _clear.Click.ClickSound = Sfx.Hover;
            _clear.Click.Clicked = AskClear;
            x = Fit(_clear, x, ToolsY) - 14f;
            _undo = PillButton.Create(rt, "UNDO", 0f, ToolsY, 150f, 46f, false, "undo");
            _undo.Click.ClickSound = Sfx.Hover;
            _undo.Click.Clicked = Undo;
            Fit(_undo, x, ToolsY);
            Chrome.KeyHints(rt, Right, HintsY, true, "SPACE", "RUN", "Z", "UNDO", "D", "PEN", "T", "TEXT");
        }

        void BuildSolvedPanel()
        {
            var rt = UIKit.Rect(Root, "Solved").Stretch();
            _solved = rt.Group();
            _solved.alpha = 0f;
            _solved.blocksRaycasts = false;

            _solvedLabel = UIKit.Text(rt, "CIRCUIT COMPLETE", TextStyle.Label.WithSize(15).WithTracking(0.34f).WithColor(Color.white), TextAnchor.MiddleRight, "Solved");
            _solvedLabel.SignalFill = true;
            _solvedStats = UIKit.Text(rt, "", TextStyle.Label.WithSize(12).WithColor(Palette.Dim), TextAnchor.MiddleRight, "Stats");

            _circuits = PillButton.Create(rt, "ALL CIRCUITS", 0f, ToolsY, 150f, 46f, false);
            _circuits.Click.Clicked = () => Game.GoChapters(Puzzle.Chapter);
            _next = PillButton.Create(rt, "NEXT", 0f, ToolsY, 230f, 50f, true, "arrow");
            _next.Click.Clicked = GoNext;
            LayOutSolved();
            Chrome.KeyHints(rt, Right, HintsY, true, "ENTER", "NEXT", "Z", "UNDO", "ESC", "MENU");
        }

        /// <summary>
        /// Sizes a pill to its label and puts its right edge at <paramref name="right"/>.
        /// Returns its left edge, for the next button along.
        /// </summary>
        static float Fit(PillButton b, float right, float y, float h = 46f, float min = 120f)
        {
            float w = Mathf.Max(min, b.Label.MeasureWidth() + (b.Icon != null ? 34f : 0f) + 60f);
            ((RectTransform)b.transform).At(right - w * 0.5f, y, w, h);
            if (b.Icon != null) b.SetText(b.Label.text);
            return right - w;
        }

        /// <summary>NEXT at the bottom right, ALL CIRCUITS beside it, and the solved message left of both.</summary>
        void LayOutSolved()
        {
            float x = Fit(_next, Right, ToolsY, 50f, 200f) - 14f;
            if (_circuits.gameObject.activeSelf) x = Fit(_circuits, x, ToolsY) - 28f;
            else x -= 14f;
            _solvedLabel.rectTransform.At(x - 200f, ToolsY - 12f, 400f, 24f);
            _solvedStats.rectTransform.At(x - 200f, ToolsY + 16f, 400f, 18f);
        }

        // ------------------------------------------------------------------ showing state

        void RefreshCounters()
        {
            _moves.text = _session.Moves == 1 ? "1 MOVE" : $"{_session.Moves} MOVES";
            int total = Puzzle.Outputs.Count;
            if (_session.HasRun && !_running)
            {
                int matched = _session.LastRun.Met;
                bool all = _session.RunSolved;
                _matchedValue.text = $"{matched}/{total}";
                _matchedValue.SignalFill = all;
                _matchedValue.color = all ? Color.white : Palette.Text;
                _matchedLabel.text = "OUTPUTS MET";
                _matchedLabel.color = all ? Palette.Dim : Palette.Faint;
            }
            else
            {
                _matchedValue.text = $"?/{total}";
                _matchedValue.SignalFill = false;
                _matchedValue.color = Palette.Dim;
                _matchedLabel.text = _running ? "CHECKING" : "RUN TO CHECK";
                _matchedLabel.color = Palette.Faint;
            }
            _undo.Enabled = _session.CanUndo;
            _clear.Enabled = !_session.AtStart;
        }

        void RefreshTools()
        {
            _pen.Active = _ink.Tool == InkTool.Pen;
            _eraser.Active = _ink.Tool == InkTool.Eraser;
            _textTool.Active = _ink.Tool == InkTool.Text;
            // The colours only matter while drawing or typing, and clearing only once there are notes.
            bool inking = _ink.Tool == InkTool.Pen || _ink.Tool == InkTool.Text;
            if (_clearNotes != null) _clearNotes.gameObject.SetActive(_ink.ItemCount > 0);
            // Under whichever of PEN and TEXT is on.
            float under = _ink.Tool == InkTool.Text ? ((RectTransform)_textTool.transform).anchoredPosition.x : ((RectTransform)_pen.transform).anchoredPosition.x;
            for (int i = 0; i < _swatches.Count; i++)
            {
                var holder = (RectTransform)_swatches[i].transform.parent;
                holder.gameObject.SetActive(inking);
                holder.At(under + (i - (_swatches.Count - 1) * 0.5f) * SwatchPitch, SwatchY, SwatchPitch, 30f);
                bool chosen = i == _ink.Colour;
                var c = InkLayer.Inks[i];
                _swatches[i].Style(Paint.Solid, chosen ? c : c.WithAlpha(0.45f), chosen ? Paint.Solid : Paint.None, Palette.Text, 1.4f);
                _swatches[i].Inset = chosen ? -2f : 2f;
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
            _run.Enabled = !on;
            if (on && quiet) FillSolvedPanel(false, null);
            float fromA = _solved.alpha, fromB = _controls.alpha;
            Tweener.Run(this, "solved panel", quiet ? 0.01f : 0.35f, t =>
            {
                _solved.alpha = Mathf.Lerp(fromA, on ? 1f : 0f, t);
                _controls.alpha = Mathf.Lerp(fromB, on ? 0f : 1f, t);
            }, Ease.OutCubic, on && !quiet ? 0.3f : 0f);
        }

        void Hush()
        {
            Tweener.Kill(this, "say");
            _status.text = _hint;
            _status.color = TextStyle.Body.Color;
        }

        public void Say(string message, float seconds = 4.5f)
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

        void OnChanged()
        {
            CancelRun(conceal: false);
            _board.RebuildLinks();
            ConcealChanged();
            SaveWires();
            RefreshCounters();
            if (_solvedShown) SetSolvedShown(false);
            if (_session.Runs == 0 && !Game.Progress.IsSolved(Puzzle) && _session.WireCount >= 2)
                Say("Press RUN, or Space, to send the signals through and see what comes out.", 4f);
        }

        /// <summary>The wiring the signals on show came from: the last run's, or null before any.</summary>
        int[][] _ranWith;

        /// <summary>
        /// After a wiring change, each block keeps the signal its last run showed
        /// until the next run, unless its own inputs were rewired or a block feeding
        /// it changed. Rewiring where a block's signal goes leaves it showing. An
        /// output clears too, since what reaches it is checked only by a run, unless
        /// it was solved and nothing feeding it changed (see <see cref="HeldOutputs"/>).
        /// </summary>
        void ConcealChanged()
        {
            if (_ranWith == null) { _board.Conceal(); return; }
            _board.ConcealChanged(WiringSession.BlocksUnchanged(Puzzle, _ranWith, _session.Drivers), _session.Check(), HeldOutputs());
        }

        /// <summary>The wiring of the last run that finished, and which outputs it found doing what their targets show.</summary>
        int[][] _metWith;
        bool[] _metOutputs;
        /// <summary>The outputs the run in progress leaves as they were.</summary>
        bool[] _held;

        void RememberMet(WiringReport report)
        {
            _metWith = _session.Drivers;
            _metOutputs = (bool[])report.OutputMet.Clone();
        }

        /// <summary>
        /// The outputs that stay solved: each did what its target shows on the last
        /// run that finished, and nothing feeding it has been rewired since, so it
        /// still gets the same signal. A run leaves them showing as they are and
        /// doesn't play them again. Null before a run has finished.
        /// </summary>
        bool[] HeldOutputs()
        {
            if (_metWith == null) return null;
            var held = WiringSession.OutputsUnchanged(Puzzle, _metWith, _session.Drivers);
            for (int o = 0; o < held.Length; o++) held[o] &= _metOutputs[o];
            return held;
        }

        public void BlocksMoved() => SavePlaces();

        void SavePlaces()
        {
            if (!Game.Save.Circuits.TryGetValue(Puzzle.Id, out var state)) Game.Save.Circuits[Puzzle.Id] = state = new CircuitState();
            state.Places.Clear();
            foreach (var kv in _board.Places()) state.Places[kv.Key] = (Mathf.RoundToInt(kv.Value.x), Mathf.RoundToInt(kv.Value.y));
            Game.MarkDirty();
        }

        void SaveWires()
        {
            if (!Game.Save.Circuits.TryGetValue(Puzzle.Id, out var state)) Game.Save.Circuits[Puzzle.Id] = state = new CircuitState();
            state.Wires.Clear();
            state.Wires.AddRange(_session.Snapshot());
            Game.MarkDirty();
        }

        void OnSolved()
        {
            var p = Puzzle;
            var library = Game.Library;
            bool first = !Game.Progress.IsSolved(p);
            var chaptersBefore = new List<bool>();
            foreach (var c in library.Chapters) chaptersBefore.Add(Game.Progress.IsUnlocked(c));
            var partsBefore = new List<bool>();
            foreach (var part in library.Parts) partsBefore.Add(Game.Progress.IsUnlocked(part));

            Game.Save.BestMoves.TryGetValue(p.Id, out int previousBest);
            Game.Save.RecordSolve(p.Id, _session.Moves);
            SaveWires();
            Game.SaveNow();
            _board.SetShowRules(true);

            PartDef openedPart = null;
            for (int i = 0; i < library.Parts.Count; i++)
                if (!partsBefore[i] && Game.Progress.IsUnlocked(library.Parts[i])) openedPart = library.Parts[i];
            ChapterDef opened = null;
            for (int i = 0; i < library.Chapters.Count; i++)
                if (!chaptersBefore[i] && Game.Progress.IsUnlocked(library.Chapters[i])) opened = library.Chapters[i];

            FillSolvedPanel(first, previousBest > 0 ? previousBest : (int?)null);
            _record.text = "";
            SetSolvedShown(true);

            Tweener.After(this, "solved sound", 0.3f, () => App.Sound(Sfx.Solved));
            if (openedPart != null || opened != null)
            {
                Tweener.After(this, "unlock", 1.6f, () => App.Sound(Sfx.Unlock));
                if (openedPart != null) Say($"Solved. {openedPart.Title.ToUpperInvariant()} is open: {openedPart.Tagline}", 7f);
                else Say($"Solved. Chapter {opened.Numeral} is open: {opened.Title}.", 6f);
            }
            else Say(first ? "Solved. Each block's rule is now written under it." : "Solved again.", first ? 5f : 3.5f);
        }

        void FillSolvedPanel(bool first, int? previousBest)
        {
            int moves = _session.Moves, runs = _session.Runs;
            string stats = (moves == 1 ? "1 MOVE" : $"{moves} MOVES") + (runs == 1 ? "  ·  1 RUN" : $"  ·  {runs} RUNS");
            if (!first && previousBest.HasValue && moves > 0) stats += moves < previousBest.Value ? "  ·  NEW BEST" : $"  ·  BEST {previousBest.Value}";
            if (moves == 0) stats = "SOLVED";
            _solvedStats.text = stats;
            _nextPuzzle = Game.Progress.NextAfter(Puzzle);
            _next.SetText(_nextPuzzle == null ? "CIRCUITS" : _nextPuzzle.Chapter != Puzzle.Chapter ? "NEXT CHAPTER" : "NEXT CIRCUIT");
            _circuits.gameObject.SetActive(_nextPuzzle != null);
            LayOutSolved();
        }

        void GoNext()
        {
            if (_nextPuzzle != null) Game.GoPuzzle(_nextPuzzle);
            else Game.GoChapters(Puzzle.Chapter);
        }

        // ------------------------------------------------------------------ actions

        void Undo()
        {
            if (_board.CancelWire()) return;
            if (_session.Undo()) App.Sound(Sfx.Dial, 0.85f);
            else App.Sound(Sfx.Miss, 1.5f, 0.3f);
        }

        void AskClear()
        {
            if (_session.AtStart) return;
            Game.Confirm("Take every wire off this circuit? Notes stay.", "CLEAR", false, ClearWires);
        }

        public void RestartWires()
        {
            if (_session.Restart())
            {
                App.Sound(Sfx.Close);
                Say("The wires are back as the circuit began. Undo puts yours back.", 3f);
            }
        }

        void PutBlocksBack()
        {
            // The blocks glide home, so the save forgets every place now.
            if (!_board.PutBlocksBack()) return;
            if (Game.Save.Circuits.TryGetValue(Puzzle.Id, out var state)) state.Places.Clear();
            Game.MarkDirty();
        }

        public void ClearWires()
        {
            if (_session.Clear())
            {
                App.Sound(Sfx.Close);
                Say("Every wire is off. Undo puts them back.", 3f);
            }
        }

        void SetTool(InkTool tool)
        {
            if (_ink.Tool == tool) return;
            _board.CancelWire();
            _ink.SetTool(tool);
            RefreshTools();
            App.Sound(tool == InkTool.Off ? Sfx.Close : Sfx.Open, 1.5f, 0.5f);
            if (tool == InkTool.Pen) Say("Pen: draw and write anywhere on the circuit. Right-drag rubs out. Press D or Esc to put it down.", 4.5f);
            else if (tool == InkTool.Eraser) Say("Eraser: drag over a note to rub it out. Press E or Esc to put it down.", 4f);
            else if (tool == InkTool.Text) Say("Text: click anywhere on the circuit and type. Drag a note to move it. Press T or Esc to put it down.", 5f);
        }

        // ------------------------------------------------------------------ running

        /// <summary>
        /// Sends the signals through. Light leaves the inputs and flows wire by
        /// wire; each block shows what it sends as the light reaches it; then the
        /// outputs show what arrived, top to bottom. Only then are the targets and
        /// the rules checked.
        /// </summary>
        void StartRun()
        {
            if (_running || _solvedShown) return;
            _board.CancelWire();
            _ink.FinishTyping();
            App.Audio?.StopSignals();
            if (_session.WireCount == 0)
            {
                App.Sound(Sfx.Miss, 1.4f, 0.35f);
                Say("Nothing is wired yet. Drag from the dot on an input to the dot on a block or an output.", 4f);
                return;
            }
            _runPlan.Clear();
            _runClock = 0f;
            _running = true;
            _run.SetText("RUNNING");
            _run.Active = true;
            // Outputs already solved, with nothing feeding them changed, stay solved and quiet.
            var held = _held = HeldOutputs();
            _board.Conceal(held);
            _ranWith = _session.Drivers;
            RefreshCounters();
            Say("Running the circuit.", 30f);

            var report = _session.Check();
            if (held != null)
                for (int o = 0; o < held.Length; o++)
                    if (held[o] && !_board.Outputs[o].Showing) _board.ShowOutput(o, report);
            var depth = _board.Depths();
            int max = 1;
            for (int n = _session.InputCount; n < _session.NodeCount; n++) max = Mathf.Max(max, depth[n]);
            float flow = Mathf.Clamp(max * 0.45f, 1.0f, 2.6f);
            float step = flow / max;
            bool sounds = Game.Save.Prefs.SignalSounds;

            Plan(0f, () => App.Sound(Sfx.Run, 1f, 0.7f));
            foreach (var link in _board.Links)
            {
                int from = depth[link.From], to = depth[link.To];
                float length = Mathf.Max(1, to - from) * step;
                var l = link;
                Plan(from * step, () => { if (!App.ReduceMotion) l.Pulse(0f, length); });
            }
            var levels = new HashSet<int>();
            for (int b = 0; b < _board.Blocks.Count; b++)
            {
                int at = depth[_session.BlockNode(b)];
                levels.Add(at);
                int index = b;
                Plan(at * step, () =>
                {
                    _board.Blocks[index].Flash();
                    _board.ShowBlock(index, report);
                });
            }
            foreach (int level in levels)
            {
                int d = level;
                Plan(d * step, () => App.Sound(Sfx.Tick, 0.85f + 0.07f * d, 0.35f));
            }

            var order = new List<int>();
            for (int o = 0; o < _board.Outputs.Count; o++) order.Add(o);
            order.Sort((a, b) => _board.Outputs[b].Centre.y.CompareTo(_board.Outputs[a].Centre.y));
            float t = flow + 0.25f;
            foreach (int o in order)
            {
                if (held != null && held[o]) continue;
                int index = o;
                var got = report.Values[_session.OutputNode(o)];
                var device = Puzzle.Outputs[o].Device;
                bool audible = sounds && got != null && (device == Device.Speaker || device == Device.Display);
                Plan(t, () =>
                {
                    _board.ShowOutput(index, report);
                    if (audible) App.Audio?.PlayTrace(got);
                    else App.Sound(got == null ? Sfx.Miss : Sfx.Arrive, got == null ? 1.4f : 1f, 0.45f);
                });
                t += audible ? SignalSound.Duration(got) + 0.16f : 0.45f;
            }
            Plan(t + 0.1f, FinishRun);
        }

        void Plan(float at, Action act)
        {
            int i = _runPlan.Count;
            while (i > 0 && _runPlan[i - 1].at > at) i--;
            _runPlan.Insert(i, (at, act));
        }

        void Update()
        {
            if (!_running) return;
            _runClock += Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            while (_running && _runPlan.Count > 0 && _runPlan[0].at <= _runClock)
            {
                var next = _runPlan[0];
                _runPlan.RemoveAt(0);
                next.act();
            }
        }

        void FinishRun()
        {
            _running = false;
            _runPlan.Clear();
            RestoreRunButton();
            bool solved = _session.Run();
            var report = _session.LastRun;
            _board.ShowAll(report, _held);
            RememberMet(report);
            RefreshCounters();
            if (solved)
            {
                if (!_solvedShown) OnSolved();
                return;
            }
            App.Sound(report.Met > 0 ? Sfx.Match : Sfx.Miss, report.Met > 0 ? 0.9f : 1.2f, 0.45f);
            // No verdict line: the board marks unused blocks and empty inputs itself.
            Hush();
        }

        void CancelRun(bool conceal = true)
        {
            if (!_running) return;
            _running = false;
            _runPlan.Clear();
            App.Audio?.StopSignals();
            RestoreRunButton();
            if (conceal) _board.Conceal();
            RefreshCounters();
            Hush();
        }

        void RestoreRunButton()
        {
            _run.SetText("RUN");
            _run.Active = false;
        }

        // ------------------------------------------------------------------ IWiringHost

        public bool CanWire => !_running && _ink.Tool == InkTool.Off && !_ink.Typing;

        public void PlayTrace(Trace trace)
        {
            if (trace == null) return;
            App.Audio?.PlayTrace(trace);
        }

        // ------------------------------------------------------------------ keys

        public override void HandleInput()
        {
            if (_ink.Typing)
            {
                _ink.HandleTyping();
                return;
            }
            if (GameInput.Down(Key.Escape))
            {
                if (_board.CancelWire()) return;
                if (_board.ClearSelection()) return;
                if (_ink.Tool != InkTool.Off) SetTool(InkTool.Off);
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
            if (GameInput.Down(Key.T)) SetTool(_ink.Tool == InkTool.Text ? InkTool.Off : InkTool.Text);
            if (GameInput.Down(Key.Backspace) && _ink.ResumeTyping()) return;
            if ((GameInput.Down(Key.Delete) || GameInput.Down(Key.Backspace)) && _board.SelectedCount > 0)
            {
                _board.DeleteSelection();
                return;
            }
            if (GameInput.Down(Key.Backspace) || GameInput.Down(Key.Z))
            {
                if (_ink.Tool != InkTool.Off)
                {
                    if (_ink.UndoLast()) App.Sound(Sfx.Close, 1.6f, 0.4f);
                }
                else Undo();
            }
            if (GameInput.Down(Key.H)) OpenHelp();
            if (GameInput.Down(Key.Space) || GameInput.Down(Key.Enter)) StartRun();
        }

        void OpenPause()
        {
            CancelRun();
            _board.CancelWire();
            _ink.FinishTyping();
            var pause = Game.Screens.Create<PauseOverlay>();
            pause.Puzzle = Puzzle;
            bool moved = _board.BlocksMoved;
            if (_session.HasStart)
            {
                pause.Reset = () => { RestartWires(); PutBlocksBack(); };
                pause.ResetQuestion = moved ? "Put the wires back as this circuit began, and every block where it started? Notes stay." : "Put the wires back as this circuit began? Notes stay.";
            }
            else
            {
                pause.Reset = () => { ClearWires(); PutBlocksBack(); };
                pause.ResetQuestion = moved ? "Take every wire off this circuit, and put every block back where it started? Notes stay." : "Take every wire off this circuit? Notes stay.";
            }
            Game.Screens.Push(pause);
        }

        void OpenHelp()
        {
            CancelRun();
            _board.CancelWire();
            _ink.FinishTyping();
            var help = Game.Screens.Create<HelpOverlay>();
            help.Chapter = Puzzle.Chapter;
            help.Wiring = true;
            Game.Screens.Push(help);
        }

        /// <summary>
        /// Each block's rule is written under it once the circuit has been solved,
        /// and from then on whenever it's opened. The debug setting shows them always.
        /// </summary>
        bool RulesShown => Game.Save.Prefs.DebugRules || Game.Progress.IsSolved(Puzzle);

        public override void OnUncovered()
        {
            _board.SetShowRules(RulesShown);
            RefreshCounters();
        }

        public override void OnClosing()
        {
            _running = false;
            _runPlan.Clear();
            App.Audio?.StopSignals();
            Tweener.Kill(this);
            if (_ink != null) _ink.SetTool(InkTool.Off);
            if (_session != null) SaveWires();
        }
    }
}
