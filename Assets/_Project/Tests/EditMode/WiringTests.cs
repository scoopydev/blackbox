using System.Collections.Generic;
using System.Linq;
using Blackbox.Components;
using Blackbox.Core;
using Blackbox.Puzzles;
using NUnit.Framework;

namespace Blackbox.Tests
{
    /// <summary>
    /// Wired circuits: the mixing blocks, the signals and devices they use, the
    /// wiring rules the player works under, and every shipped wired circuit.
    /// </summary>
    public class WiringTests
    {
        static IEnumerable<PuzzleDef> WiredPuzzles() => TestContent.Library.AllPuzzles.Where(p => p.Wiring);

        static Trace T(string signal) => Signals.Parse(signal).Trace;

        static Trace Mix(string kind, params Trace[] inputs) => KindCatalog.Get(kind).Apply(inputs);

        static int Pulses(Trace bits) => bits.Runs().Count(r => r.level > 0.5f);

        // ------------------------------------------------------------ wave and mixing blocks

        [Test]
        public void AWaveAndItsHalfLoopTwinMixToNothing()
        {
            var mixed = Mix("MIX", T("wave sine f2 a7"), T("wave sine f2 a7 p2"));
            Assert.That(mixed.Medium, Is.EqualTo(Medium.Wave));
            Assert.That(mixed.Matches(Trace.Flat(Medium.Wave, 0f)), Is.True, "silence");
            Assert.That(Mix("MIX", T("wave sine f2 a7"), T("wave sine f2 a7 p1")).Matches(Trace.Flat(Medium.Wave, 0f)), Is.False, "a quarter apart they don't cancel");
        }

        [Test]
        public void MixIsHalfwayBetween()
        {
            Assert.That(Mix("MIX", T("wave sine f1 a7"), T("wave sine f1 a7")).Matches(T("wave sine f1 a7")), Is.True, "a wave mixed with itself is unchanged");
            Assert.That(Mix("MIX", T("binary 1"), T("binary 0")).Matches(Trace.Flat(Medium.Wave, 0f)), Is.True, "halfway between the top and the bottom is the middle");
            Mix("MIX", T("wave square f1 a7"), T("wave saw f3 a7")).Range(out float low, out float high);
            Assert.That(high, Is.LessThanOrEqualTo(Trace.Peak + 0.001f), "never past the top");
            Assert.That(low, Is.GreaterThanOrEqualTo(-Trace.Peak - 0.001f), "or the bottom");
        }

        [Test]
        public void EachSetStartsWithAFlip()
        {
            Assert.That(KindCatalog.Get("NOT").Apply(new[] { T("binary 0110") }).Matches(T("binary 1001")), Is.True);
            Assert.That(KindCatalog.Get("MIRROR").Apply(new[] { T("decimal 2") }).Matches(T("decimal 7")), Is.True);
            Assert.That(KindCatalog.Get("FILL").Apply(new[] { T("shape solid triangle") }).Matches(T("shape hollow triangle")), Is.True);
            Assert.That(Mix("FLIP", T("wave sine f3 a5")).Matches(T("wave sine f3 a5 p2")), Is.True);
            Assert.That(Mix("MIX", T("wave saw f1 a7"), Mix("FLIP", T("wave saw f1 a7"))).Matches(Trace.Flat(Medium.Wave, 0f)), Is.True, "a wave and its flip mix to nothing");
        }

        [Test]
        public void LaterIsAQuarterOfTheLoop()
        {
            Assert.That(Mix("LATER", T("wave sine f1 a7")).Matches(T("wave sine f1 a7 p1")), Is.True, "a quarter of a slow wave's cycle");
            Assert.That(Mix("LATER", T("wave sine f4 a7")).Matches(T("wave sine f4 a7")), Is.True, "a whole cycle of a wave that repeats four times");
            Assert.That(Mix("LATER", T("binary 1000")).As(Medium.Binary).Matches(T("binary 0100")), Is.True, "a bit read as a wave, moved along");
        }

        [Test]
        public void FasterRepeatsTwiceALoop()
        {
            Assert.That(Mix("FASTER", T("wave sine f1 a7")).Matches(T("wave sine f2 a7")), Is.True);
            Assert.That(Mix("FASTER", T("binary 01")).As(Medium.Binary).Matches(T("binary 0101")), Is.True);
            var twice = Mix("FASTER", Mix("FASTER", T("wave saw f1 a5")));
            Assert.That(twice.Matches(T("wave saw f4 a5")), Is.True, "faster twice is four times as fast");
        }

        [Test]
        public void WaveBlocksAcceptAnySignal()
        {
            // Wave blocks read every input as a wave and always send one, so they chain and never refuse.
            var mixed = Mix("MIX", T("wave sine f1 a7"), T("wave saw f2 a3"));
            Assert.That(Mix("FASTER", mixed).Medium, Is.EqualTo(Medium.Wave));
            Assert.That(Mix("FLIP", T("binary 1")).Matches(Trace.Flat(Medium.Wave, -Trace.Peak)), Is.True, "a 1 is the top of a wave");
            Assert.That(Mix("LATER", T("decimal 9")).Matches(Trace.Flat(Medium.Wave, Trace.Peak)), Is.True, "9 is the top too");
            Assert.That(Mix("FASTER", T("shape solid circle")).Medium, Is.EqualTo(Medium.Wave));
        }

        [Test]
        public void AGateLetsTheTopThroughWhileTheBottomIsOn()
        {
            var gated = Mix("GATE", T("wave saw f3 a7"), T("binary 0110"));
            for (int k = 0; k < Trace.Steps; k++)
            {
                bool on = k >= 72 && k < 216;
                Assert.That(gated[k], Is.EqualTo(on ? T("wave saw f3 a7")[k] : 0f).Within(0.0001f), $"step {k}");
            }
            // GATE is a wave block: whatever comes in, a wave comes out, and off is the middle.
            Assert.That(Mix("GATE", T("decimal 6"), T("binary 0")).Matches(Trace.Flat(Medium.Wave, 0f)), Is.True, "off is a flat line");
            Assert.That(Mix("GATE", T("binary 1"), T("binary 1")).Matches(Trace.Flat(Medium.Wave, 9f)), Is.True, "a bit comes through as a wave at the top");
            Assert.That(Mix("GATE", T("decimal 9"), T("binary 1")).Matches(Trace.Flat(Medium.Wave, 9f)), Is.True, "and a digit as a level");
        }

