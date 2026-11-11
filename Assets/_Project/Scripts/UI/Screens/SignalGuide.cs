using System.Collections.Generic;
using Blackbox.Components;
using Blackbox.Game;
using Blackbox.Puzzles;
using Blackbox.UI.Board;
using UnityEngine;

namespace Blackbox.UI.Screens
{
    /// <summary>
    /// The chapter's kinds of signal, drawn as the board draws them, with notes
    /// pointing at what to look for: where a bit's line sits, how a digit's
    /// height reads, what a wave's F and A mean, and how each kind of block reads
    /// the other kinds. It follows the chapter's title card on the first visit,
    /// before the first circuit, and opens again from the pause menu. The chapter
    /// says which page it shows (<see cref="SignalGuide"/>). Every signal on it
    /// can be clicked to hear it, with a playhead running through the loop.
    /// </summary>
    public sealed class SignalGuideOverlay : Overlay
    {
        public ChapterDef Chapter;
        /// <summary>Opened from the pause menu, so closing it goes back there.</summary>
        public bool FromMenu;

        /// <summary>Which side of the end of its leader a note is written on.</summary>
        enum Side { Left, Right, Above, Below }

        sealed class Loop
        {
            public SignalView View;
            public float Start;
            /// <summary>How long the playhead takes over the loop: as long as the signal's sound, for a pattern.</summary>
            public float Seconds;
            /// <summary>Plays through once, then hides its playhead.</summary>
            public bool Once;
        }

        const float Cx = VoidFrame.Width * 0.5f;
        static readonly Color LeaderColour = Palette.Dim.WithAlpha(0.8f);
        static readonly Color NumberColour = Color.Lerp(Palette.Dim, Palette.Text, 0.6f);

        readonly List<Loop> _loops = new List<Loop>();
        RectTransform _content;
        VoidText _prompt;
        float _shownAt;
        float _nextNote;
        SignalView _lead;

        protected override float VeilAlpha => 1f;

        /// <summary>True when the chapter has a guide to show.</summary>
        public static bool Has(ChapterDef chapter) => chapter != null && chapter.Guide != SignalGuide.None;

