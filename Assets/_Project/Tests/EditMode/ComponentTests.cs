using System.Collections.Generic;
using System.Linq;
using Blackbox.Components;
using NUnit.Framework;

namespace Blackbox.Tests
{
    /// <summary>The block kinds, the five signal media, their traces over a run, and their sounds.</summary>
    public class ComponentTests
    {
        /// <summary>The kinds that work one value at a time: bits, digits and shapes.</summary>
        static IEnumerable<BlockKind> ValueKinds() => KindCatalog.All.Where(k => !k.Mixing && !Media.IsWave(k.Medium));

        static int Apply(string kind, params int[] inputs) => KindCatalog.Get(kind).Apply(inputs);

        /// <summary>Every combination of input values a kind can be given.</summary>
        static IEnumerable<int[]> InputsFor(BlockKind kind)
        {
            int size = Media.Size(kind.Medium);
            if (kind.InputCount == 1)
                for (int a = 0; a < size; a++) yield return new[] { a };
            else
                for (int a = 0; a < size; a++)
                    for (int b = 0; b < size; b++) yield return new[] { a, b };
        }

        [Test]
        public void EachSignalKindHasFourBlocks()
        {
            foreach (Medium m in new[] { Medium.Binary, Medium.Decimal, Medium.Shape, Medium.Wave })
                Assert.That(KindCatalog.All.Count(k => !k.Mixing && k.Medium == m), Is.EqualTo(4), m.ToString());
            Assert.That(KindCatalog.All.Count(k => !k.Mixing && k.Medium == Medium.Sampled), Is.Zero, "stepped waves are set aside");
        }

        [Test]
        public void EveryBlockHasOneFixedRule()
        {
            foreach (var kind in KindCatalog.All)
            {
                Assert.That(kind.StateCount, Is.EqualTo(1), kind.Id);
                Assert.That(kind.InputCount, Is.InRange(1, 2), kind.Id);
                Assert.That(KindCatalog.Get(kind.Id), Is.SameAs(kind));
                Assert.That(kind.Rule, Is.Not.Empty, kind.Id);
            }
        }

        [Test]
        public void ShortRulesFitUnderABlock()
        {
            // Once a circuit is solved its blocks show their rules on the board, in at most two short lines.
            foreach (var kind in KindCatalog.All)
            {
                Assert.That(kind.ShortRule, Is.Not.Empty, kind.Id);
                Assert.That(kind.ShortRule.Length, Is.LessThanOrEqualTo(45), $"{kind.Id}: \"{kind.ShortRule}\"");
            }
        }

        [Test]
        public void MixingKindsAreWellFormed()
        {
            var mixing = KindCatalog.All.Where(k => k.Mixing).Select(k => k.Id).ToList();
            Assert.That(mixing, Is.EquivalentTo(new[] { "GATE", "COUNT", "COMPARE", "STEPS" }));
            foreach (string id in mixing)
            {
                var kind = KindCatalog.Get(id);
                Assert.Throws<System.InvalidOperationException>(() => kind.Apply(new int[kind.InputCount]), id);
            }
            Assert.That(KindCatalog.Get("STEPS").Shelved, Is.True);
            Assert.That(KindCatalog.All.Where(k => k.Shelved).Select(k => k.Id), Is.EquivalentTo(new[] { "FILL", "NEXT", "TAKEFILL", "TAKECOLOUR", "STEPS" }), "shapes and stepped waves are set aside");
            Assert.That(KindCatalog.MediumShelved(Medium.Shape) && KindCatalog.MediumShelved(Medium.Sampled), Is.True);
            Assert.That(KindCatalog.MediumShelved(Medium.Binary) || KindCatalog.MediumShelved(Medium.Decimal) || KindCatalog.MediumShelved(Medium.Wave), Is.False);
        }

        [TestCaseSource(nameof(ValueKinds))]
        public void KindAlwaysSendsAValueOfItsMedium(BlockKind kind)
        {
            foreach (var inputs in InputsFor(kind))
            {
                int v = kind.Apply(inputs);
                Assert.That(Media.IsValid(kind.Medium, v), Is.True, $"{kind.Id} on [{string.Join(",", inputs)}] gave {v}");
            }
        }

