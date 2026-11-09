using System.Collections.Generic;
using Blackbox.Circuits;
using Blackbox.Core;
using Blackbox.Game;
using Blackbox.Puzzles;
using Blackbox.UI.Board;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>
    /// One circuit. The player sets the unknown boxes, runs the tests, probes boxes
    /// to see what they did, names the behaviours they work out, and solves the
    /// circuit when every test matches.
    /// </summary>
    public sealed class PuzzleScreen : GameScreen, IBoardHost
    {
        public PuzzleDef Puzzle;

        PuzzleSession _session;
        BoardView _board;
        CaseTable _table;
        Timeline _timeline;
        ProbeCard _probe;
        NamingPalette _naming;

        VoidText _title, _record, _status;
        VoidText _testsValue, _matchedValue, _matchedLabel;
        PillButton _run, _probeButton;
        CanvasGroup _controls, _solved;
        VoidText _solvedStats, _unlockLine;
        PillButton _next, _circuits;

        // What each behaviour (kind and setting) was seen to do, for logic circuits:
        // input combination (port 0 as the high bit) -> output.
        readonly Dictionary<string, Dictionary<int, bool>> _seen = new Dictionary<string, Dictionary<int, bool>>();

        bool _probeMode;
        bool _shift;
        int _pinned = -1;
        int _hover = -1;
        int _namingBox = -1;
        int _case;
        int _tick;
        bool _playing;
        bool _solvedShown;
        string _probeSignature;
        string _hint;
        PuzzleDef _nextPuzzle;

        bool IsTable => Puzzle.Mode == PuzzleMode.Table;

        // Layout, in frame coordinates from the top-left.
        const float Left = 90f;
        const float Right = 1830f;
        const float HeaderRule = 122f;
        const float StatusY = 782f;
        const float FooterRule = 800f;
        const float FooterTop = 824f;

        protected internal override void Build()
        {
            var p = Puzzle;
            Game.Save.Settings.TryGetValue(p.Id, out var resume);
            _session = new PuzzleSession(p, resume);
            _session.SettingChanged += OnSettingChanged;
            _session.Reset += OnSessionReset;

            BuildHeader();
            _board = BoardView.Create(Root, p, _session.Circuit, this);
            _board.StepTime = 0.15f;
            for (int b = 0; b < p.Boxes.Count; b++) _board.Boxes[b].SetSetting(_session.SettingOf(b), false);

            _hint = p.Hint ?? "";
            _status = UIKit.Text(Root, _hint, TextStyle.Body.WithSize(19), TextAnchor.MiddleCenter, "Status");
            _status.rectTransform.At(VoidFrame.Width * 0.5f, StatusY, 1500f, 30f);
            UIKit.Rule(Root, Left, Right, FooterRule, Palette.Rule);

            if (IsTable)
            {
                _table = CaseTable.Create(Root, p, Left, FooterTop);
                _table.RowClicked = i => SelectCase(i, true);
            }
            else
            {
                _timeline = Timeline.Create(Root, p, Left, FooterTop, 1000f);
                _timeline.TickChosen = SelectTick;
                _timeline.ShowCase(0);
            }

            BuildControls();
            BuildSolvedPanel();

            var popups = UIKit.Rect(Root, "Popups").Stretch();
            _probe = ProbeCard.Create(popups);
            _naming = NamingPalette.Create(popups);
            _naming.Chosen = OnNamed;
            _naming.Closed = () => { _namingBox = -1; _probeSignature = null; RefreshProbe(); };

            ShowCurrent(false);
            RefreshState();
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

            Chrome.Counter(Root, "TESTS", Right, 62f, out _testsValue);
            _testsValue.text = "00";
            _record = UIKit.Text(Root, "", TextStyle.Label.WithSize(11).WithTracking(0.2f).WithColor(Palette.Faint), TextAnchor.MiddleRight, "Record");
            _record.rectTransform.At(Right - 150f, 92f, 300f, 16f);
            if (Game.Progress.IsSolved(p))
            {
                Game.Save.BestTests.TryGetValue(p.Id, out int best);
                _record.text = best > 0 ? $"SOLVED BEFORE  ·  BEST {best}" : "SOLVED BEFORE";
            }
            UIKit.Rule(Root, Left, Right, HeaderRule, Palette.Rule);
        }

        void BuildControls()
        {
            var rt = UIKit.Rect(Root, "Controls").At(Right - 215f, FooterTop + 128f, 430f, 280f);
            _controls = rt.Group();
            const float w = 430f;

            _matchedValue = UIKit.Text(rt, "–", TextStyle.Value.WithSize(44).WithWeight(200), TextAnchor.MiddleRight, "Matched");
            _matchedValue.rectTransform.At(w - 100f, 30f, 200f, 56f);
            _matchedLabel = UIKit.Text(rt, IsTable ? "ROWS MATCHED" : "TICKS MATCHED", TextStyle.Label.WithSize(13), TextAnchor.MiddleRight, "Matched label");
            _matchedLabel.rectTransform.At(w - 120f, 74f, 240f, 20f);

            _probeButton = PillButton.Create(rt, "PROBE", w - 170f - 16f - 75f, 150f, 150f, 50f, false, "probe");
            _probeButton.Click.ClickSound = Sfx.Hover;
            _probeButton.Click.Clicked = () => SetProbeMode(!_probeMode);
            _run = PillButton.Create(rt, "RUN", w - 85f, 150f, 170f, 50f, true, "arrow");
            _run.Click.ClickSound = Sfx.Hover;
            _run.Click.Clicked = Run;

            Chrome.KeyHints(rt, w, 222f, true, "SPACE", "RUN", "P", "PROBE", "Z", "UNDO", "H", "HELP", "ESC", "MENU");
        }

        void BuildSolvedPanel()
        {
            var rt = UIKit.Rect(Root, "Solved").At(Right - 215f, FooterTop + 128f, 430f, 280f);
            _solved = rt.Group();
            _solved.alpha = 0f;
            _solved.blocksRaycasts = false;
            const float w = 430f;

            var label = UIKit.Text(rt, "CIRCUIT COMPLETE", TextStyle.Label.WithSize(15).WithTracking(0.34f).WithColor(Color.white), TextAnchor.MiddleRight, "Solved");
            label.SignalFill = true;
            label.rectTransform.At(w - 160f, 22f, 320f, 24f);
            _solvedStats = UIKit.Text(rt, "", TextStyle.Label.WithSize(13).WithColor(Palette.Dim), TextAnchor.MiddleRight, "Stats");
            _solvedStats.rectTransform.At(w - 200f, 54f, 400f, 20f);
            _unlockLine = UIKit.Text(rt, "", TextStyle.Label.WithSize(13).WithColor(Palette.Cyan), TextAnchor.MiddleRight, "Unlock");
            _unlockLine.rectTransform.At(w - 200f, 84f, 400f, 20f);

            _circuits = PillButton.Create(rt, "CIRCUITS", w - 230f - 16f - 75f, 150f, 150f, 50f, false);
            _circuits.Click.Clicked = () => Game.GoChapters(Puzzle.Chapter);
            _next = PillButton.Create(rt, "NEXT", w - 115f, 150f, 230f, 50f, true, "arrow");
            _next.Click.Clicked = GoNext;

            Chrome.KeyHints(rt, w, 222f, true, "ENTER", "NEXT", "ESC", "MENU");
        }

        // ------------------------------------------------------------------ showing state

        /// <summary>Draws the selected case and moment on the board, from the last run if there is one.</summary>
        void ShowCurrent(bool animate)
        {
            var run = _session.LastRun;
            if (run == null) _board.ShowInputsOnly(Puzzle.Cases[_case], _tick);
            else _board.Show(run.Cases[_case], _tick, animate, !_session.RunIsCurrent);
        }

        void RefreshState()
        {
            var run = _session.LastRun;
            bool current = _session.RunIsCurrent;
            bool stale = run != null && !current;
            _board.SetStale(stale);
            _table?.SetStale(stale);
            _timeline?.SetStale(stale);
            _run.Attention = current ? 0f : 1f;

            if (run == null)
            {
                _matchedValue.text = "–";
                _matchedValue.color = Palette.Faint;
            }
            else
            {
                Matched(run, out int ok, out int total);
                _matchedValue.text = $"{ok}/{total}";
                _matchedValue.color = stale ? Palette.Faint : run.AllMatched ? Palette.Cyan : Palette.Text;
            }
            _matchedLabel.color = stale ? Palette.Faint : Palette.Dim;
            SetSolvedShown(_session.SolvedNow && !_playing);
        }

        void Matched(RunResult run, out int ok, out int total)
        {
            ok = 0;
            total = 0;
            if (IsTable)
            {
                ok = run.MatchedCases;
                total = run.Cases.Length;
                return;
            }
            foreach (var cr in run.Cases)
            {
                for (int t = 0; t < cr.Ticks; t++)
                {
                    total++;
                    if (cr.TickOk(t)) ok++;
                }
            }
        }

        void SetSolvedShown(bool on)
        {
            if (_solvedShown == on) return;
            _solvedShown = on;
            _board.SetSolved(on);
            _title.SignalFill = on;
            _solved.blocksRaycasts = on;
            _controls.blocksRaycasts = !on;
            float fromA = _solved.alpha, fromB = _controls.alpha;
            Tweener.Run(this, "solved panel", 0.35f, t =>
            {
                _solved.alpha = Mathf.Lerp(fromA, on ? 1f : 0f, t);
                _controls.alpha = Mathf.Lerp(fromB, on ? 0f : 1f, t);
            }, Ease.OutCubic, on ? 0.25f : 0f);
            if (on) SetProbeMode(false, quiet: true);
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

        // ------------------------------------------------------------------ running

        void Run()
        {
            if (_playing) { FinishPlayback(); return; }
            if (_naming.IsOpen) return;

            var result = _session.Run();
            _testsValue.text = _session.Tests.ToString("00");
            App.Sound(Sfx.Run);
            Record(result);
            _playing = true;
            _board.SetStale(false);
            _table?.SetStale(false);
            _timeline?.SetStale(false);
            _run.Attention = 0f;
            bool fast = Game.Save.Prefs.FastRuns;

            if (IsTable)
            {
                _table.ShowRun(result, fast ? 0f : 0.07f);
                _board.Show(result.Cases[_case], 0, !fast, false);
                float duration = fast ? 0f : Mathf.Max(_board.PropagationTime, 0.07f * result.Cases.Length) + 0.15f;
                if (duration <= 0f) FinishPlayback();
                else Tweener.After(this, "play", duration, FinishPlayback);
            }
            else
            {
                var cr = result.Cases[_case];
                int ticks = cr.Ticks;
                if (fast) { FinishPlayback(); return; }
                int shown = -1;
                Tweener.Run(this, "play", 0.2f * ticks, t =>
                {
                    int k = Mathf.Min(ticks - 1, Mathf.FloorToInt(t * ticks));
                    if (k == shown) return;
                    shown = k;
                    _tick = k;
                    _timeline.ShowRun(cr, k + 1, false);
                    _timeline.SetTick(k);
                    _board.Show(cr, k, false, false);
                    App.Sound(Sfx.Tick, cr.TickOk(k) ? 1.25f : 0.8f, 0.7f);
                    RefreshProbe();
                }, Ease.Linear, 0f, FinishPlayback);
            }
            RefreshProbe();
        }

        /// <summary>Ends the run animation, or skips to its end.</summary>
        void FinishPlayback()
        {
            if (!_playing) return;
            Tweener.Kill(this, "play");
            _playing = false;
            var run = _session.LastRun;
            if (run == null) return;

            if (IsTable)
            {
                _table.ShowRun(run, 0f);
                _board.Show(run.Cases[_case], 0, false, false);
            }
            else
            {
                var cr = run.Cases[_case];
                // Stop where it first went wrong, or at the end when it all matched.
                _tick = cr.Ticks - 1;
                for (int t = 0; t < cr.Ticks; t++)
                    if (!cr.TickOk(t)) { _tick = t; break; }
                _timeline.ShowRun(cr, int.MaxValue, false);
                _timeline.SetTick(_tick);
                _board.Show(cr, _tick, false, false);
            }

            RefreshState();
            RefreshProbe();
            if (run.AllMatched) OnSolved();
            else
            {
                App.Sound(Sfx.Miss, 1f, 0.55f);
                if (!IsTable && _session.Tests <= 2) Say("The cursor stops at the first moment that doesn't match.", 3.5f);
            }
        }

        /// <summary>Remembers what each box did, so the probe can show it.</summary>
        void Record(RunResult run)
        {
            if (!IsTable) return;
            for (int b = 0; b < Puzzle.Boxes.Count; b++)
            {
                string key = SaveData.NameKey(Puzzle.Boxes[b].Kind.Id, run.Settings[b]);
                if (!_seen.TryGetValue(key, out var seen)) _seen[key] = seen = new Dictionary<int, bool>();
                foreach (var cr in run.Cases) seen[Combo(cr.BoxInputs(b, 0))] = cr.BoxOutput(b, 0);
            }
        }

        static int Combo(bool[] inputs)
        {
            int c = 0;
            foreach (bool v in inputs) c = (c << 1) | (v ? 1 : 0);
            return c;
        }

        void OnSolved()
        {
            var p = Puzzle;
            var library = Game.Library;
            bool first = !Game.Progress.IsSolved(p);
            var unlockedBefore = new List<bool>();
            foreach (var c in library.Chapters) unlockedBefore.Add(Game.Progress.IsUnlocked(c));

            Game.Save.BestTests.TryGetValue(p.Id, out int previousBest);
            Game.Save.RecordSolve(p.Id, _session.Tests);
            Game.Save.Settings[p.Id] = _session.CopySettings();
            Game.SaveNow();

            ChapterDef opened = null;
            for (int i = 0; i < library.Chapters.Count; i++)
                if (!unlockedBefore[i] && Game.Progress.IsUnlocked(library.Chapters[i])) opened = library.Chapters[i];

            int tests = _session.Tests;
            string stats = tests == 1 ? "1 TEST" : $"{tests} TESTS";
            if (!first && previousBest > 0) stats += tests < previousBest ? "  ·  NEW BEST" : $"  ·  BEST {previousBest}";
            int named = Game.Save.Names.Count;
            if (named > 0) stats += named == 1 ? "  ·  1 BEHAVIOUR NAMED" : $"  ·  {named} BEHAVIOURS NAMED";
            _solvedStats.text = stats;
            _unlockLine.text = opened != null ? $"CHAPTER {opened.Numeral} OPEN  ·  {opened.Title}" : "";
            _record.text = "";

            _nextPuzzle = Game.Progress.NextAfter(p);
            _next.SetText(_nextPuzzle == null ? "CIRCUITS" : _nextPuzzle.Chapter != p.Chapter ? "NEXT CHAPTER" : "NEXT CIRCUIT");
            _circuits.gameObject.SetActive(_nextPuzzle != null);

            App.Sound(Sfx.Solved);
            if (opened != null) Tweener.After(this, "unlock", 1.4f, () => App.Sound(Sfx.Unlock));
            Say(first ? "Solved." : "Solved again.", 3f);
        }

        void GoNext()
        {
            if (_nextPuzzle != null) Game.GoPuzzle(_nextPuzzle);
            else Game.GoChapters(Puzzle.Chapter);
        }

        // ------------------------------------------------------------------ settings

        void OnSettingChanged(int box)
        {
            if (_playing) FinishPlayback();
            _board.Boxes[box].SetSetting(_session.SettingOf(box), true);
            Game.Save.Settings[Puzzle.Id] = _session.CopySettings();
            Game.MarkDirty();
            ShowCurrent(false);
            RefreshState();
            RefreshProbe();
        }

        void OnSessionReset()
        {
            Tweener.Kill(this, "play");
            _playing = false;
            for (int b = 0; b < Puzzle.Boxes.Count; b++) _board.Boxes[b].SetSetting(_session.SettingOf(b), false);
            Game.Save.Settings.Remove(Puzzle.Id);
            Game.MarkDirty();
            _table?.Clear();
            _timeline?.ShowRun(null, 0, false);
            ShowCurrent(false);
            RefreshState();
            RefreshProbe();
            Say("Every box is back where it started.", 3f);
        }

        public void ResetCircuit() => _session.ResetToStart();

        void Undo()
        {
            if (_session.Undo()) App.Sound(Sfx.Dial, 0.85f);
            else App.Sound(Sfx.Miss, 1.5f, 0.3f);
        }

        // ------------------------------------------------------------------ cases and moments

        void SelectCase(int index, bool fromClick)
        {
            index = Mathf.Clamp(index, 0, Puzzle.Cases.Count - 1);
            if (_playing) FinishPlayback();
            if (index == _case) return;
            _case = index;
            if (!fromClick) App.Sound(Sfx.Tick);
            if (_table != null) _table.Selected = index;
            if (_timeline != null)
            {
                _timeline.ShowCase(index);
                var run = _session.LastRun;
                _timeline.ShowRun(run?.Cases[index], int.MaxValue, run != null && !_session.RunIsCurrent);
            }
            ShowCurrent(IsTable && _session.RunIsCurrent && !Game.Save.Prefs.FastRuns);
            RefreshProbe();
        }

        void SelectTick(int tick)
        {
            if (_timeline == null) return;
            if (_playing) FinishPlayback();
            _tick = Mathf.Clamp(tick, 0, _timeline.Ticks - 1);
            _timeline.SetTick(_tick);
            ShowCurrent(false);
            RefreshProbe();
        }

        // ------------------------------------------------------------------ probe and names

        void SetProbeMode(bool on, bool quiet = false)
        {
            if (_probeMode == on) return;
            _probeMode = on;
            _probeButton.Active = on;
            if (!on) _pinned = -1;
            if (!quiet)
            {
                App.Sound(on ? Sfx.Open : Sfx.Close, 1.5f, 0.6f);
                if (on) Say(_session.LastRun == null ? "Probe: point at a box. Run the circuit first to see what it does." : "Probe: point at a box to see what it did. Click to keep it open.", 4f);
            }
            RefreshProbe();
        }

        int ProbeTarget()
        {
            if (_namingBox >= 0) return -1;
            if (!_probeMode && !_shift) return -1;
            if (_hover >= 0) return _hover;
            return _probeMode ? _pinned : -1;
        }

        void RefreshProbe()
        {
            if (_board == null) return;
            int target = ProbeTarget();
            for (int b = 0; b < _board.Boxes.Count; b++)
                _board.Boxes[b].Highlight = b == target ? BoxHighlight.Probe : b == _hover || b == _namingBox ? BoxHighlight.Hover : BoxHighlight.None;

            if (target < 0)
            {
                _probeSignature = null;
                _probe.Hide();
                return;
            }

            var view = _board.Boxes[target];
            var def = view.Def;
            int setting = _session.SettingOf(target);
            var run = _session.LastRun;
            var name = NameOf(def, setting);
            string signature = $"{target}/{setting}/{_session.Tests}/{_case}/{_tick}/{name.Encode()}/{_session.RunIsCurrent}";
            if (signature == _probeSignature && _probe.Visible) return;
            _probeSignature = signature;

            if (IsTable)
            {
                _seen.TryGetValue(SaveData.NameKey(def.Kind.Id, setting), out var seen);
                int current = run != null && _session.RunIsCurrent ? Combo(run.Cases[_case].BoxInputs(target, 0)) : -1;
                _probe.ShowTable(view, setting, name, seen, current);
            }
            else
            {
                var cr = run != null && run.Settings[target] == setting ? run.Cases[_case] : null;
                _probe.ShowTrace(view, setting, name, cr, target, _tick);
            }
        }

        void OpenNaming(int box)
        {
            if (box < 0) return;
            if (_playing) FinishPlayback();
            _namingBox = box;
            var view = _board.Boxes[box];
            _probe.Hide();
            _probeSignature = null;
            _naming.Open(view, NameOf(view.Def, _session.SettingOf(box)));
            RefreshProbe();
        }

        void OnNamed(RuleName name)
        {
            if (_namingBox < 0) return;
            var def = Puzzle.Boxes[_namingBox];
            int setting = _session.SettingOf(_namingBox);
            bool hadNames = Game.Save.Names.Count > 0;
            Game.Save.SetName(def.Kind.Id, setting, name);
            Game.MarkDirty();
            _board.RefreshNames();
            App.Sound(name.IsEmpty ? Sfx.Close : Sfx.Name);
            if (name.IsEmpty) Say("Name cleared.", 2.5f);
            else if (!hadNames) Say("Named. Every box that behaves this way will show it, in every circuit.", 5f);
        }

        // ------------------------------------------------------------------ IBoardHost

        public void BoxClicked(int box)
        {
            if (_naming.IsOpen) return;
            if (_probeMode || _shift)
            {
                if (!_probeMode) SetProbeMode(true, quiet: true);
                _pinned = box;
                App.Sound(Sfx.Tick, 1.2f);
                _probeSignature = null;
                RefreshProbe();
                return;
            }
            if (!_session.CanChange(box))
            {
                App.Sound(Sfx.Miss, 1.6f, 0.35f);
                Say("This box is locked. Probe it to see what it does.", 3.5f);
                return;
            }
            _session.Cycle(box, 1);
        }

        public void BoxRightClicked(int box) => OpenNaming(box);

        public void BoxScrolled(int box, int direction)
        {
            if (_naming.IsOpen || !_session.CanChange(box)) return;
            if (_session.Cycle(box, direction)) App.Sound(Sfx.Dial, direction > 0 ? 1.05f : 0.95f);
        }

        public void PipClicked(int box, int setting)
        {
            if (_naming.IsOpen) return;
            _session.SetSetting(box, setting);
        }

        public void BoxHover(int box, bool hovering)
        {
            if (hovering) _hover = box;
            else if (_hover == box) _hover = -1;
            RefreshProbe();
        }

        public RuleName NameOf(BoxDef box, int setting) => Game.Save.GetName(box.Kind.Id, setting);

        // ------------------------------------------------------------------ keys

        int KeyboardBox() => _hover >= 0 ? _hover : _pinned;

        void FocusNextBox(int direction)
        {
            int n = Puzzle.Boxes.Count;
            if (n == 0) return;
            int start = KeyboardBox();
            _hover = start < 0 ? (direction > 0 ? 0 : n - 1) : ((start + direction) % n + n) % n;
            if (_probeMode) _pinned = _hover;
            App.Sound(Sfx.Hover);
            RefreshProbe();
        }

        public override void HandleInput()
        {
            if (_naming.IsOpen)
            {
                _naming.HandleInput();
                return;
            }

            bool shift = GameInput.Held(Key.Shift);
            if (shift != _shift)
            {
                _shift = shift;
                RefreshProbe();
            }

            if (GameInput.Down(Key.Escape))
            {
                if (_probeMode) SetProbeMode(false);
                else OpenPause();
                return;
            }
            if (_solvedShown && GameInput.Down(Key.Enter))
            {
                App.Sound(Sfx.Click);
                GoNext();
                return;
            }
            if (GameInput.Confirm) { Run(); return; }
            if (GameInput.Down(Key.P)) SetProbeMode(!_probeMode);
            if (GameInput.Down(Key.Backspace) || GameInput.Down(Key.Z)) Undo();
            if (GameInput.Down(Key.Tab)) FocusNextBox(shift ? -1 : 1);
            if (GameInput.Down(Key.H)) OpenHelp();
            if (GameInput.Down(Key.R))
            {
                int b = KeyboardBox();
                if (b < 0) Say("Point at a box, or press Tab to choose one, then R to turn it.", 3.5f);
                else BoxClickedFromKey(b, shift ? -1 : 1);
            }
            if (GameInput.Down(Key.N))
            {
                int b = KeyboardBox();
                if (b < 0) Say("Point at a box, then press N to name what it does.", 3.5f);
                else OpenNaming(b);
            }

            if (GameInput.Down(Key.Up)) SelectCase(_case - 1, false);
            else if (GameInput.Down(Key.Down)) SelectCase(_case + 1, false);
            if (!IsTable)
            {
                if (GameInput.Down(Key.Left)) { App.Sound(Sfx.Tick); SelectTick(_tick - 1); }
                else if (GameInput.Down(Key.Right)) { App.Sound(Sfx.Tick); SelectTick(_tick + 1); }
            }
        }

        void BoxClickedFromKey(int box, int direction)
        {
            if (!_session.CanChange(box))
            {
                App.Sound(Sfx.Miss, 1.6f, 0.35f);
                Say("This box is locked. Probe it to see what it does.", 3.5f);
                return;
            }
            if (_session.Cycle(box, direction)) App.Sound(Sfx.Dial);
        }

        void OpenPause()
        {
            if (_playing) FinishPlayback();
            _naming.Close();
            var pause = Game.Screens.Create<PauseOverlay>();
            pause.Owner = this;
            Game.Screens.Push(pause);
        }

        void OpenHelp()
        {
            var help = Game.Screens.Create<HelpOverlay>();
            help.Trace = !IsTable;
            Game.Screens.Push(help);
        }

        public override void OnUncovered()
        {
            _shift = false;
            _board.RefreshNames();
            _probeSignature = null;
            RefreshProbe();
        }

        public override void OnClosing()
        {
            Tweener.Kill(this);
            if (_session != null) Game.MarkDirty();
        }
    }
}