        protected internal override void Build()
        {
            BuildVeil();
            var c = Veil.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = TryDismiss;

            _content = UIKit.Rect(Root, "Guide").Stretch();
            _nextNote = 0.35f;
            switch (Chapter.Guide)
            {
                case SignalGuide.Bits: BuildBits(); break;
                case SignalGuide.Digits: BuildDigits(); break;
                case SignalGuide.Waves: BuildWaves(); break;
                case SignalGuide.Readings: BuildReadings(); break;
                default: BuildRecap(); break;
            }

            var hint = UIKit.Text(_content, "CLICK ANY SIGNAL TO HEAR IT", TextStyle.Label.WithSize(11).WithTracking(0.34f).WithColor(Palette.Faint));
            hint.raycastTarget = false;
            hint.rectTransform.At(Cx, 968f, 600f, 16f);
            _prompt = UIKit.Text(_content, FromMenu ? "CLICK OR PRESS ENTER TO GO BACK" : "CLICK OR PRESS ENTER TO BEGIN", TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
            _prompt.raycastTarget = false;
            _prompt.rectTransform.At(Cx, 1006f, 700f, 18f);
            if (!App.ReduceMotion)
            {
                _prompt.canvasRenderer.SetAlpha(0f);
                Tweener.Run(_prompt, "show", 0.6f, t => _prompt.canvasRenderer.SetAlpha(t), Ease.OutQuad, _nextNote + 0.2f);
            }

            // The main example plays once, so its sound and its line are met together.
            if (_lead != null && !FromMenu)
                Tweener.After(this, "play", 1.1f, () => { if (_lead != null) Play(_lead); });

            _shownAt = Time.unscaledTime;
        }

        // ------------------------------------------------------------------ pages

        /// <summary>First Light: a bit's line above or below the middle, steady, then switching through the loop.</summary>
        void BuildBits()
        {
            Header("READING A BIT", "A BIT IS 0 OR 1. IT'S DRAWN AS A LINE ACROSS THE LOOP.");

            Example("binary 1", 720f, 318f, 240f, 60f);
            Caption(720f, 362f, "A STEADY 1", "ABOVE THE MIDDLE ALL LOOP LONG");
            Example("binary 0", 1200f, 318f, 240f, 60f);
            Caption(1200f, 362f, "A STEADY 0", "BELOW THE MIDDLE ALL LOOP LONG");

            var big = _lead = Example("binary 0011", Cx, 640f, 760f, 140f, loop: true);
            var one = At(big, 0.75f, 1f);
            Note(one, new Vector2(one.x, 524f), Side.Above, "1: ABOVE THE MIDDLE", "A 1 SOUNDS AS A BEEP");
            var zero = At(big, 0.25f, 0f);
            Note(zero, new Vector2(zero.x, 766f), Side.Below, "0: BELOW THE MIDDLE", "A 0 IS ONLY A SOFT TAP");
            var mid = At(big, 1f, 0.5f);
            Note(mid, new Vector2(mid.x + 70f, mid.y), Side.Right, "THE MIDDLE", "THE FAINT LINE", 220f);
            LoopArrow(big, 862f);
        }

        /// <summary>Tally: a steady digit is its number; a changing one is a height, 0 at the bottom and 9 at the top.</summary>
        void BuildDigits()
        {
            Header("READING A DIGIT", "A DIGIT IS A NUMBER FROM 0 TO 9.");

            Example("decimal 7", 600f, 318f, 150f, 72f);
            Caption(600f, 370f, "A STEADY DIGIT", "SHOWN AS JUST ITS NUMBER");
            Example("decimal 012345678", 1320f, 318f, 280f, 64f);
            Caption(1320f, 370f, "A DIGIT WITH MANY STEPS", "ONLY THE HIGHEST AND LOWEST ARE WRITTEN, AT THE LEFT", 380f);

            var big = _lead = Example("decimal 2709", Cx, 640f, 760f, 170f, loop: true);
            // A scale beside the line, from 0 at the bottom to 9 at the top.
            var bottom = At(big, 0f, 0f);
            var top = At(big, 0f, 9f);
            float x = bottom.x - 28f;
            var g = NoteGroup();
            Line(g, new Vector2(x + 9f, top.y), new Vector2(x, top.y), new Vector2(x, bottom.y), new Vector2(x + 9f, bottom.y));
            Small(g, "9", x - 12f, top.y);
            Small(g, "0", x - 12f, bottom.y);
            Words(g, new Vector2(x - 22f, (top.y + bottom.y) * 0.5f), Side.Left, "HEIGHT IS THE DIGIT", "0 AT THE BOTTOM, 9 AT THE TOP. A BIGGER DIGIT SOUNDS HIGHER.", 300f);
            Reveal(g);

            // The number written on a step: point at the last one, 9, from the right.
            if (big.StepNumbers.Count > 0)
            {
                var nine = big.StepNumbers[big.StepNumbers.Count - 1];
                var at = FromView(big, nine.rectTransform.anchoredPosition + new Vector2(nine.fontSize * 0.5f, 0f));
                var (_, right) = big.LineSpan;
                Note(at, new Vector2(FromView(big, new Vector2(right, 0f)).x + 40f, at.y), Side.Right, "EACH STEP'S DIGIT", "IS WRITTEN ON IT", 240f);
            }
            LoopArrow(big, 790f);
        }

        /// <summary>Resonance: one cycle and F, the height and A, the three forms, and a wave that starts late.</summary>
        void BuildWaves()
        {
            Header("READING A WAVE", "A WAVE RISES AND FALLS ABOUT THE MIDDLE.");

            var big = _lead = Example("wave sine f2 a5", Cx, 470f, 760f, 170f, loop: true);
            var (left, right) = big.LineSpan;

            // One cycle, bracketed above the line.
            var start = At(big, 0f, 7f);
            var half = At(big, 0.5f, 7f);
            float y = 470f - 85f - 12f;
            var g = NoteGroup();
            Line(g, new Vector2(start.x, y + 8f), new Vector2(start.x, y), new Vector2(half.x, y), new Vector2(half.x, y + 8f));
            Words(g, new Vector2((start.x + half.x) * 0.5f, y), Side.Above, "ONE CYCLE",
                "F2: IT REPEATS TWICE A LOOP. THE HIGHER F IS, FROM 1 TO 7, THE FASTER IT REPEATS AND THE HIGHER IT SOUNDS.", 480f);
            Reveal(g);

            // The height, measured from the middle to a peak, beside the line.
            var peak = At(big, 0.625f, 5f);
            var middle = At(big, 1f, 0f);
            float x = FromView(big, new Vector2(right, 0f)).x + 26f;
            g = NoteGroup();
            Line(g, LeaderColour.WithAlpha(0.35f), new Vector2(peak.x + 6f, peak.y), new Vector2(x - 12f, peak.y));
            Line(g, new Vector2(x - 9f, peak.y), new Vector2(x, peak.y), new Vector2(x, middle.y), new Vector2(x - 9f, middle.y));
            Words(g, new Vector2(x + 4f, (peak.y + middle.y) * 0.5f), Side.Right, "A5: 5 UP FROM THE MIDDLE",
                "AND 5 DOWN. THE HIGHER A IS, FROM 1 TO 7, THE TALLER IT IS AND THE LOUDER IT SOUNDS.", 300f);
            Reveal(g);

            // F and A are written under every wave, as here.
            var settings = UIKit.Text(_content, Waves.Describe(big.Trace), TextStyle.Label.WithSize(15).WithTracking(0.16f).WithColor(NumberColour));
            settings.raycastTarget = false;
            settings.rectTransform.At(Cx, 576f, 200f, 20f);
            float w = settings.MeasureWidth();
            Note(new Vector2(Cx - w * 0.5f - 10f, 576f), new Vector2(Cx - w * 0.5f - 60f, 576f), Side.Left, "WRITTEN UNDER EVERY WAVE", null, 360f, dot: false);

            // The three forms, each its own sound.
            Section(760f, 676f, "THREE FORMS, EACH WITH ITS OWN SOUND");
            string[] forms = { "sine", "square", "saw" };
            for (int i = 0; i < forms.Length; i++)
            {
                float fx = 470f + 290f * i;
                Example($"wave {forms[i]} f1 a6", fx, 752f, 220f, 64f);
                Caption(fx, 800f, forms[i].ToUpperInvariant(), null);
            }

            // A wave that starts late, over the same wave starting on time.
            Section(1440f, 676f, "STARTING LATE");
            var onTime = Example("wave sine f1 a6", 1440f, 752f, 220f, 64f, playable: false);
            onTime.Look = ValueLook.Target;
            onTime.Group().alpha = 0.55f;
            Example("wave sine f1 a6 p1", 1440f, 752f, 220f, 64f);
            Caption(1440f, 800f, "+1/4: A QUARTER CYCLE LATE", "+1/2 AND +3/4 ARE HALF AND THREE QUARTERS LATE. THE FAINT LINE STARTS ON TIME.", 340f);
        }

        /// <summary>The mixed chapters: one signal of each kind, as a block of each kind reads it.</summary>
        void BuildReadings()
        {
            Header("HOW A BLOCK READS EACH KIND", "A BLOCK READS EVERY SIGNAL AS THE KIND IT SENDS.");

            var kinds = new[] { Medium.Binary, Medium.Decimal, Medium.Wave };
            string[] names = { "A BIT", "A DIGIT", "A WAVE" };
            string[] blocks = { "A BIT BLOCK SEES", "A DIGIT BLOCK SEES", "A WAVE BLOCK SEES" };
            string[] examples = { "binary 0110", "decimal 0396", "wave sine f1 a7" };
            float[] columns = { 830f, 1190f, 1550f };
            const float source = 450f, w = 250f, h = 66f;

            Section(source, 272f, "THE SIGNAL");
            for (int c = 0; c < 3; c++) Section(columns[c], 272f, blocks[c]);
            Line(_content, Palette.Outline, new Vector2(640f, 300f), new Vector2(640f, 830f));

            for (int r = 0; r < 3; r++)
            {
                float y = 370f + 190f * r;
                var signal = Signals.Parse(examples[r]).Trace;
                var label = UIKit.Text(_content, names[r], TextStyle.Label.WithSize(14).WithTracking(0.2f).WithColor(Palette.Text), TextAnchor.MiddleRight);
                label.raycastTarget = false;
                label.rectTransform.At(source - w * 0.5f - 90f, y, 150f, 20f);
                Example(signal, source, y, w, h);

                for (int c = 0; c < 3; c++)
                {
                    bool same = kinds[c] == kinds[r];
                    var read = Example(signal.As(kinds[c]), columns[c], y, w, h);
                    if (same) read.Group().alpha = 0.45f;
                    string how = Media.HowRead(kinds[r], kinds[c]);
                    Caption(columns[c], y + h * 0.5f + 8f, null, same ? "THE SAME: IT'S ALREADY THIS KIND" : how, 330f);
                }
            }
        }

        /// <summary>The last chapters: all three kinds side by side, with what to look for in each.</summary>
        void BuildRecap()
        {
            Header("EVERY KIND OF SIGNAL", Chapter.Signals);

            var columns = new (string title, string example, string[] lines)[]
            {
                ("BITS", "binary 0110", new[]
                {
                    "ABOVE THE MIDDLE IS 1, BELOW IS 0.",
                    "A 1 BEEPS; A 0 IS A SOFT TAP.",
                    "A BIT BLOCK READS A DIGIT AS 1 UNLESS IT'S 0, AND A WAVE AS 1 WHILE IT'S ABOVE THE MIDDLE.",
                }),
                ("DIGITS", "decimal 1357", new[]
                {
                    "HEIGHT IS THE DIGIT: 0 AT THE BOTTOM, 9 AT THE TOP. A STEADY DIGIT IS JUST ITS NUMBER.",
                    "A BIGGER DIGIT SOUNDS HIGHER.",
                    "A DIGIT BLOCK READS A BIT AS 0 OR 1, AND A WAVE BY ITS HEIGHT.",
                }),
                ("WAVES", "wave sine f2 a5", new[]
                {
                    "F IS HOW OFTEN IT REPEATS, AND HOW HIGH IT SOUNDS. A IS HOW TALL IT IS, AND HOW LOUD.",
                    "+1/4, +1/2 OR +3/4 STARTS IT THAT MUCH OF A CYCLE LATE.",
                    "A WAVE BLOCK READS A BIT AS THE TOP OR THE BOTTOM, AND A DIGIT AS A LEVEL.",
                }),
            };
            for (int i = 0; i < columns.Length; i++)
            {
                var (title, example, lines) = columns[i];
                float x = 500f + 460f * i;
                Section(x, 330f, title);
                var view = Example(example, x, 410f, 300f, 76f);
                if (view.Trace.Medium == Medium.Wave) Small(_content, Waves.Describe(view.Trace), x, 462f, 14);
                float top = 498f;
                foreach (var line in lines)
                {
                    var g = NoteGroup();
                    top += Paragraph(g, line, TextStyle.Body.WithSize(17), x, top, 360f, TextAnchor.UpperCenter) + 16f;
                    Reveal(g);
                }
            }
        }

        // ------------------------------------------------------------------ pieces

        void Header(string heading, string sub)
        {
            var where = UIKit.Text(_content, $"CHAPTER {Chapter.Numeral}  ·  {Chapter.Title}  ·  SIGNALS", TextStyle.Label.WithSize(13).WithTracking(0.4f).WithColor(Palette.Faint));
            where.raycastTarget = false;
            where.rectTransform.At(Cx, 92f, 1200f, 20f);
            var title = UIKit.Text(_content, heading, TextStyle.Heading.WithSize(40).WithWeight(200));
            title.raycastTarget = false;
            title.rectTransform.At(Cx, 140f, 1500f, 56f);
            if (!string.IsNullOrEmpty(sub)) Paragraph(_content, sub, TextStyle.Body.WithSize(19), Cx, 176f, 1300f, TextAnchor.UpperCenter);
        }

        /// <summary>A small heading over a group of examples.</summary>
        void Section(float x, float y, string text)
        {
            var t = UIKit.Text(_content, text, TextStyle.Label.WithSize(12).WithTracking(0.3f).WithColor(Palette.Faint));
            t.raycastTarget = false;
            t.rectTransform.At(x, y, 520f, 18f);
        }

        SignalView Example(string spec, float x, float y, float w, float h, bool loop = false, bool playable = true) =>
            Example(Signals.Parse(spec).Trace, x, y, w, h, loop, playable);

        /// <summary>A signal as the board draws it, centred at (x, y), that plays when clicked.</summary>
        SignalView Example(Trace trace, float x, float y, float w, float h, bool loop = false, bool playable = true)
        {
            var view = SignalView.Create(_content, w, h, "Example");
            view.GetComponent<RectTransform>().At(x, y, w, h);
            view.Set(trace, ValueLook.Live);
            var run = new Loop { View = view, Start = Time.unscaledTime, Seconds = SecondsFor(trace), Once = App.ReduceMotion || !loop };
            if (loop && !App.ReduceMotion) _loops.Add(run);
            if (playable) view.MakePlayable(() => Play(view));
            return view;
        }

        /// <summary>Plays a signal from the start of the loop, with its playhead running along it.</summary>
        void Play(SignalView view)
        {
            if (view == null || view.Trace == null) return;
            App.Audio?.PlayTrace(view.Trace);
            view.Ping();
            var run = _loops.Find(l => l.View == view);
            if (run == null) _loops.Add(run = new Loop { View = view, Seconds = SecondsFor(view.Trace), Once = true });
            run.Start = Time.unscaledTime;
        }

        /// <summary>
        /// A pattern of bits or digits sounds each value in turn over a set time, so
        /// its playhead keeps that time and lands on each value as it sounds. A wave
        /// sounds as a tone, so its playhead keeps the board's time.
        /// </summary>
        static float SecondsFor(Trace trace) =>
            trace != null && !Media.IsWave(trace.Medium) && !trace.IsFlat ? SignalSound.PatternLength : WiringBoard.LoopSeconds;

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = _loops.Count - 1; i >= 0; i--)
            {
                var run = _loops[i];
                if (run.View == null) { _loops.RemoveAt(i); continue; }
                float t = (now - run.Start) / run.Seconds;
                if (run.Once && t >= 1f)
                {
                    run.View.Playhead = -1f;
                    _loops.RemoveAt(i);
                    continue;
                }
                run.View.Playhead = Mathf.Repeat(t, 1f);
            }
        }