        [TestCaseSource(nameof(ValueKinds))]
        public void KindIsNeverConstant(BlockKind kind)
        {
            // A block that ignores its inputs couldn't be worked out by wiring it.
            int first = kind.Apply(InputsFor(kind).First());
            Assert.That(InputsFor(kind).Any(x => kind.Apply(x) != first), Is.True, kind.Id);
        }

        [TestCaseSource(nameof(ValueKinds))]
        public void EveryInputMatters(BlockKind kind)
        {
            // Changing either input can change what the block sends, so no wire is ever wasted.
            for (int p = 0; p < kind.InputCount; p++)
            {
                bool matters = InputsFor(kind).Any(x =>
                {
                    var y = (int[])x.Clone();
                    y[p] = (y[p] + 1) % Media.Size(kind.Medium);
                    return kind.Apply(x) != kind.Apply(y);
                });
                Assert.That(matters, Is.True, $"{kind.Id} input {p}");
            }
        }

        [Test]
        public void BlocksSayHowTheyReadEachInput()
        {
            // The board tells the player how a block reads a wire of another kind.
            foreach (var kind in KindCatalog.All.Where(k => !k.Mixing))
                for (int p = 0; p < kind.InputCount; p++)
                    Assert.That(kind.Reads(p), Is.EqualTo(kind.Medium), $"{kind.Id} input {p}");
            Assert.That(KindCatalog.Get("GATE").Reads(0), Is.Null, "GATE passes its top input as it is");
            Assert.That(KindCatalog.Get("GATE").Reads(1), Is.EqualTo(Medium.Binary));
            Assert.That(KindCatalog.Get("COUNT").Reads(0), Is.EqualTo(Medium.Binary));
            Assert.That(KindCatalog.Get("COMPARE").Reads(0), Is.EqualTo(Medium.Wave));
            Assert.That(KindCatalog.Get("COMPARE").Reads(1), Is.EqualTo(Medium.Wave));

            var kinds = new[] { Medium.Binary, Medium.Decimal, Medium.Shape, Medium.Wave };
            foreach (var from in kinds)
                foreach (var to in kinds)
                {
                    string how = Media.HowRead(from, to);
                    if (from == to) Assert.That(how, Is.Null);
                    else Assert.That(how, Is.Not.Empty, $"{from} as {to}");
                }
            Assert.That(Media.HowRead(Medium.Wave, Medium.Decimal), Does.Contain("9 at the top"));
            Assert.That(Media.HowRead(Medium.Shape, Medium.Decimal), Does.Contain("triangle 3"));
            Assert.That(Trace.Of(Medium.Shape, Shapes.Make(Shapes.Triangle, false)).As(Medium.Decimal)[0], Is.EqualTo(3f), "as the note says");
        }

        [Test]
        public void BitRules()
        {
            Assert.That(Apply("NOT", 1), Is.EqualTo(0));
            Assert.That(Apply("NOT", 0), Is.EqualTo(1));
            Assert.That(Apply("AND", 1, 0), Is.EqualTo(0));
            Assert.That(Apply("AND", 1, 1), Is.EqualTo(1));
            Assert.That(Apply("OR", 1, 0), Is.EqualTo(1));
            Assert.That(Apply("OR", 0, 0), Is.EqualTo(0));
            Assert.That(Apply("XOR", 1, 1), Is.EqualTo(0));
            Assert.That(Apply("XOR", 0, 1), Is.EqualTo(1));
        }

        [Test]
        public void DigitRulesNeverWrap()
        {
            Assert.That(Apply("MIRROR", 2), Is.EqualTo(7));
            Assert.That(Apply("MIRROR", 9), Is.EqualTo(0));
            Assert.That(Apply("MAX", 3, 8), Is.EqualTo(8));
            Assert.That(Apply("MIN", 3, 8), Is.EqualTo(3));
            Assert.That(Apply("DIFF", 2, 7), Is.EqualTo(5));
            Assert.That(Apply("DIFF", 7, 2), Is.EqualTo(5), "the same either way round");
            Assert.That(Apply("DIFF", 4, 4), Is.EqualTo(0));
        }

