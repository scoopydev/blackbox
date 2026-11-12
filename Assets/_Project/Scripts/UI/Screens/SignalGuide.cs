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
    /// says which guide it shows (<see cref="SignalGuide"/>); most guides are one
    /// page, and the waves guide has a second on which way round two wires go
    /// into a block, taught by Resonance's TURNAROUND. Every signal on it can be
    /// clicked to hear it, with a playhead running through the loop.
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
        List<System.Action> _pages;
        int _page;
        VoidText _prompt;
        float _shownAt;
        float _nextNote;
        SignalView _lead;

        protected override float VeilAlpha => 1f;

        /// <summary>True when the chapter has a guide to show.</summary>
        public static bool Has(ChapterDef chapter) => chapter != null && chapter.Guide != SignalGuide.None;

        /// <summary>Drawn as a tab of How to Play rather than as its own screen: no veil, nothing plays by itself, and pages turn with buttons.</summary>
        bool _embedded;

        /// <summary>Draws a chapter's guide inside <paramref name="parent"/>, as a tab of How to Play.</summary>
        public static SignalGuideOverlay Embed(RectTransform parent, ChapterDef chapter)
        {
            var rt = UIKit.Rect(parent, "Signal guide").Stretch();
            var guide = rt.gameObject.AddComponent<SignalGuideOverlay>();
            guide.Chapter = chapter;
            guide._embedded = true;
            guide.FromMenu = true;
            guide._pages = guide.PagesOf(chapter.Guide);
            guide._page = 0;
            guide.BuildPage();
            return guide;
        }

        void OnDestroy()
        {
            if (_embedded) Tweener.Kill(this);
        }

        /// <summary>Turns to the next or previous page, if there is one.</summary>
        public void TurnPage(int direction)
        {
            int next = Mathf.Clamp(_page + direction, 0, _pages.Count - 1);
            if (next == _page) return;
            App.Sound(Sfx.Click);
            _page = next;
            BuildPage();
        }

        protected internal override void Build()
        {
            BuildVeil();
            var c = Veil.gameObject.AddComponent<Clickable>();
            c.HoverSound = false;
            c.ClickSound = Sfx.Hover;
            c.Clicked = TryDismiss;

            _pages = PagesOf(Chapter.Guide);
            _page = 0;
            BuildPage();
        }

        /// <summary>The pages of a guide, in the order they're shown.</summary>
        List<System.Action> PagesOf(SignalGuide guide)
        {
            switch (guide)
            {
                case SignalGuide.Bits: return new List<System.Action> { BuildBits };
                case SignalGuide.Digits: return new List<System.Action> { BuildDigits };
                case SignalGuide.ChangingDigits: return new List<System.Action> { BuildChangingDigits };
                case SignalGuide.Waves: return new List<System.Action> { BuildWaves, BuildWaveSettings, BuildInputOrder };
                case SignalGuide.Readings: return new List<System.Action> { BuildReadings };
                case SignalGuide.Listening: return new List<System.Action> { BuildListening };
                case SignalGuide.Dark: return new List<System.Action> { BuildDark };
                default: return new List<System.Action> { BuildRecap };
            }
        }

        bool LastPage => _page >= _pages.Count - 1;

        /// <summary>Clears whatever page was showing and builds the current one.</summary>
        void BuildPage()
        {
            Tweener.Kill(this, "play");
            App.Audio?.StopSignals();
            _loops.Clear();
            _lead = null;
            if (_content != null)
            {
                _content.gameObject.SetActive(false);
                Destroy(_content.gameObject);
            }

            _content = UIKit.Rect(Root, "Guide").Stretch();
            _nextNote = 0.35f;
            _pages[_page]();

            var hint = UIKit.Text(_content, "Click any signal to hear it", TextStyle.Label.WithSize(11).WithTracking(0.34f).WithColor(Palette.Faint));
            hint.raycastTarget = false;
            hint.rectTransform.At(Cx, 968f, 600f, 16f);
            _shownAt = Time.unscaledTime;
            if (_embedded)
            {
                // Inside How to Play: page buttons in place of the prompt.
                if (_pages.Count > 1)
                {
                    var count = UIKit.Text(_content, $"PAGE {_page + 1} OF {_pages.Count}", TextStyle.Label.WithSize(12).WithTracking(0.3f).WithColor(Palette.Faint));
                    count.rectTransform.At(Cx, 1000f, 300f, 18f);
                    if (_page > 0) Chrome.TextButton(_content, "PREVIOUS PAGE", Cx - 130f, 1000f, true, () => TurnPage(-1), "back");
                    if (!LastPage) Chrome.TextButton(_content, "NEXT PAGE", Cx + 130f, 1000f, false, () => TurnPage(1), "arrow");
                }
                return;
            }
            string prompt = !LastPage ? "Click or press Enter for the next page" : FromMenu ? "Click or press Enter to go back" : "Click or press Enter to begin";
            _prompt = UIKit.Text(_content, prompt, TextStyle.Label.WithSize(12).WithTracking(0.4f).WithColor(Palette.Faint));
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
            Header("READING A BIT", "A bit is 0 or 1. It's drawn as a line across the loop.");

            Example("binary 1", 720f, 318f, 240f, 60f);
            Caption(720f, 362f, "A STEADY 1", "Above the middle all loop long");
            Example("binary 0", 1200f, 318f, 240f, 60f);
            Caption(1200f, 362f, "A STEADY 0", "Below the middle all loop long");

            var big = _lead = Example("binary 0011", Cx, 640f, 760f, 140f, loop: true);
            var one = At(big, 0.75f, 1f);
            Note(one, new Vector2(one.x, 524f), Side.Above, "1: ABOVE THE MIDDLE", "A 1 sounds as a beep");
            var zero = At(big, 0.25f, 0f);
            Note(zero, new Vector2(zero.x, 766f), Side.Below, "0: BELOW THE MIDDLE", "A 0 is only a soft tap");
            var mid = At(big, 1f, 0.5f);
            Note(mid, new Vector2(mid.x + 70f, mid.y), Side.Right, "THE MIDDLE", "the faint line", 220f);
            LoopArrow(big, 862f);
        }

        /// <summary>Tally: a steady digit is a whole number, shown as just its number; a bigger one sounds higher.</summary>
        void BuildDigits()
        {
            Header("READING A DIGIT", "A digit is a whole number from 0 to 9. Here each one stays the same all loop long.");

            _lead = Example("decimal 7", Cx, 420f, 240f, 120f);
            Caption(Cx, 492f, "A STEADY DIGIT", "Shown as just its number, and heard as one short tone", 520f);

            Section(Cx, 632f, "A bigger digit sounds higher");
            int[] digits = { 0, 3, 6, 9 };
            for (int i = 0; i < digits.Length; i++)
            {
                float x = Cx + (i - 1.5f) * 260f;
                Example($"decimal {digits[i]}", x, 716f, 150f, 72f);
                if (i == 0) Caption(x, 768f, null, "The lowest tone", 240f);
                if (i == digits.Length - 1) Caption(x, 768f, null, "The highest tone", 240f);
            }
        }

        /// <summary>Ticker: a digit that changes is a line, its height the digit, 0 at the bottom and 9 at the top.</summary>
        void BuildChangingDigits()
        {
            Header("DIGITS THAT CHANGE", "A digit can change as the loop plays. Then it's drawn as a line, one step for each digit.");

            Example("decimal 7", 600f, 318f, 150f, 72f);
            Caption(600f, 370f, "A STEADY DIGIT", "Shown as just its number");
            Example("decimal 012345678", 1320f, 318f, 280f, 64f);
            Caption(1320f, 370f, "A digit with many steps", "Only the highest and lowest are written, at the left", 380f);

            var big = _lead = Example("decimal 2709", Cx, 640f, 760f, 170f, loop: true);
            // A scale beside the line, from 0 at the bottom to 9 at the top.
            var bottom = At(big, 0f, 0f);
            var top = At(big, 0f, 9f);
            float x = bottom.x - 28f;
            var g = NoteGroup();
            Line(g, new Vector2(x + 9f, top.y), new Vector2(x, top.y), new Vector2(x, bottom.y), new Vector2(x + 9f, bottom.y));
            Small(g, "9", x - 12f, top.y);
            Small(g, "0", x - 12f, bottom.y);
            Words(g, new Vector2(x - 22f, (top.y + bottom.y) * 0.5f), Side.Left, "HEIGHT IS THE DIGIT", "0 at the bottom, 9 at the top. A bigger digit sounds higher.", 300f);
            Reveal(g);

            // The number written on a step: point at the last one, 9, from the right.
            if (big.StepNumbers.Count > 0)
            {
                var nine = big.StepNumbers[big.StepNumbers.Count - 1];
                var at = FromView(big, nine.rectTransform.anchoredPosition + new Vector2(nine.fontSize * 0.5f, 0f));
                var (_, right) = big.LineSpan;
                Note(at, new Vector2(FromView(big, new Vector2(right, 0f)).x + 40f, at.y), Side.Right, "EACH STEP'S DIGIT", "is written on it", 240f);
            }
            LoopArrow(big, 790f);
        }

        /// <summary>Resonance: one cycle and F, the height and A, the three forms, and a wave that starts late.</summary>
        void BuildWaves()
        {
            Header("READING A WAVE", "A wave rises and falls about the middle.");

            var big = _lead = Example("wave sine f2 a5", Cx, 470f, 760f, 170f, loop: true);
            var (left, right) = big.LineSpan;

            // One cycle, bracketed above the line.
            var start = At(big, 0f, 7f);
            var half = At(big, 0.5f, 7f);
            float y = 470f - 85f - 12f;
            var g = NoteGroup();
            Line(g, new Vector2(start.x, y + 8f), new Vector2(start.x, y), new Vector2(half.x, y), new Vector2(half.x, y + 8f));
            Words(g, new Vector2((start.x + half.x) * 0.5f, y), Side.Above, "ONE CYCLE",
                "F2: it repeats twice a loop. The higher F is, from 1 to 7, the faster it repeats and the higher it sounds.", 480f);
            Reveal(g);

            // The height, measured from the middle to a peak, beside the line.
            var peak = At(big, 0.625f, 5f);
            var middle = At(big, 1f, 0f);
            float x = FromView(big, new Vector2(right, 0f)).x + 26f;
            g = NoteGroup();
            Line(g, LeaderColour.WithAlpha(0.35f), new Vector2(peak.x + 6f, peak.y), new Vector2(x - 12f, peak.y));
            Line(g, new Vector2(x - 9f, peak.y), new Vector2(x, peak.y), new Vector2(x, middle.y), new Vector2(x - 9f, middle.y));
            Words(g, new Vector2(x + 4f, (peak.y + middle.y) * 0.5f), Side.Right, "A5: 5 up from the middle",
                "And 5 down. The higher A is, from 1 to 9, the taller it is and the louder it sounds.", 300f);
            Reveal(g);

            // F and A are written under every wave, as here.
            var settings = UIKit.Text(_content, Waves.Describe(big.Trace), TextStyle.Label.WithSize(15).WithTracking(0.16f).WithColor(NumberColour));
            settings.raycastTarget = false;
            settings.rectTransform.At(Cx, 576f, 200f, 20f);
            float w = settings.MeasureWidth();
            Note(new Vector2(Cx - w * 0.5f - 10f, 576f), new Vector2(Cx - w * 0.5f - 60f, 576f), Side.Left, "Written under every wave", null, 360f, dot: false);

            // The three forms, each its own sound.
            Section(760f, 676f, "Three forms, each with its own sound");
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
            Caption(1440f, 800f, "A quarter cycle late", "A wave can also start half or three quarters of a cycle late. The faint line starts on time.", 340f);
        }

        /// <summary>
        /// Resonance, second page: the same wave at rising frequencies, then at rising
        /// amplitudes, so the player can see and hear what F and A each change.
        /// </summary>
        void BuildWaveSettings()
        {
            Header("Frequency and amplitude", "F and A are written under every wave. Click any wave here to hear it.");

            Section(Cx, 330f, "Frequency: F changes how often it repeats");
            (int f, string sub)[] freqs = { (1, "Once a loop. The lowest pitch"), (3, "3 times a loop"), (5, "6 times a loop"), (7, "12 times a loop. The highest pitch") };
            for (int i = 0; i < freqs.Length; i++)
            {
                float x = Cx + (i - 1.5f) * 380f;
                var view = Example($"wave sine f{freqs[i].f} a6", x, 420f, 300f, 96f, loop: true);
                if (i == 0) _lead = view;
                Caption(x, 482f, $"F{freqs[i].f}", freqs[i].sub, 320f);
            }

            Section(Cx, 640f, "Amplitude: A changes how tall it is");
            (int a, string sub)[] amps = { (1, "1 up and down. The quietest"), (3, "3 up and down"), (6, "6 up and down"), (9, "9 up and down. The loudest") };
            for (int i = 0; i < amps.Length; i++)
            {
                float x = Cx + (i - 1.5f) * 380f;
                Example($"wave sine f2 a{amps[i].a}", x, 740f, 300f, 120f, loop: true);
                Caption(x, 812f, $"A{amps[i].a}", amps[i].sub, 320f);
            }
        }

        /// <summary>
        /// Resonance, third page: the same two waves wired into a block's two
        /// inputs one way round and then the other. It shows no block's rule and
        /// no result, only that the two wirings are different and why that can
        /// matter.
        /// </summary>
        void BuildInputOrder()
        {
            Header("WHICH WAY ROUND", "A block with two inputs has two dots on its left, a top one and a bottom one. The same two wires can go in either way round.");

            float[] centres = { 613f, 1433f };
            const float topY = 380f, bottomY = 520f, waveW = 180f, waveH = 56f;
            for (int d = 0; d < 2; d++)
            {
                bool swapped = d == 1;
                float cx = centres[d];
                float waveX = cx - 230f;
                Example("wave sine f1 a6", waveX, topY, waveW, waveH);
                Example("wave square f3 a4", waveX, bottomY, waveW, waveH);
                Letter(waveX - waveW * 0.5f - 22f, topY, "A");
                Letter(waveX - waveW * 0.5f - 22f, bottomY, "B");

                var g = NoteGroup();
                var block = new Vector2(cx + 60f, (topY + bottomY) * 0.5f);
                const float bw = 150f, bh = 88f;
                var topPort = new Vector2(block.x - bw * 0.5f, block.y - 22f);
                var bottomPort = new Vector2(block.x - bw * 0.5f, block.y + 22f);
                var outPort = new Vector2(block.x + bw * 0.5f, block.y);
                float from = waveX + waveW * 0.5f + 8f;
                var aTo = swapped ? bottomPort : topPort;
                var bTo = swapped ? topPort : bottomPort;
                Color wire = Palette.SignalAtX(from).WithAlpha(0.75f);
                Line(g, wire, new Vector2(from, topY), new Vector2(cx - 80f, topY), new Vector2(cx - 80f, aTo.y), aTo);
                Line(g, wire, new Vector2(from, bottomY), new Vector2(cx - 55f, bottomY), new Vector2(cx - 55f, bTo.y), bTo);
                Line(g, wire, outPort, outPort + new Vector2(46f, 0f));

                var box = UIKit.Shape(g, "Block");
                box.rectTransform.At(block.x, block.y, bw, bh);
                box.Radius = 16f;
                box.Style(Paint.Solid, Color.black, Paint.Solid, Palette.Dim, 1.2f);
                var label = UIKit.Text(g, "W1", TextStyle.Value.WithSize(28).WithWeight(200).WithTracking(0.08f).WithColor(Palette.Dim));
                label.raycastTarget = false;
                label.rectTransform.At(block.x, block.y + 3f, bw, bh * 0.6f);
                foreach (var port in new[] { topPort, bottomPort, outPort })
                {
                    var dot = UIKit.Shape(g, "Port", ShapeKind.Circle);
                    dot.Style(Paint.Solid, Palette.Text, Paint.None, Color.clear, 0f);
                    dot.rectTransform.At(port.x, port.y, 7f, 7f);
                }
                var unknown = UIKit.Text(g, "?", TextStyle.Value.WithSize(30).WithWeight(200).WithColor(Palette.Dim));
                unknown.raycastTarget = false;
                unknown.rectTransform.At(outPort.x + 66f, block.y, 30f, 40f);
                Reveal(g);

                Caption(cx, 600f, swapped ? "B ON TOP, A BELOW" : "A ON TOP, B BELOW", null);
            }
            var or = UIKit.Text(_content, "OR", TextStyle.Label.WithSize(14).WithTracking(0.3f).WithColor(Palette.Faint));
            or.raycastTarget = false;
            or.rectTransform.At(Cx, (topY + bottomY) * 0.5f, 60f, 20f);

            Caption(centres[0], 690f, "Some blocks don't mind", "They treat both inputs alike, so either way round sends the same.", 420f);
            Caption(centres[1], 690f, "OTHERS DO", "Each input has its own job, so swapping the wires changes what comes out.", 420f);
            Caption(Cx, 830f, "Close, but not right?", "Try the same wires the other way round. Click a wire to take it off, then drag a new one to the other dot.", 760f);
        }

        /// <summary>An input's letter, beside its signal.</summary>
        void Letter(float x, float y, string letter)
        {
            var t = UIKit.Text(_content, letter, TextStyle.Label.WithSize(16).WithTracking(0.2f).WithColor(Palette.Text));
            t.raycastTarget = false;
            t.rectTransform.At(x, y, 30f, 22f);
        }

        /// <summary>The mixed chapters: one signal of each kind, as a block of each kind reads it.</summary>
        void BuildReadings()
        {
            Header("How a block reads each kind", "A block's letter says what it sends: B a bit, D a digit, W a wave. It reads every signal as that kind.");

            var kinds = new[] { Medium.Binary, Medium.Decimal, Medium.Wave };
            string[] names = { "A BIT", "A DIGIT", "A WAVE" };
            string[] blocks = { "A bit block (B) sees", "A digit block (D) sees", "A wave block (W) sees" };
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
                    Caption(columns[c], y + h * 0.5f + 8f, null, same ? "The same: it's already this kind" : how, 330f);
                }
            }
        }

        /// <summary>The last chapters: all three kinds side by side, with what to look for in each.</summary>
        void BuildRecap()
        {
            Header("Every kind of signal", Chapter.Signals);

            var columns = new (string title, string example, string[] lines)[]
            {
                ("BITS", "binary 0110", new[]
                {
                    "Above the middle is 1, below is 0.",
                    "A 1 beeps; a 0 is a soft tap.",
                    "A bit block (B1, B2...) only ever sends 0 or 1. It reads a digit as 1 unless it's 0, and a wave as 1 while it's above the middle.",
                }),
                ("DIGITS", "decimal 1357", new[]
                {
                    "Height is the digit: 0 at the bottom, 9 at the top. A steady digit is just its number.",
                    "A bigger digit sounds higher.",
                    "A digit block (D1, D2...) only ever sends 0 to 9. It reads a bit as 0 or 1, and a wave by its height above the middle: A7 peaks at 7.",
                }),
                ("WAVES", "wave sine f2 a5", new[]
                {
                    "F is how often it repeats, and how high it sounds. A is how tall it is, and how loud.",
                    "A wave can start a quarter, half or three quarters of a cycle late.",
                    "A wave block (W1, W2...) always sends a wave, with its own F and A. It reads a bit as the top or the bottom, and a digit as a level.",
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

        /// <summary>
        /// Earshot: a listen-only block as the board draws it, a speaker beside its
        /// label and a speaker over it in place of its signal; then a chain from it
        /// to an output, every signal a speaker, since everything it feeds is only
        /// heard too until the circuit is solved; then what can and can't be heard
        /// in each kind of signal.
        /// </summary>
        void BuildListening()
        {
            Header("LISTEN-ONLY BLOCKS", "Some blocks never show what they send. You can only hear it.");

            // The block, as on the board.
            const float bx = 560f, by = 460f, bw = 150f, bh = 88f;
            Reveal(Card(bx, by, bw, bh, "W1", listen: true, ports: 2));
            // What it sent on the run: a speaker that plays it.
            _lead = Heard("wave saw f2 a5", bx, by - bh * 0.5f - 30f, 118f, 34f);
            Note(new Vector2(bx - 44f, by + 3f), new Vector2(bx - 150f, by + 3f), Side.Left, "THE MARK", "A speaker beside the label", 240f, dot: false);
            Note(new Vector2(bx + 30f, by - bh * 0.5f - 30f), new Vector2(bx + 140f, by - bh * 0.5f - 30f), Side.Right, "WHAT IT SENDS", "After a run, only a speaker. Click it to hear the signal.", 300f, dot: false);

            // Everything wired after it is only heard too: a block with no mark, and an output.
            Section(bx, 590f, "EVERYTHING AFTER IT");
            const float cy = 712f, cw = 112f, ch = 64f, step = 210f;
            var chain = new (string label, bool listen, int ports, string spec)[]
            {
                ("W1", true, 2, "wave saw f2 a5"),
                ("D1", false, 2, "decimal 2709"),
                ("X", false, 1, "decimal 2709"),
            };
            var wires = NoteGroup();
            for (int i = 0; i < chain.Length - 1; i++)
            {
                float x0 = bx + (i - 1) * step + cw * 0.5f, x1 = bx + i * step - cw * 0.5f;
                Line(wires, Palette.Faint, new Vector2(x0, cy), new Vector2(x1, cy - (chain[i + 1].ports == 2 ? 17f : 0f)));
            }
            Reveal(wires);
            for (int i = 0; i < chain.Length; i++)
            {
                float x = bx + (i - 1) * step;
                Reveal(Card(x, cy, cw, ch, chain[i].label, chain[i].listen, chain[i].ports, output: i == chain.Length - 1));
                Heard(chain[i].spec, x, cy - ch * 0.5f - 24f, 96f, 28f);
            }
            Caption(bx, cy + ch * 0.5f + 34f, "Only heard too", "Every block and output it feeds, however far on, shows only a speaker, mark or not. Solve the circuit and every signal shows.", 560f);

            // How each kind sounds.
            const float cx = 1340f;
            Section(cx, 300f, "WHAT YOU CAN HEAR");
            var rows = new (string spec, string title, string detail)[]
            {
                ("binary 0110", "BITS", "A 1 beeps and a 0 taps, in turn"),
                ("decimal 2709", "DIGITS", "A bigger digit sounds higher"),
                ("wave sine f3 a7", "WAVES", "Higher the bigger F is, louder the bigger A is. Each form has its own sound."),
            };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 370f + 112f * i;
                Example(rows[i].spec, cx - 190f, y, 210f, 56f);
                var t = NoteGroup();
                Words(t, new Vector2(cx - 60f, y), Side.Right, rows[i].title, rows[i].detail, 360f);
                Reveal(t);
            }
            Section(cx, 724f, "WHAT YOU CAN'T HEAR");
            Example("wave sine f1 a6", cx - 190f, 800f, 210f, 56f);
            Example("wave sine f1 a6 p1", cx - 190f, 880f, 210f, 56f);
            var u = NoteGroup();
            Words(u, new Vector2(cx - 60f, 812f), Side.Right, "WHEN A WAVE STARTS", "Or which way up it is. These two sound the same, so listen to what a block does further on.", 360f);
            Reveal(u);
        }

        /// <summary>
        /// Lights Out: nothing is drawn, so the page says how a circuit is played by
        /// ear, with speakers to try, and what can't be heard.
        /// </summary>
        void BuildDark()
        {
            Header("BY EAR ALONE", "In this chapter no signal is drawn: not the inputs, the blocks, the outputs or the targets. Each one is a speaker. Click it to hear it.");

            var steps = new (string spec, string title, string detail)[]
            {
                ("decimal 8655", "1  Listen to the targets", "Each output's target plays what it wants."),
                ("binary 0110", "2  Listen to the inputs", "A beat of bits, a tune of digits, or a wave's tone. Work out which is which."),
                ("decimal 1357", "3  WIRE, THEN RUN", "Each output plays what came out. The sign beside it says whether it matches its target."),
            };
            for (int i = 0; i < steps.Length; i++)
            {
                float x = 500f + 460f * i;
                var view = Heard(steps[i].spec, x, 400f, 140f, 64f);
                if (i == 0) _lead = view;
                Caption(x, 470f, steps[i].title, steps[i].detail, 380f);
            }

            Section(Cx, 690f, "WHAT YOU CAN'T HEAR");
            Caption(Cx, 726f, null, "When a wave starts, or which way up it is. Every circuit here is built so that when an output sounds like its target, it is its target.", 820f);
            Caption(Cx, 830f, "Listen as often as you like", "A signal plays each time you click it, and the pen still works for notes.", 820f);
        }

        /// <summary>
        /// A block's card as the board draws it, centred at (x, y): its label, a
        /// speaker beside it on a listen-only block, a dot for each input and one for
        /// what it sends. An output's card has its input only.
        /// </summary>
        RectTransform Card(float x, float y, float w, float h, string label, bool listen, int ports, bool output = false)
        {
            var g = NoteGroup();
            var box = UIKit.Shape(g, "Block");
            box.rectTransform.At(x, y, w, h);
            box.Radius = h * 0.18f;
            box.Style(Paint.Solid, Color.black, Paint.Solid, Palette.Faint, 1.4f);
            float size = h * 0.32f;
            var text = UIKit.Text(g, label, TextStyle.Value.WithSize(Mathf.RoundToInt(size)).WithWeight(200).WithTracking(0.08f).WithColor(Palette.Dim));
            text.raycastTarget = false;
            float shift = listen ? size * 0.6f : 0f;
            text.rectTransform.At(x + shift, y + 3f, w, h * 0.55f);
            if (listen)
            {
                var mark = UIKit.Glyph(g, "sound", Palette.Dim, 1.4f, "Mark");
                mark.raycastTarget = false;
                mark.rectTransform.At(x - shift - size * 0.55f, y + 3f, size * 0.8f, size * 0.8f);
            }
            var dots = new List<Vector2>();
            if (ports == 1) dots.Add(new Vector2(x - w * 0.5f, y));
            else { dots.Add(new Vector2(x - w * 0.5f, y - h * 0.27f)); dots.Add(new Vector2(x - w * 0.5f, y + h * 0.27f)); }
            if (!output) dots.Add(new Vector2(x + w * 0.5f, y));
            foreach (var port in dots)
            {
                var dot = UIKit.Shape(g, "Port", ShapeKind.Circle);
                dot.Style(Paint.Solid, Palette.Text, Paint.None, Color.clear, 0f);
                dot.rectTransform.At(port.x, port.y, 7f, 7f);
            }
            return g;
        }

        /// <summary>A signal shown only as a speaker, as a listen-only block or a dark circuit shows it, that plays when clicked.</summary>
        SignalView Heard(string spec, float x, float y, float w, float h)
        {
            var view = SignalView.Create(_content, w, h, "Heard");
            view.GetComponent<RectTransform>().At(x, y, w, h);
            view.HeardOnly = true;
            view.Set(Signals.Parse(spec).Trace, ValueLook.Live);
            view.MakePlayable(() => Play(view));
            return view;
        }

        // ------------------------------------------------------------------ pieces

        void Header(string heading, string sub)
        {
            string page = _pages.Count > 1 ? $"  ·  PAGE {_page + 1} OF {_pages.Count}" : "";
            var where = UIKit.Text(_content, $"CHAPTER {Chapter.Numeral}  ·  {Chapter.Title}  ·  SIGNALS{page}", TextStyle.Label.WithSize(13).WithTracking(0.4f).WithColor(Palette.Faint));
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
            App.Audio?.PlayTrace(view.Trace, App.LoopSpeed);
            view.Ping();
            var run = _loops.Find(l => l.View == view);
            if (run == null) _loops.Add(run = new Loop { View = view, Seconds = SecondsFor(view.Trace), Once = true });
            // Every playhead on the page starts again with the sound.
            foreach (var l in _loops) l.Start = Time.unscaledTime;
        }

        /// <summary>
        /// A pattern of bits or digits sounds each value in turn over a set time, so
        /// its playhead keeps that time and lands on each value as it sounds. A wave
        /// sounds as a tone, so its playhead keeps the board's time.
        /// </summary>
        static float SecondsFor(Trace trace) =>
            (trace != null && !Media.IsWave(trace.Medium) && !trace.IsFlat ? SignalSound.PatternLength : WiringBoard.LoopSeconds) / App.LoopSpeed;

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
            Words(g, new Vector2((x0 + x1) * 0.5f, y), Side.Below, "ONE LOOP", "It plays left to right, then starts again, over and over", 640f);
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

        /// <summary>Goes on to the next page, or closes after the last.</summary>
        void TryDismiss()
        {
            if (Time.unscaledTime - _shownAt < 0.6f) return;
            if (!LastPage)
            {
                App.Sound(Sfx.Click);
                _page++;
                BuildPage();
                return;
            }
            App.Sound(Sfx.Close);
            Close();
        }

        /// <summary>Closes at once, skipping any pages left.</summary>
        void TryClose()
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
            if (GameInput.Down(Key.Escape)) TryClose();
            else if (GameInput.Confirm) TryDismiss();
        }
    }
}