        /// <summary>A spot on a signal's line, in frame units from the top left, y down.</summary>
        static Vector2 At(SignalView view, float loop, float level) => FromView(view, view.PointAt(loop, level));

        static Vector2 FromView(SignalView view, Vector2 local)
        {
            var p = view.GetComponent<RectTransform>().anchoredPosition;
            return new Vector2(p.x + local.x, -p.y - local.y);
        }

        RectTransform NoteGroup()
        {
            var g = UIKit.Rect(_content, "Note").Stretch();
            var group = g.Group();
            group.blocksRaycasts = false;
            group.interactable = false;
            return g;
        }

        /// <summary>Fades a note in after the ones before it, so the eye follows them in turn.</summary>
        void Reveal(RectTransform g)
        {
            var group = g.Group();
            if (App.ReduceMotion) return;
            group.alpha = 0f;
            Tweener.Run(g, "show", 0.5f, t => group.alpha = t, Ease.OutQuad, _nextNote);
            _nextNote += 0.3f;
        }

        /// <summary>A dot on the spot, a leader from it to a note, and the note.</summary>
        void Note(Vector2 at, Vector2 to, Side side, string title, string detail, float width = 330f, bool dot = true)
        {
            var g = NoteGroup();
            if (Mathf.Abs(at.x - to.x) > 0.5f && Mathf.Abs(at.y - to.y) > 0.5f) Line(g, at, new Vector2(at.x, to.y), to);
            else Line(g, at, to);
            if (dot)
            {
                var d = UIKit.Shape(g, "Dot", ShapeKind.Circle);
                d.Style(Paint.Solid, Palette.Text, Paint.None, Color.clear, 0f);
                d.rectTransform.At(at.x, at.y, 7f, 7f);
            }
            Words(g, to, side, title, detail, width);
            Reveal(g);
        }