        [Test]
        public void ShapeRules()
        {
            int hollowCircle = Shapes.Make(0, false), solidCircle = Shapes.Make(0, true);
            int pinkTriangle = Shapes.Make(1, true, 1), hollowAmberSquare = Shapes.Make(2, false, 2), solidGreenHexagon = Shapes.Make(3, true, 3);
            Assert.That(Apply("FILL", hollowCircle), Is.EqualTo(solidCircle));
            Assert.That(Apply("FILL", pinkTriangle), Is.EqualTo(Shapes.Make(1, false, 1)), "keeps form and colour");
            Assert.That(Apply("NEXT", pinkTriangle), Is.EqualTo(Shapes.Make(2, true, 1)), "triangle to square, keeping fill and colour");
            Assert.That(Apply("NEXT", solidGreenHexagon), Is.EqualTo(Shapes.Make(0, true, 3)), "hexagon round to circle");
            Assert.That(Apply("TAKEFILL", hollowAmberSquare, pinkTriangle), Is.EqualTo(Shapes.Make(2, true, 2)), "top's form and colour, bottom's fill");
            Assert.That(Apply("TAKECOLOUR", hollowAmberSquare, pinkTriangle), Is.EqualTo(Shapes.Make(2, false, 1)), "top's form and fill, bottom's colour");
        }

        static Trace WaveOf(int form, int frequency, int height, int phase = 0) => Trace.Of(Medium.Wave, Waves.Make(form, frequency, height, phase));
        static Trace Run(string kind, params Trace[] inputs) => KindCatalog.Get(kind).Apply(inputs);

        [Test]
        public void WaveRules()
        {
            var sine = WaveOf(Waves.Sine, 0, 6);
            Assert.That(Run("FLIP", sine).Matches(WaveOf(Waves.Sine, 0, 6, 2)), Is.True, "a sine upside down is a sine half a cycle late");
            Assert.That(Run("FASTER", sine).Matches(WaveOf(Waves.Sine, 1, 6)), Is.True, "twice as fast");
            Assert.That(Run("FASTER", WaveOf(Waves.Saw, 2, 3)).Matches(WaveOf(Waves.Saw, 4, 3)), Is.True, "3 a loop becomes 6");
            Assert.That(Run("LATER", sine).Matches(WaveOf(Waves.Sine, 0, 6, 1)), Is.True, "a quarter of the loop later");
            Assert.That(Run("MIX", sine, Run("FLIP", sine)).Matches(Trace.Flat(Medium.Wave, 0)), Is.True, "a wave and its upside-down twin mix to a flat line");
            Assert.That(Run("MIX", sine, sine).Matches(sine), Is.True, "halfway between a wave and itself is the wave");
        }

        [Test]
        public void WaveBlocksReadAnySignalAsAWave()
        {
            var bit = Trace.Of(Medium.Binary, 1);
            var flipped = Run("FLIP", bit);
            Assert.That(flipped.Medium, Is.EqualTo(Medium.Wave));
            Assert.That(flipped[0], Is.EqualTo(-7f), "a 1 is a wave at the top, so upside down it's at the bottom");
            Assert.That(Run("MIX", Trace.Of(Medium.Decimal, 9), Trace.Of(Medium.Binary, 0))[0], Is.EqualTo(0f), "9 is the top, a 0 bit the bottom");
        }

        [Test]
        public void ValuesReadBackFromTheirDescriptions()
        {
            foreach (Medium m in AllMedia())
                foreach (int v in Media.All(m))
                {
                    string words = Media.Describe(m, v);
                    Assert.That(Media.TryParse(m, words, out int back), Is.True, $"{m}: '{words}'");
                    Assert.That(back, Is.EqualTo(v), $"{m}: '{words}'");
                }
            Assert.That(Media.TryParse(Medium.Binary, "2", out _), Is.False);
            Assert.That(Media.TryParse(Medium.Shape, "solid octagon", out _), Is.False);
        }