        [Test]
        public void CountingRises()
        {
            var count = Mix("COUNT", T("binary 01010101"));
            Assert.That(count.Medium, Is.EqualTo(Medium.Decimal));
            Assert.That(count[Trace.Steps - 1], Is.EqualTo(4f));
            Assert.That(count.Runs().Select(r => r.level), Is.EqualTo(new[] { 0f, 1f, 2f, 3f, 4f }));
            Assert.That(Mix("COUNT", T("binary 1")).Matches(T("decimal 1")), Is.True, "a signal that's on from the start has switched on once");
            Assert.That(Mix("COUNT", T("binary 0")).Matches(T("decimal 0")), Is.True);
            var many = Mix("COUNT", T("wave square f7 a7"));
            Assert.That(many[Trace.Steps - 1], Is.EqualTo(9f), "twelve switch-ons stop at 9");
        }

        [Test]
        public void CompareMakesBitsFromWaves()
        {
            var bits = Mix("COMPARE", T("wave sine f1 a7"), T("decimal 7"));
            Assert.That(bits.Medium, Is.EqualTo(Medium.Binary));
            Assert.That(Pulses(bits), Is.EqualTo(1));
            int high = bits.Runs().Where(r => r.level > 0.5f).Sum(r => r.length);
            int low = Mix("COMPARE", T("wave sine f1 a7"), T("decimal 2")).Runs().Where(r => r.level > 0.5f).Sum(r => r.length);
            Assert.That(low, Is.GreaterThan(high), "a lower bottom input keeps it on for longer");
            Assert.That(Mix("COMPARE", T("decimal 5"), T("decimal 5")).Matches(T("binary 0")), Is.True, "equal is not higher");
        }

        [Test]
        public void AWaveIntoAnAndBlockBlinks()
        {
            // The idea the Combine tutorial is built on: a wave read as a bit is 1
            // above its middle, so an AND with a steady 1 blinks at its frequency.
            for (int f = 1; f <= 4; f++)
            {
                var bits = KindCatalog.Get("AND").Apply(new[] { T("binary 1"), T($"wave sine f{f} a7") });
                Assert.That(Pulses(bits), Is.EqualTo(Waves.Cycles[f - 1]), $"sine f{f}");
            }
        }

        [Test]
        public void ShapesCarryAColour()
        {
            var pink = Signals.Parse("shape solid pink triangle").Value.Value;
            Assert.That(Shapes.Colour(pink), Is.EqualTo(1));
            Assert.That(Shapes.Form(pink), Is.EqualTo(Shapes.Triangle));
            Assert.That(Shapes.Solid(pink), Is.True);
            Assert.That(Media.Describe(Medium.Shape, pink), Is.EqualTo("solid pink triangle"));
            Assert.That(Media.Describe(Medium.Shape, Shapes.Make(Shapes.Square, false)), Is.EqualTo("hollow square"), "cyan is the plain colour and goes unsaid");
            Assert.That(Media.Size(Medium.Shape), Is.EqualTo(32));
            // Shape blocks keep a shape's colour with its form.
            Assert.That(KindCatalog.Get("FILL").Apply(new[] { pink }), Is.EqualTo(Shapes.Make(Shapes.Triangle, false, 1)));
            Assert.That(KindCatalog.Get("NEXT").Apply(new[] { pink }), Is.EqualTo(Shapes.Make(Shapes.Square, true, 1)));
            int green = Shapes.Make(Shapes.Hexagon, false, 3);
            Assert.That(KindCatalog.Get("TAKEFILL").Apply(new[] { pink, green }), Is.EqualTo(Shapes.Make(Shapes.Triangle, false, 1)), "form and colour of top, fill of bottom");
            Assert.That(KindCatalog.Get("TAKECOLOUR").Apply(new[] { pink, green }), Is.EqualTo(Shapes.Make(Shapes.Triangle, true, 3)), "form and fill of top, colour of bottom");
        }