        /// <summary>A note with no leader, centred under a spot.</summary>
        void Caption(float x, float top, string title, string detail, float width = 330f)
        {
            var g = NoteGroup();
            Words(g, new Vector2(x, top - 8f), Side.Below, title, detail, width);
            Reveal(g);
        }

        /// <summary>
        /// A note's words: a title in small capitals, and a line or two under it,
        /// written on <paramref name="side"/> of <paramref name="to"/>. Beside the
        /// point, the title is level with it.
        /// </summary>
        void Words(Transform g, Vector2 to, Side side, string title, string detail, float width)
        {
            const float gap = 8f, titleH = 20f, between = 6f;
            var align = side == Side.Left ? TextAnchor.UpperRight : side == Side.Right ? TextAnchor.UpperLeft : TextAnchor.UpperCenter;
            float x = side == Side.Left ? to.x - gap : side == Side.Right ? to.x + gap : to.x;
            bool hasTitle = !string.IsNullOrEmpty(title);
            VoidText words = null;
            float detailH = 0f;
            if (!string.IsNullOrEmpty(detail))
                words = Measure(g, detail, TextStyle.Body.WithSize(17), width, align, out detailH);
            float height = (hasTitle ? titleH : 0f) + (words != null ? (hasTitle ? between : 0f) + detailH : 0f);
            float top;
            switch (side)
            {
                case Side.Above: top = to.y - gap - height; break;
                case Side.Below: top = to.y + gap; break;
                default: top = to.y - (hasTitle ? titleH : detailH) * 0.5f; break;
            }
            if (hasTitle)
            {
                var t = UIKit.Text(g, title, TextStyle.Label.WithSize(14).WithTracking(0.2f).WithColor(Palette.Text), Middle(align));
                t.raycastTarget = false;
                t.rectTransform.At(CentreX(x, width, align), top + titleH * 0.5f, width, titleH);
                top += titleH + between;
            }
            if (words != null) Place(words, x, top, width, detailH, align);
        }