        [Test]
        public void WavesAreWrittenAsFormAndSettings()
        {
            Assert.That(Media.TryParse(Medium.Wave, "sine f3 a5", out int v), Is.True);
            Assert.That(v, Is.EqualTo(Waves.Make(Waves.Sine, 2, 4, 0)));
            Assert.That(Media.TryParse(Medium.Wave, "saw a7 p2 f1", out v), Is.True, "settings in any order");
            Assert.That(v, Is.EqualTo(Waves.Make(Waves.Saw, 0, 6, 2)));
            Assert.That(Media.TryParse(Medium.Sampled, "square f7 a1 s6", out v), Is.True);
            Assert.That(v, Is.EqualTo(Sampled.Make(Waves.SquareWave, 6, 0, 1)));
            Assert.That(Media.Describe(Medium.Wave, Waves.Make(Waves.Saw, 1, 3, 3)), Is.EqualTo("saw f2 a4 p3"));
            Assert.That(Media.Describe(Medium.Sampled, Sampled.Make(Waves.Sine, 1, 3, 2)), Is.EqualTo("sine f2 a4 s12"));
            Assert.That(Waves.Settings(Waves.Make(Waves.Sine, 0, 6, 1)), Is.EqualTo("F1 A7 +1/4"));
            Assert.That(Sampled.Settings(Sampled.Make(Waves.Sine, 0, 6, 0)), Is.EqualTo("F1 A7 S3"));

            foreach (var bad in new[] { "sine low", "sine f8 a1", "sine f1 a0", "sine f1", "sine f1 a1 p4", "sine f1 a1 s4", "sine f1 f2 a1", "hum f1 a1" })
                Assert.That(Media.TryParse(Medium.Wave, bad, out _), Is.False, bad);
            foreach (var bad in new[] { "sine f1 a1", "sine f1 a1 s4", "sine f1 a1 p1 s3" })
                Assert.That(Media.TryParse(Medium.Sampled, bad, out _), Is.False, bad);
        }

        [Test]
        public void EncodingsRoundTrip()
        {
            for (int f = 0; f < Shapes.Forms; f++)
                foreach (bool solid in new[] { false, true })
                {
                    int v = Shapes.Make(f, solid);
                    Assert.That(Shapes.Form(v), Is.EqualTo(f));
                    Assert.That(Shapes.Solid(v), Is.EqualTo(solid));
                }
            Assert.That(Media.Size(Medium.Wave), Is.EqualTo(3 * 7 * 7 * 4));
            foreach (int v in Media.All(Medium.Wave))
                Assert.That(Waves.Make(Waves.Form(v), Waves.Frequency(v), Waves.Height(v), Waves.Phase(v)), Is.EqualTo(v));
            Assert.That(Media.Size(Medium.Sampled), Is.EqualTo(3 * 7 * 7 * 3));
            foreach (int v in Media.All(Medium.Sampled))
                Assert.That(Sampled.Make(Sampled.Form(v), Sampled.Frequency(v), Sampled.Height(v), Sampled.Rate(v)), Is.EqualTo(v));
        }

        // ------------------------------------------------------------------ traces

        static int Step(double loop) => (int)System.Math.Round(loop * Trace.Steps);

        [Test]
        public void BitsDigitsAndShapesAreFlat()
        {
            foreach (Medium m in new[] { Medium.Binary, Medium.Decimal, Medium.Shape })
                foreach (int v in Media.All(m))
                {
                    var trace = Trace.Of(m, v);
                    Assert.That(trace.IsFlat, Is.True, $"{m} {v}");
                    Assert.That(trace[0], Is.EqualTo(v));
                }
        }

        [Test]
        public void WaveTracesFollowTheirSettings()
        {
            var sine = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 0, 6));
            Assert.That(sine[0], Is.EqualTo(0f).Within(1e-4f), "a sine starts at the middle");
            Assert.That(sine[Step(0.25)], Is.EqualTo(7f).Within(1e-4f), "and peaks a quarter of the way through one cycle");
            Assert.That(sine[Step(0.75)], Is.EqualTo(-7f).Within(1e-4f));