        [Test]
        public void ACircuitCanStartHalfWired()
        {
            var p = TestContent.ParseWired(Small.Replace(@"""solution"":", @"""start"": [ ""A -> g.0"", ""h -> X"" ], ""solution"":"));
            var s = new WiringSession(p);
            Assert.That(s.WireCount, Is.EqualTo(2), "the starting wires are in place");
            Assert.That(s.HasStart && s.AsStarted, Is.True);
            Assert.That(s.CanUndo, Is.False, "and they aren't a move");
            s.Disconnect(s.OutputNode(0), 0);
            s.Connect(1, s.BlockNode(0), 1);
            Assert.That(s.AsStarted, Is.False);
            Assert.That(s.Restart(), Is.True);
            Assert.That(s.AsStarted, Is.True, "restart puts the starting wires back");
            Assert.That(s.WireCount, Is.EqualTo(2));
            Assert.That(s.Undo(), Is.True, "and can be undone");
            Assert.That(s.WireCount, Is.EqualTo(2));
            Assert.That(s.DriverOf(s.BlockNode(0), 1), Is.EqualTo(1));
            Assert.That(new WiringSession(p, new string[0]).WireCount, Is.EqualTo(0), "a saved wiring wins over the start");
            Assert.Throws<PuzzleFormatException>(() => TestContent.ParseWired(Small.Replace(@"""solution"":", @"""start"": [ ""A -> g.0"", ""B -> g.0"" ], ""solution"":")), "two wires into one port");
        }

        [Test]
        public void BlocksOfOneKindReadOtherKinds()
        {
            Assert.That(KindCatalog.Get("NOT").Apply(new[] { T("decimal 0") }).Matches(T("binary 1")), Is.True, "a digit is 1 unless it's 0");
            Assert.That(KindCatalog.Get("NOT").Apply(new[] { T("shape hollow square") }).Matches(T("binary 1")), Is.True, "a hollow shape is 0");
            Assert.That(KindCatalog.Get("MIRROR").Apply(new[] { T("shape solid triangle") }).Matches(T("decimal 6")), Is.True, "a shape is its corners");
            Assert.That(KindCatalog.Get("MIRROR").Apply(new[] { T("binary 01") }).Matches(T("decimal 98")), Is.True, "a bit is 0 or 1, step by step");
            Assert.That(KindCatalog.Get("NEXT").Apply(new[] { T("binary 1") }).Matches(T("shape solid triangle")), Is.True, "a bit is a circle, solid for 1");
        }

        // ------------------------------------------------------------ signals

        [Test]
        public void SignalsAreWrittenAsTheyRead()
        {
            Assert.That(Signals.Parse("binary 0110").Values, Is.EqualTo(new[] { 0, 1, 1, 0 }));
            Assert.That(Signals.Parse("decimal 3141").Values, Is.EqualTo(new[] { 3, 1, 4, 1 }));
            Assert.That(Signals.Parse("decimal 7").Value, Is.EqualTo(7));
            Assert.That(Signals.Parse("shape solid triangle, hollow circle").Values.Length, Is.EqualTo(2));
            Assert.That(Signals.Parse("wave sine f2 a7").Trace.Medium, Is.EqualTo(Medium.Wave));
            Assert.That(Signals.Parse("sampled saw f1 a7 s6").Trace.Medium, Is.EqualTo(Medium.Sampled));
            foreach (var text in new[] { "binary 0110", "decimal 3141", "shape solid triangle, hollow circle", "wave sine f2 a7 p2", "sampled saw f1 a7 s6" })
                Assert.That(Signals.Parse(text).ToString(), Is.EqualTo(text));

            Assert.That(Signals.TryParse("binary 01101", out _, out string error), Is.False, "5 values don't fit the loop");
            Assert.That(error, Does.Contain("doesn't fit"));
            Assert.That(Signals.TryParse("colour red", out _, out _), Is.False);
            Assert.That(Signals.TryParse("decimal", out _, out _), Is.False);
            Assert.That(Signals.TryParse("shape solid heptagon", out _, out _), Is.False);
        }

        [Test]
        public void APatternHoldsEachValueEqually()
        {
            var t = T("binary 0110");
            Assert.That(t.Runs().Select(r => r.length), Is.EqualTo(new[] { 72, 144, 72 }));
            Assert.That(t.Key, Is.EqualTo(T("binary 0110").Key));
            Assert.That(t.Key, Is.Not.EqualTo(T("binary 0011").Key));
        }

        // ------------------------------------------------------------ devices

        [Test]
        public void DevicesMatchOnWhatTheyDo()
        {
            Assert.That(Devices.Same(Device.Digits, T("shape solid triangle"), T("decimal 3")), Is.True, "a digit display shows a shape's corners");
            Assert.That(Devices.Same(Device.Display, T("shape solid triangle"), T("decimal 3")), Is.False, "a display shows the signal itself");
            Assert.That(Devices.Same(Device.Light, T("binary 1"), T("decimal 9")), Is.True, "both fully on");
            Assert.That(Devices.Same(Device.Light, T("binary 0"), T("wave sine f1 a7 p2")), Is.False, "the wave lights it for half the loop");
            Assert.That(Devices.Same(Device.Light, T("decimal 0"), Trace.Flat(Medium.Wave, -Trace.Peak)), Is.True, "a light is dark below a wave's middle");
            Assert.That(Devices.Same(Device.Light, T("shape solid pink triangle"), T("shape hollow pink square")), Is.True, "a light shows only a shape's colour");
            Assert.That(Devices.Same(Device.Light, T("shape solid pink triangle"), T("shape solid green triangle")), Is.False, "each colour lights it differently");
            Assert.That(Devices.Same(Device.Light, T("shape solid amber circle"), T("binary 1")), Is.False, "a coloured shape tints the light");
            Assert.That(Devices.Same(Device.Light, T("shape hollow circle"), T("binary 1")), Is.True, "a cyan shape lights it like any signal");
            Assert.That(Devices.Same(Device.Fan, T("decimal 9"), T("binary 1")), Is.True);
            Assert.That(Devices.Same(Device.Fan, T("decimal 3"), T("decimal 4")), Is.False);
            Assert.That(Devices.Same(Device.Servo, Trace.Flat(Medium.Wave, Trace.Peak), T("binary 1")), Is.True, "all the way right");
            Assert.That(Devices.Same(Device.Speaker, T("wave sine f2 a7"), T("wave sine f2 a6")), Is.False, "a quieter sound");
            Assert.That(Devices.Same(Device.Light, null, T("binary 0")), Is.False, "nothing doesn't match");
        }

        [Test]
        public void DevicesReadTheirNames()
        {
            foreach (var d in Devices.All)
            {
                Assert.That(Devices.TryParse(Devices.Key(d), out var back), Is.True, Devices.Key(d));
                Assert.That(back, Is.EqualTo(d));
                Assert.That(Devices.Noun(d), Is.Not.Empty);
            }
            Assert.That(Devices.TryParse("motor", out var motor) && motor == Device.Fan, Is.True, "a motor is shown as a fan");
            Assert.That(Devices.TryParse("toaster", out _), Is.False);
        }

        [Test]
        public void SilenceSoundsSilent()
        {
            var data = SignalSound.Render(Trace.Flat(Medium.Wave, 0f), 8000);
            Assert.That(data.Length, Is.GreaterThan(0));
            Assert.That(data.All(x => x == 0f), Is.True);
            Assert.That(SignalSound.Render(T("wave sine f2 a7"), 8000).Any(x => System.Math.Abs(x) > 0.05f), Is.True);
            Assert.That(SignalSound.Render(T("binary 0110"), 8000).Any(x => x != 0f), Is.True);
        }

        // ------------------------------------------------------------ the session

        const string Small = @"{
            ""id"": ""t"", ""name"": ""Test"",
            ""inputs"": { ""A"": ""binary 1"", ""B"": ""wave sine f1 a7"" },
            ""blocks"": { ""g"": { ""kind"": ""AND"" }, ""h"": { ""kind"": ""NOT"" } },
            ""outputs"": { ""X"": ""display"" },
            ""solution"": [ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]
        }";

        [Test]
        public void EveryWaveSaysItsFrequencyAndAmplitude()
        {
            // Under a wave on a block or an output: the settings of the wave it is,
            // however it was made, or else what can be measured.
            Assert.That(Waves.Describe(T("wave sine f2 a7")), Is.EqualTo("A7"));
            Assert.That(Waves.Describe(Mix("FLIP", T("wave sine f2 a7"))), Is.EqualTo("A7"), "a flipped sine is the same sine half a cycle on; the late start isn't written");
            Assert.That(Waves.Describe(Mix("FASTER", T("wave saw f1 a5"))), Is.EqualTo("A5"));
            Assert.That(Waves.Describe(Mix("LATER", T("wave square f1 a3"))), Is.EqualTo("A3"));
            Assert.That(Waves.Describe(Mix("MIX", T("wave sine f3 a6"), T("wave sine f3 a6"))), Is.EqualTo("A6"));
            Assert.That(Waves.Describe(Mix("MIX", T("wave sine f2 a4"), T("binary 1"))), Is.EqualTo("A2"), "lifted and halved: it swings 2 each way");
            Assert.That(Waves.Describe(Mix("MIX", T("wave sine f2 a7"), T("binary 1"))), Is.EqualTo("A3.5"));
            Assert.That(Waves.Describe(Mix("FLIP", T("wave saw f1 a7"))), Is.EqualTo("A7"), "a falling saw isn't one of the waves, so it's measured");
            Assert.That(Waves.Describe(Mix("MIX", T("wave sine f2 a7"), T("wave sine f2 a7 p2"))), Is.EqualTo("A0"), "a flat line");
            Assert.That(Waves.Describe(Mix("FASTER", T("wave sine f7 a7"))), Is.EqualTo("A7"), "24 times a loop is off the frequency scale");
            Assert.That(Waves.Describe(T("decimal 4")), Is.Empty);
            Assert.That(Waves.Describe(null), Is.Empty);
        }

        [Test]
        public void BlocksKeepTheirSignalUntilTheirInputsChange()
        {
            // After a run, a block's signal stays shown until its own inputs are
            // rewired, or a block feeding it changes. Where its signal goes doesn't matter.
            var p = TestContent.ParseWired(Small);
            var s = new WiringSession(p);
            int a = 0, b = 1, g = s.BlockNode(0), h = s.BlockNode(1), x = s.OutputNode(0);
            s.Connect(a, g, 0);
            s.Connect(b, g, 1);
            s.Connect(g, h, 0);
            var ran = s.Drivers;

            Assert.That(WiringSession.BlocksUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true, true }), "nothing changed");
            s.Connect(h, x, 0);
            Assert.That(WiringSession.BlocksUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true, true }), "wiring a block on to an output keeps it");
            s.Connect(g, x, 0);
            Assert.That(WiringSession.BlocksUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true, true }), "and so does moving where it goes");
            s.Connect(a, g, 1);
            Assert.That(WiringSession.BlocksUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { false, false }), "rewiring g's input changes g, and h, which g feeds");
            s.Connect(b, g, 1);
            Assert.That(WiringSession.BlocksUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true, true }), "put back as it ran, nothing has changed");
            s.Disconnect(h, 0);
            Assert.That(WiringSession.BlocksUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true, false }), "taking h's input off changes only h");
        }

        [Test]
        public void AnOutputIsJudgedByItsSignalAlone()
        {
            // Any wiring that makes the target's signal meets the output, the
            // designer's or not, and a blip too short to see or hear doesn't count
            // against it: here a wave read as a bit, 0 at the one step it sits on
            // the middle, where the pattern 01 is already 1.
            var p = TestContent.ParseWired(@"{
                ""id"": ""t"", ""name"": ""Test"",
                ""inputs"": { ""A"": ""binary 01"", ""B"": ""wave sine f1 a7 p2"", ""C"": ""binary 0101"" },
                ""blocks"": { ""g"": ""NOT"" },
                ""outputs"": { ""X"": ""display"" },
                ""solution"": [ ""A -> g"", ""g -> X"" ]
            }");
            var fromWave = new WiringSession(p, new[] { "B -> g", "g -> X" });
            var got = fromWave.Check().Values[fromWave.OutputNode(0)];
            Assert.That(got.Matches(p.Outputs[0].Want), Is.False, "the wave crosses the middle a step later than the pattern switches");
            Assert.That(fromWave.Check().OutputMet[0], Is.True, "but the display shows the same, so X is met");
            Assert.That(fromWave.Check().Solved, Is.True, "and a different wiring from the designer's solves it");
            Assert.That(new WiringSession(p, new[] { "C -> g", "g -> X" }).Check().OutputMet[0], Is.False, "a quarter of the loop out is not a blip");
            Assert.That(Trace.Of(Medium.Binary, 1).LooksLike(Trace.Of(Medium.Decimal, 1)), Is.False, "a bit is still not a digit");
        }

        [Test]
        public void SolvedOutputsHoldUntilWhatFeedsThemChanges()
        {
            // An output solved on a run stays solved, and quiet on the next, while
            // the wire into it and every block upstream of it are as they ran.
            var p = TestContent.ParseWired(Small);
            var s = new WiringSession(p);
            int a = 0, b = 1, g = s.BlockNode(0), h = s.BlockNode(1), x = s.OutputNode(0);
            s.Connect(a, g, 0);
            s.Connect(b, g, 1);
            s.Connect(g, h, 0);
            s.Connect(h, x, 0);
            var ran = s.Drivers;

            Assert.That(WiringSession.OutputsUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true }), "nothing changed");
            s.Connect(g, x, 0);
            Assert.That(WiringSession.OutputsUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { false }), "its wire comes from somewhere else");
            s.Connect(h, x, 0);
            Assert.That(WiringSession.OutputsUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true }), "put back");
            s.Connect(a, g, 1);
            Assert.That(WiringSession.OutputsUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { false }), "a block two steps up was rewired");
            s.Connect(b, g, 1);
            Assert.That(WiringSession.OutputsUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { true }), "put back again");
            s.Disconnect(x, 0);
            Assert.That(WiringSession.OutputsUnchanged(p, ran, s.Drivers), Is.EqualTo(new[] { false }), "no wire");
        }

        [Test]
        public void TheSessionKeepsTheRules()
        {
            var p = TestContent.ParseWired(Small);
            var s = new WiringSession(p);
            int a = 0, b = 1, g = s.BlockNode(0), h = s.BlockNode(1), x = s.OutputNode(0);

            Assert.That(s.Check().Solved, Is.False, "nothing wired");
            Assert.That(s.Check().UnwiredOutputs, Is.EqualTo(new[] { 0 }));

            Assert.That(s.CanConnect(x, g, 0, out _), Is.False, "outputs don't drive wires");
            Assert.That(s.CanConnect(a, b, 0, out _), Is.False, "wires don't run into inputs");
            Assert.That(s.CanConnect(g, g, 0, out _), Is.False, "a block can't feed itself");
            Assert.That(s.Connect(g, h, 0), Is.True);
            Assert.That(s.CanConnect(h, g, 0, out string why), Is.False, "no loops");
            Assert.That(why, Does.Contain("loop"));

            Assert.That(s.Connect(h, x, 0), Is.True);
            var report = s.Check();
            Assert.That(report.EmptyBlocks, Is.EqualTo(new[] { 0 }), "g has empty inputs");
            Assert.That(report.Values[g], Is.Null, "a block with an empty input sends nothing");
            Assert.That(report.Values[h], Is.Null, "nor does one fed nothing");

            s.Connect(a, g, 0);
            s.Connect(a, g, 1);
            report = s.Check();
            Assert.That(report.EmptyBlocks, Is.Empty);
            Assert.That(report.RulesKept, Is.True, "B goes unused, which is allowed");
            Assert.That(report.Solved, Is.False, "steady instead of blinking");
            Assert.That(s.Connect(b, g, 1), Is.True, "a new wire replaces the old one in that port");
            Assert.That(s.DriverOf(g, 1), Is.EqualTo(b));
            Assert.That(s.Check().Solved, Is.True);
            Assert.That(s.Run(), Is.True);
            Assert.That(s.RunSolved, Is.True);

            Assert.That(s.Disconnect(g, 1), Is.True);
            Assert.That(s.RunSolved, Is.False, "a change takes the result away until the next run");
            Assert.That(s.Undo(), Is.True);
            Assert.That(s.Check().Solved, Is.True, "undo puts it back");
        }

        [Test]
        public void InputsMayGoUnusedButBlocksMayNot()
        {
            // An input can be left unwired, so the player can try things; every block has to be used.
            var p = TestContent.ParseWired(Small);
            var s = new WiringSession(p);
            int a = 0, b = 1, g = s.BlockNode(0), h = s.BlockNode(1), x = s.OutputNode(0);
            s.Connect(a, g, 0);
            s.Connect(a, g, 1);
            s.Connect(g, h, 0);
            s.Connect(h, x, 0);
            var report = s.Check();
            Assert.That(report.UnusedBlocks, Is.Empty);
            Assert.That(report.RulesKept, Is.True, "B leads nowhere, and that's fine");
            Assert.That(GoalText.BrokenRules(p, report), Is.Null);

            s.Connect(b, h, 0);
            report = s.Check();
            Assert.That(report.UnusedBlocks, Is.EqualTo(new[] { 0 }), "h now reads B, so g leads nowhere");
            Assert.That(GoalText.BrokenRules(p, report), Is.EqualTo("Block B1 isn't used: its signal doesn't reach an output. Every block with a solid outline has to lead to an output."));
            s.Disconnect(g, 1);
            report = s.Check();
            Assert.That(GoalText.BrokenRules(p, report), Is.EqualTo("Block B1 has an input with no wire, so it sends nothing. Block B1 isn't used: its signal doesn't reach an output. Every block with a solid outline has to lead to an output. Every block input needs a wire."));
            s.Clear();
            report = s.Check();
            Assert.That(GoalText.BrokenRules(p, report), Does.StartWith("Blocks B1 and B2 have inputs with no wire, so they send nothing. Blocks B1 and B2 aren't used"));
            Assert.That(GoalText.BrokenRules(p, report), Does.Not.Contain("Input"));

            s.Connect(a, g, 0);
            s.Connect(b, g, 1);
            s.Connect(g, h, 0);
            s.Connect(h, x, 0);
            Assert.That(GoalText.BrokenRules(p, s.Check()), Is.Null, "every rule kept");
            Assert.That(WiringSession.AllBlocksUsed(p, s.Drivers), Is.True);
            Assert.That(GoalText.WiringRules, Is.EqualTo("Match all outputs. Use everything with a solid outline."));
        }

        const string WithSpares = @"{
            ""id"": ""t"", ""name"": ""Test"",
            ""inputs"": { ""A"": ""binary 0110"", ""B"": ""binary 0011"", ""C"": ""decimal 5"" },
            ""blocks"": { ""g"": { ""kind"": ""AND"" }, ""h"": { ""kind"": ""NOT"" }, ""k"": { ""kind"": ""FLIP"", ""optional"": true } },
            ""outputs"": { ""X"": ""display"" },
            ""solution"": [ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]
        }";

        [Test]
        public void OptionalBlocksMayBeLeftOut()
        {
            // A dotted block may be left out. It may go unused, wired or not; once
            // it leads to an output, it's a block like any other and needs every
            // input wired.
            var p = TestContent.ParseWired(WithSpares);
            Assert.That(p.Inputs.Select(i => i.Optional), Is.EqualTo(new[] { true, true, true }), "inputs are dotted unless a circuit needs one");
            Assert.That(p.Blocks.Select(b => b.Optional), Is.EqualTo(new[] { false, false, true }));

            var s = new WiringSession(p);
            int a = 0, c = 2, h = s.BlockNode(1), k = s.BlockNode(2), x = s.OutputNode(0);
            foreach (var w in p.Solution) s.Connect(s.NodeOf(w.From), s.NodeOf(w.To), w.ToPort);
            var report = s.Check();
            Assert.That(report.Solved, Is.True, "solved without the optional block");
            Assert.That(report.IdleBlocks, Is.EqualTo(new[] { 2 }));
            Assert.That(report.UnusedBlocks, Is.Empty, "leaving it out breaks no rule");

            s.Connect(c, k, 0);
            report = s.Check();
            Assert.That(report.Solved, Is.True, "wired, but going nowhere, is still left out");
            Assert.That(report.IdleBlocks, Is.EqualTo(new[] { 2 }));

            s.Disconnect(k, 0);
            s.Connect(k, x, 0);
            report = s.Check();
            Assert.That(report.IdleBlocks, Is.Empty, "it leads to an output now");
            Assert.That(report.EmptyBlocks, Is.EqualTo(new[] { 2 }), "so its input needs a wire");
            Assert.That(report.UnusedBlocks, Is.EqualTo(new[] { 0, 1 }), "and the blocks it replaced lead nowhere");
            Assert.That(report.RulesKept, Is.False);

            var analysis = WiringSolver.Analyse(p);
            Assert.That(analysis.Space, Is.EqualTo(25 * 5 * 6), "an optional block has one more way to be wired: not at all");
            Assert.That(analysis.Solutions, Is.EqualTo(1), "wiring the optional block to nowhere isn't another answer");
            Assert.That(analysis.Examples[0], Does.Not.Contain("k"));
        }

        [Test]
        public void AnAnswerMayUseADottedBlock()
        {
            // The outline doesn't say whether a dotted block is needed: here the
            // answer runs through it, and the solver counts answers that do.
            var p = TestContent.ParseWired(WithSpares.Replace(@"""g -> h"", ""h -> X""", @"""g -> k"", ""k -> h"", ""h -> X"""));
            Assert.That(p.Blocks[2].Optional, Is.True);
            var s = new WiringSession(p);
            foreach (var w in p.Solution) s.Connect(s.NodeOf(w.From), s.NodeOf(w.To), w.ToPort);
            var report = s.Check();
            Assert.That(report.Solved, Is.True);
            Assert.That(report.IdleBlocks, Is.Empty, "the dotted block is in use");
            var analysis = WiringSolver.Analyse(p);
            Assert.That(analysis.Examples.Any(e => e.Contains("-> k")), Is.True, "the solver counts answers through a dotted block");
        }

        const string Needs = @"{
            ""id"": ""t"", ""name"": ""Test"",
            ""inputs"": { ""A"": { ""signal"": ""binary 0110"", ""required"": true }, ""B"": ""binary 0011"", ""C"": ""decimal 5"" },
            ""blocks"": { ""g"": { ""kind"": ""AND"" }, ""h"": { ""kind"": ""NOT"" } },
            ""outputs"": { ""X"": ""display"" },
            ""solution"": [ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]
        }";

        [Test]
        public void ARequiredInputHasToBeUsed()
        {
            // A solid input has to lead to an output: a run names it while it doesn't,
            // and an answer without it isn't one.
            var p = TestContent.ParseWired(Needs);
            Assert.That(p.Inputs.Select(i => i.Optional), Is.EqualTo(new[] { false, true, true }), "A is solid, the rest dotted");
            var s = new WiringSession(p);
            int a = 0, b = 1, c = 2, g = s.BlockNode(0), h = s.BlockNode(1), x = s.OutputNode(0);
            s.Connect(b, g, 0);
            s.Connect(c, g, 1);
            s.Connect(g, h, 0);
            s.Connect(h, x, 0);
            var report = s.Check();
            Assert.That(report.UnusedInputs, Is.EqualTo(new[] { 0 }));
            Assert.That(report.RulesKept, Is.False);
            Assert.That(GoalText.BrokenRules(p, report), Is.EqualTo("Input A isn't used, and its solid outline means it has to be."));
            s.Connect(a, g, 1);
            report = s.Check();
            Assert.That(report.UnusedInputs, Is.Empty);
            Assert.That(WiringSession.RequiredUsed(p, s.Drivers), Is.True);

            // B AND A gives the same as A AND B; without A, nothing else does.
            var analysis = WiringSolver.Analyse(p);
            Assert.That(analysis.Solutions, Is.EqualTo(1));
            Assert.That(analysis.Examples[0], Does.Contain("A ->"));
        }

        [Test]
        public void SolidAndDottedAreChecked()
        {
            void Refused(string json, string reason, string expect)
            {
                var ex = Assert.Throws<PuzzleFormatException>(() => TestContent.ParseWired(json), reason);
                Assert.That(ex.Message, Does.Contain(expect), reason);
            }
            Refused(Small.Replace(@"""B"": ""wave sine f1 a7""", @"""B"": { ""signal"": ""wave sine f1 a7"", ""optional"": true }"), "inputs are dotted already; only required ones are marked", "optional unless marked \"required\"");
            Refused(Needs.Replace(@"""A -> g.0""", @"""C -> g.0"""), "a solid input is in the answer", "input 'A' is required");
            Refused(WithSpares.Replace(@"""k"": { ""kind"": ""FLIP"", ""optional"": true }", @"""k"": { ""kind"": ""FLIP"" }").Replace(@"""h -> X"" ]", @"""h -> X"", ""C -> k"" ]"), "a solid block is in the answer", "unless it's marked \"optional\"");
            Refused(WithSpares.Replace(@"""h -> X"" ]", @"""h -> X"", ""C -> k"" ]"), "the answer doesn't wire up a dotted block that goes nowhere", "leads nowhere");
            Refused(Small.Replace(@"""X"": ""display""", @"""X"": { ""device"": ""display"", ""optional"": true }"), "outputs are never optional", "only inputs and blocks");
        }

        [Test]
        public void EveryBlockMustBeUsed()
        {
            var p = TestContent.ParseWired(Small);
            var s = new WiringSession(p);
            int a = 0, b = 1, g = s.BlockNode(0), h = s.BlockNode(1), x = s.OutputNode(0);
            s.Connect(a, g, 0);
            s.Connect(b, g, 1);
            s.Connect(a, h, 0);
            s.Connect(g, x, 0);
            var report = s.Check();
            Assert.That(report.UnusedBlocks, Is.EqualTo(new[] { 1 }), "h leads nowhere");
            Assert.That(report.Solved, Is.False, "the output can't be met without h anyway, and h must be used");

            Assert.That(s.DisconnectFrom(a), Is.True);
            Assert.That(s.DriverOf(g, 0), Is.EqualTo(-1));
            Assert.That(s.DriverOf(h, 0), Is.EqualTo(-1));
            Assert.That(s.Undo(), Is.True, "taking off every wire from a source is one change");
            Assert.That(s.DriverOf(g, 0), Is.EqualTo(a));
            Assert.That(s.DriverOf(h, 0), Is.EqualTo(a));
        }

        [Test]
        public void WiringIsSavedAndResumed()
        {
            var p = TestContent.ParseWired(Small);
            var s = new WiringSession(p);
            foreach (var w in p.Solution) s.Connect(s.NodeOf(w.From), s.NodeOf(w.To), w.ToPort);
            var saved = s.Snapshot();
            Assert.That(saved, Is.EquivalentTo(new[] { "A -> g.0", "B -> g.1", "g -> h", "h -> X" }));

            var resumed = new WiringSession(p, saved);
            Assert.That(resumed.WireCount, Is.EqualTo(4));
            Assert.That(resumed.Check().Solved, Is.True);
            Assert.That(resumed.CanUndo, Is.False, "resuming isn't a move");

            var damaged = new WiringSession(p, new[] { "A -> g.0", "nonsense", "Q -> g.1", "g -> g.0", "h -> X" });
            Assert.That(damaged.WireCount, Is.EqualTo(2), "only the wires that make sense come back");
        }

        [Test]
        public void TheLoaderRefusesBrokenWiredCircuits()
        {
            void Refused(string json, string reason, string expect)
            {
                var ex = Assert.Throws<PuzzleFormatException>(() => TestContent.ParseWired(json), reason);
                Assert.That(ex.Message, Does.Contain(expect), reason);
            }
            Refused(Small.Replace(@"""kind"": ""NOT""", @"""kind"": ""NOT"", ""state"": 1"), "blocks have no settings", "no settings");
            Refused(Small.Replace(@"""kind"": ""NOT""", @"""kind"": ""B1"""), "the old kind codes are gone", "B1");
            Refused(Small.Replace(@"""g -> h"", ", ""), "the solution must use every block", "nothing wired into it");
            Refused(Small.Replace(@"[ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]", @"[ ""A -> g.0"", ""B -> g.1"", ""A -> h"", ""g -> X"" ]"), "a block that leads nowhere", "doesn't use block 'h'");
            Refused(Small.Replace(@"""solution"": [ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]", @"""hint"": ""none"""), "no solution", "solution");
            Refused(Small.Replace(@"""outputs"":", @"""probes"": 2, ""outputs"":"), "no probes", "probes");
            Refused(Small.Replace(@"""X"": ""display""", @"""X"": ""toaster"""), "unknown device", "toaster");
            foreach (var d in new[] { "light", "speaker", "digits", "fan", "servo" })
                Refused(Small.Replace(@"""X"": ""display""", $@"""X"": ""{d}"""), $"a {d} is set aside", "set aside");
            Refused(Small.Replace(@"""A"": ""binary 1""", @"""A"": ""binary 11011"""), "a pattern that doesn't fit", "doesn't fit");
            Refused(Small.Replace(@"""B"": ""wave sine f1 a7""", @"""B"": ""sampled sine f1 a7 s6"""), "stepped waves are set aside", "set aside");
            Refused(Small.Replace(@"""kind"": ""NOT""", @"""kind"": ""STEPS"""), "the stepper is set aside", "set aside");

            var p = TestContent.ParseWired(Small);
            Assert.That(p.Wiring, Is.True);
            Assert.That(p.Blocks.All(b => b.Locked), Is.True, "the player wires blocks but never sets them");
            Assert.That(p.Outputs[0].Device, Is.EqualTo(Device.Display));
            Assert.That(p.Outputs[0].Want, Is.Not.Null, "the target comes from the solution");
        }

        [Test]
        public void BlocksAreLabelledByWhatTheySend()
        {
            // The player sees B1, D1, W1 ... on blocks: the kind of signal each sends
            // and a number, never which rule it keeps. Hints name blocks as {id} so
            // they always match the labels on the board.
            var hinted = Small.Replace(@"""name"": ""Test"",", @"""name"": ""Test"", ""hint"": ""Wire {g}, then {h}."",");
            var p = TestContent.ParseWired(hinted);
            Assert.That(p.Blocks.Select(b => b.Label), Is.EqualTo(new[] { "B1", "B2" }));
            Assert.That(p.Hint, Is.EqualTo("Wire B1, then B2."));

            var mixed = TestContent.ParseWired(@"{
                ""id"": ""t"", ""name"": ""Test"",
                ""inputs"": { ""A"": ""wave sine f1 a7"", ""B"": ""decimal 3"" },
                ""blocks"": { ""g"": ""MIRROR"", ""h"": ""FLIP"", ""k"": ""COMPARE"", ""m"": ""GATE"", ""n"": ""MAX"" },
                ""outputs"": { ""X"": ""display"", ""Y"": ""display"" },
                ""solution"": [ ""A -> g"", ""A -> h"", ""g -> n.0"", ""B -> n.1"", ""h -> k.0"", ""n -> k.1"", ""h -> m.0"", ""k -> m.1"", ""m -> X"", ""n -> Y"" ]
            }");
            var labels = mixed.Blocks.ToDictionary(b => b.Id, b => b.Label);
            Assert.That(labels["g"], Does.StartWith("D"), "MIRROR sends a digit");
            Assert.That(labels["n"], Does.StartWith("D"));
            Assert.That(labels["h"], Does.StartWith("W"), "FLIP sends a wave");
            Assert.That(labels["m"], Does.StartWith("W"), "and so does GATE, whatever comes in");
            Assert.That(labels["k"], Does.StartWith("B"), "COMPARE sends a bit");
            Assert.That(labels.Values, Is.Unique);
            Assert.That(new[] { labels["g"], labels["n"] }, Is.EquivalentTo(new[] { "D1", "D2" }));
            var ex = Assert.Throws<PuzzleFormatException>(() => TestContent.ParseWired(hinted.Replace("{h}", "{q}")));
            Assert.That(ex.Message, Does.Contain("{q}"));
        }

        [Test]
        public void GoalsNameTheOutputs()
        {
            Assert.That(GoalText.For(TestContent.ParseWired(Small)), Is.EqualTo("Make display X do what its target shows."));
            var two = Small.Replace(@"""outputs"": { ""X"": ""display"" }", @"""outputs"": { ""X"": ""display"", ""Y"": ""display"" }")
                           .Replace(@"""h -> X"" ]", @"""h -> X"", ""g -> Y"" ]");
            Assert.That(GoalText.For(TestContent.ParseWired(two)), Is.EqualTo("Make displays X and Y do what their targets show."), "one noun for outputs that are all alike");
        }

        [Test]
        public void SwappedWiresIntoAnAlikeBlockAreOneAnswer()
        {
            // A AND B or B AND A, a MAJORITY's three bits in any order, BETWEEN's two
            // bounds either way round: each is one answer, not several.
            string Circuit(string inputs, string kind, string solution) => @"{
                ""id"": ""t"", ""name"": ""Test"",
                ""inputs"": { " + inputs + @" },
                ""blocks"": { ""g"": { ""kind"": """ + kind + @""" } },
                ""outputs"": { ""X"": ""display"" },
                ""solution"": [ " + solution + @", ""g -> X"" ]
            }";
            var and = TestContent.ParseWired(Circuit(@"""A"": ""binary 0011"", ""B"": ""binary 0101""", "AND", @"""A -> g.0"", ""B -> g.1"""));
            Assert.That(WiringSolver.Analyse(and).Solutions, Is.EqualTo(1));
            var majority = TestContent.ParseWired(Circuit(@"""A"": ""binary 0011"", ""B"": ""binary 0101"", ""C"": ""binary 0110""", "MAJORITY", @"""A -> g.0"", ""B -> g.1"", ""C -> g.2"""));
            Assert.That(WiringSolver.Analyse(majority).Solutions, Is.EqualTo(1));
            var between = TestContent.ParseWired(Circuit(@"""A"": ""wave sine f1 a7"", ""B"": ""decimal 3"", ""C"": ""decimal 6""", "BETWEEN", @"""A -> g.0"", ""B -> g.1"", ""C -> g.2"""));
            Assert.That(WiringSolver.Analyse(between).Solutions, Is.EqualTo(1));
            var compare = TestContent.ParseWired(Circuit(@"""A"": ""wave sine f1 a7"", ""B"": ""decimal 3""", "COMPARE", @"""A -> g.0"", ""B -> g.1"""));
            Assert.That(WiringSolver.Analyse(compare).Solutions, Is.EqualTo(1), "COMPARE's inputs aren't alike: the other way round sends something else");
            Assert.That(KindCatalog.Get("COMPARE").AlikeFrom, Is.EqualTo(-1));
        }

        // ------------------------------------------------------------ shipped circuits

        [TestCaseSource(nameof(WiredPuzzles))]
        public void WiredCircuitIsSolvable(PuzzleDef puzzle)
        {
            var analysis = WiringSolver.Analyse(puzzle);
            Assert.That(analysis.SolutionWorks, Is.True, analysis.ToString());
            Assert.That(analysis.StartsUnsolved, Is.True, analysis.ToString());
            Assert.That(analysis.Searched, Is.True, $"{puzzle.Id} is too big for the checker to search: {analysis.Space} wirings");
            Assert.That(analysis.Solutions, Is.GreaterThan(0), analysis.ToString());
        }

        [TestCaseSource(nameof(WiredPuzzles))]
        public void WiredCircuitIsWellMade(PuzzleDef puzzle)
        {
            Assert.That(puzzle.Blocks.Count, Is.InRange(1, 8), "the board fits at most eight blocks");
            Assert.That(puzzle.Inputs.Count, Is.InRange(1, 5));
            Assert.That(puzzle.Outputs.Count, Is.InRange(1, 4));
            Assert.That(puzzle.Blocks.All(b => !b.Kind.Shelved), Is.True);
            Assert.That(puzzle.Hint, Is.Not.Empty, puzzle.Id);
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(puzzle.Hint, KindNames), Is.False, $"{puzzle.Id}: the hint names a kind code: {puzzle.Hint}");
            Assert.That(puzzle.Hint, Does.Not.Contain("{"), $"{puzzle.Id}: the hint names a block the loader didn't label");
            // Each block is labelled by what it sends, numbered per letter down each column.
            var order = puzzle.Blocks.OrderBy(b => b.At.Col).ThenBy(b => b.At.Row).ToList();
            var seen = new Dictionary<string, int>();
            foreach (var b in order)
            {
                string letter = Media.Letter(b.Kind.Medium);
                seen[letter] = seen.TryGetValue(letter, out int n) ? n + 1 : 1;
                Assert.That(b.Label, Is.EqualTo(letter + seen[letter]), $"{puzzle.Id}: {b.Id}");
            }
            foreach (var o in puzzle.Outputs)
            {
                Assert.That(o.Want, Is.Not.Null, $"{puzzle.Id}: {o.Id}");
                // A steady bit can only be 0 or 1, so an input often matches it
                // already; the every-block rule still stops a straight wire solving it.
                if (o.Want.Medium == Medium.Binary && o.Want.IsFlat) continue;
                bool straight = puzzle.Inputs.Any(i => Devices.Same(o.Device, i.Signal.Trace, o.Want));
                Assert.That(straight, Is.False, $"{puzzle.Id}: wiring an input straight to {o.Id} already does what its target shows");
            }
            var goal = GoalText.For(puzzle);
            Assert.That(goal, Is.Not.Empty);
            foreach (var o in puzzle.Outputs) Assert.That(goal, Does.Contain(o.Id), $"the goal names {o.Id}");
        }

        /// <summary>Any block kind's id, as a whole word: hints must never name one.</summary>
        // Labels such as B1 are fine in a hint (that's what {g} becomes); kind ids are not.
        static readonly string KindNames = @"\b(" + string.Join("|", KindCatalog.All.Select(k => k.Id)) + @")\b";

        [Test]
        public void EveryBlockIsTaughtBeforeBuild()
        {
            // Build mixes everything: each one- or two-input kind it uses was met earlier,
            // in Part One or Two, and every such kind is met there at least once. The
            // three-input kinds are new in Build, and only turn up in its later half.
            var met = new HashSet<string>();
            var build = new List<PuzzleDef>();
            foreach (var chapter in TestContent.Library.Chapters)
                foreach (var p in chapter.Puzzles.Where(p => p.Wiring))
                {
                    if (chapter.Part.Id == "build") build.Add(p);
                    else foreach (var b in p.Blocks) met.Add(b.Kind.Id);
                }
            var late = new HashSet<string>();
            for (int i = 0; i < build.Count; i++)
                foreach (var b in build[i].Blocks)
                {
                    if (b.Kind.InputCount == 3)
                    {
                        Assert.That(i, Is.GreaterThanOrEqualTo(build.Count / 2), $"{build[i].Id}: {b.Kind.Id} turns up early in Build");
                        late.Add(b.Kind.Id);
                    }
                    else Assert.That(met, Does.Contain(b.Kind.Id), $"{build[i].Id}: {b.Kind.Id} turns up in Build before it was ever met");
                }
            foreach (var kind in KindCatalog.All.Where(k => !k.Shelved && !KindCatalog.Unplaced.Contains(k.Id)))
            {
                if (kind.InputCount == 3) Assert.That(late, Does.Contain(kind.Id), $"{kind.Id} never turns up late in Build");
                else Assert.That(met, Does.Contain(kind.Id), $"{kind.Id} is never taught");
            }
        }

        [Test]
        public void EveryDeviceIsUsed()
        {
            var used = new HashSet<Device>(WiredPuzzles().SelectMany(p => p.Outputs).Select(o => o.Device));
            foreach (var d in Devices.All.Where(d => !Devices.Shelved.Contains(d))) Assert.That(used, Does.Contain(d), Devices.Noun(d));
        }
    }
}