        /// <summary>Writes a paragraph down from <paramref name="top"/>, wrapped to the width, and returns its height.</summary>
        static float Paragraph(Transform parent, string text, TextStyle style, float x, float top, float width, TextAnchor align)
        {
            var t = Measure(parent, text, style, width, align, out float h);
            Place(t, x, top, width, h, align);
            return h;
        }

        static VoidText Measure(Transform parent, string text, TextStyle style, float width, TextAnchor align, out float height)
        {
            var t = UIKit.Text(parent, text, style, align);
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            height = t.LinesAt(width) * style.Size * t.LineHeight;
            return t;
        }

        static void Place(VoidText t, float x, float top, float width, float height, TextAnchor align) =>
            t.rectTransform.At(CentreX(x, width, align), top + height * 0.5f, width, height + 6f);

        /// <summary>The centre of a box of the width whose aligned edge is at x.</summary>
        static float CentreX(float x, float width, TextAnchor align)
        {
            switch (align)
            {
                case TextAnchor.UpperLeft: case TextAnchor.MiddleLeft: case TextAnchor.LowerLeft: return x + width * 0.5f;
                case TextAnchor.UpperRight: case TextAnchor.MiddleRight: case TextAnchor.LowerRight: return x - width * 0.5f;
                default: return x;
            }
        }