            var faster = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 1, 6));
            Assert.That(faster[Step(0.125)], Is.EqualTo(7f).Within(1e-4f), "frequency 2 fits two cycles in a loop");

            var shorter = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 0, 2));
            Assert.That(shorter[Step(0.25)], Is.EqualTo(3f).Within(1e-4f), "amplitude 3 peaks at 3");

            var late = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 0, 6, 1));
            Assert.That(late[0], Is.EqualTo(-7f).Within(1e-4f), "a quarter cycle late starts at the bottom");
            Assert.That(late[Step(0.5)], Is.EqualTo(7f).Within(1e-4f));

            for (int f = 0; f < Waves.Frequencies; f++)
            {
                var square = Trace.Of(Medium.Wave, Waves.Make(Waves.SquareWave, f, 4));
                int high = 0, edges = 0;
                for (int k = 0; k < Trace.Steps; k++)
                {
                    if (square[k] > 0) high++;
                    Assert.That(System.Math.Abs(square[k]), Is.EqualTo(5f), "a square is only ever at its top or bottom");
                    if (square[k] != square[(k + 1) % Trace.Steps]) edges++;
                }
                Assert.That(high, Is.EqualTo(Trace.Steps / 2), $"frequency {f + 1}: high exactly half the time");
                Assert.That(edges, Is.EqualTo(2 * Waves.Cycles[f]), $"frequency {f + 1}: one rise and one fall a cycle");
            }
        }

        [Test]
        public void AWaveAndItsHalfCycleTwinCancel()
        {
            foreach (int form in new[] { Waves.Sine, Waves.SquareWave })
                for (int f = 0; f < Waves.Frequencies; f++)
                {
                    var a = Trace.Of(Medium.Wave, Waves.Make(form, f, 6, 0));
                    var b = Trace.Of(Medium.Wave, Waves.Make(form, f, 6, 2));
                    for (int k = 0; k < Trace.Steps; k++)
                        Assert.That(a[k] + b[k], Is.EqualTo(0f).Within(1e-4f), $"{Waves.FormNames[form]} f{f + 1} at step {k}");
                }

            // A sine and a cosine (a quarter apart) don't cancel: they make a taller wave.
            var sine = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 0, 6, 0));
            var cosine = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 0, 6, 3));
            float peak = 0f;
            for (int k = 0; k < Trace.Steps; k++) peak = System.Math.Max(peak, System.Math.Abs(sine[k] + cosine[k]));
            Assert.That(peak, Is.EqualTo(7f * (float)System.Math.Sqrt(2)).Within(0.05f));
        }

        [Test]
        public void SampledWavesHoldWholeLevelsInSteps()
        {
            foreach (int v in Media.All(Medium.Sampled))
            {
                var trace = Trace.Of(Medium.Sampled, v);
                int height = Sampled.Height(v) + 1;
                int hold = Trace.Steps / (Waves.Cycles[Sampled.Frequency(v)] * Sampled.StepsPerCycle[Sampled.Rate(v)]);
                for (int k = 0; k < Trace.Steps; k++)
                {
                    Assert.That(trace[k], Is.EqualTo((float)System.Math.Round(trace[k])), $"{Media.Describe(Medium.Sampled, v)}: whole levels");
                    Assert.That(System.Math.Abs(trace[k]), Is.LessThanOrEqualTo(height), $"{Media.Describe(Medium.Sampled, v)}: within its height");
                    if (k % hold != 0) Assert.That(trace[k], Is.EqualTo(trace[k - 1]), $"{Media.Describe(Medium.Sampled, v)}: held between readings");
                }
            }

            // A sine read 6 times a cycle climbs in steps, then falls back through them.
            var stairs = Trace.Of(Medium.Sampled, Sampled.Make(Waves.Sine, 0, 6, 1));
            var levels = Enumerable.Range(0, 6).Select(i => stairs[i * Trace.Steps / 6]).ToArray();
            Assert.That(levels, Is.EqualTo(new[] { 4f, 7f, 4f, -4f, -7f, -4f }));

            // A saw read 3 times a cycle climbs three steps, then drops.
            var ramp = Trace.Of(Medium.Sampled, Sampled.Make(Waves.Saw, 0, 5, 0));
            Assert.That(Enumerable.Range(0, 3).Select(i => ramp[i * Trace.Steps / 3]).ToArray(), Is.EqualTo(new[] { -4f, 0f, 4f }));
        }

        [Test]
        public void TracesMatchOnlyTheirOwnValue()
        {
            foreach (Medium m in new[] { Medium.Binary, Medium.Decimal, Medium.Shape, Medium.Wave })
            {
                var values = Media.All(m).ToList();
                if (m == Medium.Wave) values = values.Where(v => Waves.Height(v) == 6 || Waves.Frequency(v) == 2).ToList();
                var traces = values.Select(v => Trace.Of(m, v)).ToList();
                for (int a = 0; a < traces.Count; a++)
                {
                    Assert.That(traces[a].Matches(Trace.Of(m, values[a])), Is.True);
                    for (int b = a + 1; b < traces.Count; b++)
                        Assert.That(traces[a].Matches(traces[b]), Is.False, $"{m}: {Media.Describe(m, values[a])} matches {Media.Describe(m, values[b])}");
                }
            }
            Assert.That(Trace.Of(Medium.Binary, 1).Matches(Trace.Of(Medium.Decimal, 1)), Is.False, "a bit is not a digit");
        }

        [Test]
        public void SampledWavesThatTraceTheSameLineAreTheSameSignal()
        {
            // A square is a square however often it's read, so targets accept any of them.
            int six = Sampled.Make(Waves.SquareWave, 3, 5, 1), twelve = Sampled.Make(Waves.SquareWave, 3, 5, 2);
            Assert.That(Media.Same(Medium.Sampled, six, twelve), Is.True);
            Assert.That(Media.Same(Medium.Sampled, six, Sampled.Make(Waves.SquareWave, 3, 4, 1)), Is.False, "a shorter square is different");
            Assert.That(Media.Same(Medium.Sampled, Sampled.Make(Waves.Sine, 0, 6, 0), Sampled.Make(Waves.Sine, 0, 6, 2)), Is.False, "a sine in 3 steps is not a sine in 12");
            Assert.That(Media.Same(Medium.Decimal, 3, 3), Is.True);
            Assert.That(Media.Same(Medium.Decimal, 3, 4), Is.False);

            foreach (int a in Media.All(Medium.Sampled))
                foreach (int b in Media.All(Medium.Sampled))
                    if (Media.Same(Medium.Sampled, a, b))
                        Assert.That(Sampled.Frequency(a) == Sampled.Frequency(b) || Sampled.Height(a) == 0, Is.True,
                            $"{Media.Describe(Medium.Sampled, a)} and {Media.Describe(Medium.Sampled, b)}: only the same frequency can look the same");
        }

        [Test]
        public void MixingReadsOneTypeAsAnother()
        {
            // A wave read as a bit pulses at the wave's frequency: 1 while it's above the middle.
            var pulses = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 2, 6)).As(Medium.Binary);
            Assert.That(pulses.Medium, Is.EqualTo(Medium.Binary));
            int rises = 0, ones = 0;
            for (int k = 0; k < Trace.Steps; k++)
            {
                if (pulses[k] == 1f) ones++;
                if (pulses[k] == 1f && pulses[(k + Trace.Steps - 1) % Trace.Steps] == 0f) rises++;
            }
            Assert.That(rises, Is.EqualTo(3), "three cycles, three pulses");
            Assert.That(ones, Is.EqualTo(Trace.Steps / 2 - 3), "high while above the middle, which leaves out the middle itself");

            Assert.That(Trace.Of(Medium.Decimal, 0).As(Medium.Binary)[0], Is.EqualTo(0f));
            Assert.That(Trace.Of(Medium.Decimal, 6).As(Medium.Binary)[0], Is.EqualTo(1f));
            Assert.That(Trace.Of(Medium.Shape, Shapes.Make(Shapes.Triangle, true)).As(Medium.Binary)[0], Is.EqualTo(1f));
            Assert.That(Trace.Of(Medium.Shape, Shapes.Make(Shapes.Hexagon, false)).As(Medium.Decimal)[0], Is.EqualTo(6f), "a shape as a digit is its corners");
            Assert.That(Trace.Of(Medium.Binary, 1).As(Medium.Shape)[0], Is.EqualTo((float)Shapes.Make(Shapes.Circle, true)));
            Assert.That(Trace.Of(Medium.Binary, 1).As(Medium.Wave)[0], Is.EqualTo(7f));
            Assert.That(Trace.Of(Medium.Binary, 0).As(Medium.Wave)[0], Is.EqualTo(-7f));
            Assert.That(Trace.Of(Medium.Decimal, 9).As(Medium.Wave)[0], Is.EqualTo(7f));
            Assert.That(Trace.Of(Medium.Decimal, 0).As(Medium.Wave)[0], Is.EqualTo(-7f));

            var sine = Trace.Of(Medium.Wave, Waves.Make(Waves.Sine, 0, 6));
            var digits = sine.As(Medium.Decimal);
            Assert.That(digits[Step(0.25)], Is.EqualTo(9f), "the top reads 9");
            Assert.That(digits[Step(0.75)], Is.EqualTo(0f), "the bottom reads 0");
            var rounded = sine.As(Medium.Sampled);
            for (int k = 0; k < Trace.Steps; k++)
            {
                Assert.That(rounded[k], Is.EqualTo((float)System.Math.Round(rounded[k])));
                Assert.That(rounded[k], Is.EqualTo(sine[k]).Within(0.5f));
            }
            Assert.That(sine.As(Medium.Wave), Is.SameAs(sine));
        }

        // ------------------------------------------------------------------ sounds

        static IEnumerable<Medium> AllMedia() => new[] { Medium.Binary, Medium.Decimal, Medium.Shape, Medium.Wave, Medium.Sampled };

        /// <summary>
        /// Values that ought to sound different from each other. Phase can't be heard
        /// (a wave sounds the same whenever it starts), so analog waves count once.
        /// </summary>
        static IEnumerable<int> Audible(Medium m) => m == Medium.Wave ? Media.All(m).Where(v => Waves.Phase(v) == 0) : Media.All(m);

        /// <summary>True when no sample differs by as much as 5% of the louder sound's peak.</summary>
        static bool SoundSame(float[] a, float[] b) => SoundSame(a, b, Peak(a), Peak(b));

        static bool SoundSame(float[] a, float[] b, float peakA, float peakB)
        {
            if (a.Length != b.Length) return false;
            float slack = 0.05f * System.Math.Max(peakA, peakB);
            for (int i = 0; i < a.Length; i++)
                if (System.Math.Abs(a[i] - b[i]) >= slack) return false;
            return true;
        }

        static int ZeroCrossings(float[] samples, float from, float to)
        {
            int n = 0;
            for (int i = (int)(from * SignalSound.SampleRate) + 1; i < (int)(to * SignalSound.SampleRate); i++)
                if ((samples[i - 1] < 0f) != (samples[i] < 0f)) n++;
            return n;
        }

        static float Rms(float[] samples)
        {
            double sum = 0;
            foreach (var x in samples) sum += x * x;
            return (float)System.Math.Sqrt(sum / System.Math.Max(1, samples.Length));
        }

        static float Peak(float[] samples) => samples.Length == 0 ? 0f : samples.Max(x => System.Math.Abs(x));

        [TestCaseSource(nameof(AllMedia))]
        public void EverySignalHasASound(Medium m)
        {
            foreach (int v in Audible(m))
            {
                var samples = SignalSound.Render(m, v);
                Assert.That(samples.Length, Is.EqualTo((int)System.Math.Ceiling(SignalSound.Duration(m, v) * SignalSound.SampleRate)), $"{m} {v}");
                Assert.That(Peak(samples), Is.LessThanOrEqualTo(0.95f), $"{m} {v} clips");
                Assert.That(Peak(samples), Is.GreaterThan(0.02f), $"{m} {v} is silent");
                Assert.That(System.Math.Abs(samples[samples.Length - 1]), Is.LessThan(0.001f), $"{m} {v} ends with a click");
            }
        }

        [TestCaseSource(nameof(AllMedia))]
        public void SignalsSoundDifferent(Medium m)
        {
            // Two different signals of one medium must never sound the same. (Sampled
            // waves that trace the same line are the same signal, and sound it.)
            var values = Audible(m).ToList();
            var sounds = values.Select(v => SignalSound.Render(m, v)).ToList();
            var peaks = sounds.Select(Peak).ToList();
            for (int a = 0; a < sounds.Count; a++)
                for (int b = a + 1; b < sounds.Count; b++)
                {
                    bool same = Media.Same(m, values[a], values[b]);
                    Assert.That(SoundSame(sounds[a], sounds[b], peaks[a], peaks[b]), Is.EqualTo(same),
                        $"{m}: {Media.Describe(m, values[a])} and {Media.Describe(m, values[b])} {(same ? "are the same signal but sound different" : "sound the same")}");
                }
        }

        [Test]
        public void OneBeepsAndZeroBarelySounds()
        {
            Assert.That(Rms(SignalSound.Render(Medium.Binary, 0)), Is.LessThan(Rms(SignalSound.Render(Medium.Binary, 1)) * 0.2f));
            Assert.That(SignalSound.Duration(Medium.Binary, 0), Is.EqualTo(SignalSound.Duration(Medium.Binary, 1)), "so sequences keep time");
        }

        [Test]
        public void DigitsSoundHigherTheBiggerTheyAre()
        {
            int previous = 0;
            for (int v = 0; v <= 9; v++)
            {
                Assert.That(SignalSound.Duration(Medium.Decimal, v), Is.EqualTo(SignalSound.Duration(Medium.Decimal, 0)), $"{v}: every digit lasts as long");
                var samples = SignalSound.Render(Medium.Decimal, v);
                int crossings = ZeroCrossings(samples, 0.02f, 0.2f);
                float hz = crossings / 2f / 0.18f;
                Assert.That(hz, Is.EqualTo(SignalSound.DigitHz[v]).Within(SignalSound.DigitHz[v] * 0.03f + 4f), $"{v} plays its note");
                Assert.That(crossings, Is.GreaterThan(previous), $"{v} is higher than {v - 1}");
                previous = crossings;
            }
            Assert.That(Rms(SignalSound.Render(Medium.Decimal, 0)), Is.GreaterThan(Rms(SignalSound.Render(Medium.Decimal, 9)) * 0.5f), "0 is a note too, not a tap");
        }

        [Test]
        public void ShapesClickOncePerCorner()
        {
            // Count the clicks: places where the sound jumps up after having faded.
            for (int form = 1; form < Shapes.Forms; form++)
                foreach (bool solid in new[] { false, true })
                {
                    var samples = SignalSound.Render(Medium.Shape, Shapes.Make(form, solid));
                    int window = SignalSound.SampleRate / 200, clicks = 0;
                    float previous = 0f;
                    for (int start = 0; start + window <= samples.Length; start += window)
                    {
                        float level = 0f;
                        for (int i = start; i < start + window; i++) level = System.Math.Max(level, System.Math.Abs(samples[i]));
                        if (level > previous * 1.5f && level > 0.05f) clicks++;
                        previous = level;
                    }
                    Assert.That(clicks, Is.EqualTo(Shapes.Corners[form]), $"{Media.Describe(Medium.Shape, Shapes.Make(form, solid))}");
                }
        }

        [Test]
        public void WavesSoundAtTheirFrequencyAndHeight()
        {
            foreach (int form in new[] { Waves.Sine, Waves.SquareWave, Waves.Saw })
            {
                float previousRms = 0f;
                for (int h = 0; h < Waves.Heights; h++)
                {
                    float rms = Rms(SignalSound.Render(Medium.Wave, Waves.Make(form, 3, h)));
                    Assert.That(rms, Is.GreaterThan(previousRms * 1.2f), $"{Waves.FormNames[form]}: amplitude {h + 1} is louder than {h}");
                    previousRms = rms;
                }
            }
            for (int f = 0; f < Waves.Frequencies; f++)
            {
                var samples = SignalSound.Render(Medium.Wave, Waves.Make(Waves.Sine, f, 6));
                float hz = ZeroCrossings(samples, 0.1f, 0.4f) / 2f / 0.3f;
                Assert.That(hz, Is.EqualTo(SignalSound.WaveHz(f)).Within(SignalSound.WaveHz(f) * 0.03f + 4f), $"frequency {f + 1}");
            }
            Assert.That(SignalSound.WaveHz(2), Is.EqualTo(440f).Within(0.01f), "frequency 3 is A4");
        }

        [Test]
        public void SampledWavesSoundStepped()
        {
            foreach (int f in new[] { 0, 3, 6 })
                foreach (int rate in new[] { 0, 1, 2 })
                {
                    int v = Sampled.Make(Waves.Sine, f, 6, rate);
                    var samples = SignalSound.Render(Medium.Sampled, v);
                    // Count the distinct levels in the held part of the sound: one wave's worth of steps.
                    int from = (int)(0.1f * SignalSound.SampleRate), to = (int)(0.3f * SignalSound.SampleRate);
                    int changes = 0;
                    for (int i = from + 1; i < to; i++) if (System.Math.Abs(samples[i] - samples[i - 1]) > 0.004f) changes++;
                    float cycles = SignalSound.WaveHz(f) * 0.2f;
                    int steps = Sampled.StepsPerCycle[rate];
                    // A sine read twice a cycle changes twice a cycle; more often for finer steps, but never more than once a step.
                    Assert.That(changes, Is.LessThanOrEqualTo((int)(cycles * steps) + 2), $"{Media.Describe(Medium.Sampled, v)} changes between its steps");
                    Assert.That(changes, Is.GreaterThanOrEqualTo((int)(cycles * 2) - 2), $"{Media.Describe(Medium.Sampled, v)} barely moves");
                    Assert.That(SoundSame(samples, SignalSound.Render(Medium.Wave, Waves.Make(Waves.Sine, f, 6))), Is.False);
                }
        }
    }
}