        static TextAnchor Middle(TextAnchor align) =>
            align == TextAnchor.UpperLeft ? TextAnchor.MiddleLeft : align == TextAnchor.UpperRight ? TextAnchor.MiddleRight : TextAnchor.MiddleCenter;

        /// <summary>A small number or word in the colour of the numbers on a line.</summary>
        static void Small(Transform parent, string text, float x, float y, int size = 13)
        {
            var t = UIKit.Text(parent, text, TextStyle.Label.WithSize(size).WithTracking(0.16f).WithColor(NumberColour));
            t.raycastTarget = false;
            t.rectTransform.At(x, y, 200f, size + 6f);
        }

        /// <summary>An arrow under a signal along its whole loop, saying how the loop plays.</summary>
        void LoopArrow(SignalView view, float y)
        {
            var (left, right) = view.LineSpan;
            float x0 = FromView(view, new Vector2(left, 0f)).x, x1 = FromView(view, new Vector2(right, 0f)).x;
            var g = NoteGroup();
            Line(g, new Vector2(x0, y - 5f), new Vector2(x0, y + 5f));
            Line(g, new Vector2(x0, y), new Vector2(x1, y));
            Line(g, new Vector2(x1 - 9f, y - 5f), new Vector2(x1, y), new Vector2(x1 - 9f, y + 5f));
            Words(g, new Vector2((x0 + x1) * 0.5f, y), Side.Below, "ONE LOOP", "IT PLAYS LEFT TO RIGHT, THEN STARTS AGAIN, OVER AND OVER", 640f);
            Reveal(g);
        }

        static void Line(Transform g, params Vector2[] points) => Line(g, LeaderColour, points);

        /// <summary>A thin line through points given in frame units from the top left, y down.</summary>
        static void Line(Transform g, Color colour, params Vector2[] points)
        {
            var p = UIKit.Add<PathGraphic>(g, "Leader");
            p.raycastTarget = false;
            p.rectTransform.Stretch();
            p.BaseColor = colour;
            p.BaseWidth = 1f;
            p.Glow = 0f;
            var local = new List<Vector2>(points.Length);
            foreach (var q in points) local.Add(new Vector2(q.x - VoidFrame.Width * 0.5f, VoidFrame.Height * 0.5f - q.y));
            p.SetPoints(local);
        }

        // ------------------------------------------------------------------ closing

        void TryDismiss()
        {
            if (Time.unscaledTime - _shownAt < 0.6f) return;
            App.Sound(Sfx.Close);
            Close();
        }

        public override void OnClosing()
        {
            Tweener.Kill(this);
            App.Audio?.StopSignals();
            base.OnClosing();
        }

        public override void HandleInput()
        {
            if (GameInput.Confirm || GameInput.Down(Key.Escape)) TryDismiss();
        }
    }
}
