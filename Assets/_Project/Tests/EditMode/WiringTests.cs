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

        static Trace Mix(string kind, params Trace[] inputs) => KindCatalog.Get(kind).Apply(inputs, 0);

        static int Pulses(Trace bits) => bits.Runs().Count(r => r.level > 0.5f);

        // ------------------------------------------------------------ mixing blocks

        [Test]
        public void AWaveAndItsHalfLoopTwinCancel()
        {
            var sum = Mix("M1", T("wave sine f2 a7"), T("wave sine f2 a7 p2"));
            Assert.That(sum.Medium, Is.EqualTo(Medium.Wave));
            Assert.That(sum.Matches(Trace.Flat(Medium.Wave, 0f)), Is.True, "silence");
            Assert.That(Mix("M1", T("wave sine f2 a7"), T("wave sine f2 a7 p1")).Matches(Trace.Flat(Medium.Wave, 0f)), Is.False, "a quarter apart they don't cancel");
        }

        [Test]
        public void AddingClipsAtTheTopAndBottom()
        {
            var sum = Mix("M1", T("wave sine f1 a7"), T("wave sine f1 a7"));
            sum.Range(out float low, out float high);
            Assert.That(high, Is.EqualTo(Trace.Peak).Within(0.001f));
            Assert.That(low, Is.EqualTo(-Trace.Peak).Within(0.001f));
        }

        [Test]
        public void FlipTurnsEachKindUpsideDown()
        {
            Assert.That(Mix("M2", T("binary 0110")).Matches(T("binary 1001")), Is.True);
            Assert.That(Mix("M2", T("decimal 2")).Matches(T("decimal 7")), Is.True);
            Assert.That(Mix("M2", T("shape solid triangle")).Matches(T("shape hollow triangle")), Is.True);
            Assert.That(Mix("M2", T("wave sine f3 a5")).Matches(T("wave sine f3 a5 p2")), Is.True);
            Assert.That(Mix("M1", T("wave saw f1 a7"), Mix("M2", T("wave saw f1 a7"))).Matches(Trace.Flat(Medium.Wave, 0f)), Is.True, "a signal and its flip add to nothing");
        }

        [Test]
        public void LaterIsAQuarterOfTheLoop()
        {
            Assert.That(Mix("M3", T("wave sine f1 a7")).Matches(T("wave sine f1 a7 p1")), Is.True, "a quarter of a slow wave's cycle");
            Assert.That(Mix("M3", T("wave sine f4 a7")).Matches(T("wave sine f4 a7")), Is.True, "a whole cycle of a wave that repeats four times");
            Assert.That(Mix("M3", T("binary 1000")).Matches(T("binary 0100")), Is.True);
        }

        [Test]
        public void SamplingHoldsTwelveReadingsALoop()
        {
            var sampled = Mix("M4", T("wave sine f1 a7"));
            Assert.That(sampled.Medium, Is.EqualTo(Medium.Sampled));
            Assert.That(sampled.Runs().Count, Is.LessThanOrEqualTo(12));
            Assert.That(sampled.Matches(T("sampled sine f1 a7 s12")), Is.True, "the same steps as a sampled sine read 12 times a cycle");
            foreach (var (_, _, level) in sampled.Runs()) Assert.That(level, Is.EqualTo(System.Math.Round(level)).Within(0.0001), "every step is a whole level");
        }

        [Test]
        public void AGateLetsTheTopThroughWhileTheBottomIsOn()
        {
            var gated = Mix("M5", T("wave saw f3 a7"), T("binary 0110"));
            for (int k = 0; k < Trace.Steps; k++)
            {
                bool on = k >= 72 && k < 216;
                Assert.That(gated[k], Is.EqualTo(on ? T("wave saw f3 a7")[k] : 0f).Within(0.0001f), $"step {k}");
            }
            Assert.That(Mix("M5", T("decimal 6"), T("binary 0")).Matches(T("decimal 0")), Is.True, "nothing is 0 for a digit");
        }

        [Test]
        public void CountingRises()
        {
            var count = Mix("M6", T("binary 01010101"));
            Assert.That(count.Medium, Is.EqualTo(Medium.Decimal));
            Assert.That(count[Trace.Steps - 1], Is.EqualTo(4f));
            Assert.That(count.Runs().Select(r => r.level), Is.EqualTo(new[] { 0f, 1f, 2f, 3f, 4f }));
            Assert.That(Mix("M6", T("binary 1")).Matches(T("decimal 0")) || Mix("M6", T("binary 1")).Matches(T("decimal 1")), Is.True, "a steady signal rises once at most");
        }

        [Test]
        public void FasterRepeatsTwiceALoop()
        {
            Assert.That(Mix("M7", T("wave sine f1 a7")).Matches(T("wave sine f2 a7")), Is.True);
            Assert.That(Mix("M7", T("binary 01")).Matches(T("binary 0101")), Is.True);
        }

        [Test]
        public void CompareMakesBitsFromWaves()
        {
            var bits = Mix("M8", T("wave sine f1 a7"), T("decimal 7"));
            Assert.That(bits.Medium, Is.EqualTo(Medium.Binary));
            Assert.That(Pulses(bits), Is.EqualTo(1));
            int high = bits.Runs().Where(r => r.level > 0.5f).Sum(r => r.length);
            int low = Mix("M8", T("wave sine f1 a7"), T("decimal 2")).Runs().Where(r => r.level > 0.5f).Sum(r => r.length);
            Assert.That(low, Is.GreaterThan(high), "a lower threshold keeps the light on for longer");
        }

        [Test]
        public void AWaveIntoAnAndBlockBlinks()
        {
            // The idea the Combine tutorial is built on: a wave read as a bit is 1
            // above its middle, so an "and" with a steady 1 blinks at its frequency.
            for (int f = 1; f <= 4; f++)
            {
                var bits = KindCatalog.Get("B2").Apply(new[] { T("binary 1"), T($"wave sine f{f} a7") }, 0);
                Assert.That(Pulses(bits), Is.EqualTo(Waves.Cycles[f - 1]), $"sine f{f}");
            }
        }

        [Test]
        public void WaveBlocksWorkOnPlainWaves()
        {
            Assert.That(KindCatalog.Get("W1").Apply(new[] { T("wave sine f2 a5") }, 0).Matches(T("wave sine f3 a5")), Is.True, "one step faster");
            Assert.That(KindCatalog.Get("W1").Apply(new[] { T("wave sine f2 a5") }, 0).Value, Is.Not.Null, "and still a plain wave, so another wave block can read it");
            Assert.That(KindCatalog.Get("W3").Apply(new[] { T("wave sine f3 a2"), T("wave square f1 a7") }, 0).Matches(T("wave sine f3 a7")), Is.True);
            Assert.That(KindCatalog.Get("Q1").Apply(new[] { T("sampled sine f1 a7 s3") }, 0).Matches(T("sampled sine f1 a7 s6")), Is.True);
            Assert.That(KindCatalog.Get("W1").Apply(new[] { T("binary 1") }, 0), Is.Null, "a bit isn't a wave");
            Assert.That(KindCatalog.Get("W1").Apply(new[] { T("sampled sine f1 a7 s3") }, 0), Is.Null, "a sampled wave isn't an analog one");
            Assert.That(KindCatalog.Get("W1").Apply(new[] { Mix("M2", T("wave sine f2 a5")) }, 0), Is.Null, "a mixed wave has no settings to change");
            Assert.That(KindCatalog.Get("W1").Reads(new[] { T("wave saw f1 a1") }), Is.True);
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
            Assert.That(KindCatalog.Get("S1").Apply(new[] { pink }, 0), Is.EqualTo(Shapes.Make(Shapes.Triangle, false, 1)));
            Assert.That(KindCatalog.Get("S2").Apply(new[] { pink }, 0), Is.EqualTo(Shapes.Make(Shapes.Square, true, 1)));
            int green = Shapes.Make(Shapes.Hexagon, false, 3);
            Assert.That(KindCatalog.Get("S3").Apply(new[] { pink, green }, 0), Is.EqualTo(Shapes.Make(Shapes.Triangle, false, 1)), "form and colour of top, fill of bottom");
            Assert.That(KindCatalog.Get("S4").Apply(new[] { pink, green }, 0), Is.EqualTo(green), "the one with more corners, whole");
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
            Assert.That(KindCatalog.Get("B1").Apply(new[] { T("decimal 0") }, 0).Matches(T("binary 1")), Is.True, "a digit is 1 unless it's 0");
            Assert.That(KindCatalog.Get("B1").Apply(new[] { T("shape hollow square") }, 0).Matches(T("binary 1")), Is.True, "a hollow shape is 0");
            Assert.That(KindCatalog.Get("D1").Apply(new[] { T("shape solid triangle") }, 0).Matches(T("decimal 4")), Is.True, "a shape is its corners");
            Assert.That(KindCatalog.Get("D1").Apply(new[] { T("binary 01") }, 0).Matches(T("decimal 12")), Is.True, "a bit is 0 or 1, step by step");
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
            ""blocks"": { ""g"": { ""kind"": ""B2"" }, ""h"": { ""kind"": ""B1"" } },
            ""outputs"": { ""X"": ""display"" },
            ""solution"": [ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]
        }";

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
            Assert.That(report.RulesKept, Is.True);
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
            Refused(Small.Replace(@"""kind"": ""B1""", @"""kind"": ""W1"""), "a wave block fed a bit sends nothing", "sends nothing");
            Refused(Small.Replace(@"""g -> h"", ", ""), "the solution must use every block", "nothing wired into it");
            Refused(Small.Replace(@"[ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]", @"[ ""A -> g.0"", ""B -> g.1"", ""A -> h"", ""g -> X"" ]"), "a block that leads nowhere", "doesn't use block 'h'");
            Refused(Small.Replace(@"""solution"": [ ""A -> g.0"", ""B -> g.1"", ""g -> h"", ""h -> X"" ]", @"""hint"": ""none"""), "no solution", "solution");
            Refused(Small.Replace(@"""outputs"":", @"""probes"": 2, ""outputs"":"), "no probes", "probes");
            Refused(Small.Replace(@"""X"": ""display""", @"""X"": ""toaster"""), "unknown device", "toaster");
            foreach (var d in new[] { "light", "speaker", "digits", "fan", "servo" })
                Refused(Small.Replace(@"""X"": ""display""", $@"""X"": ""{d}"""), $"a {d} is set aside", "set aside");
            Refused(Small.Replace(@"""A"": ""binary 1""", @"""A"": ""binary 11011"""), "a pattern that doesn't fit", "doesn't fit");
            Refused(Small.Replace(@"""B"": ""wave sine f1 a7""", @"""B"": ""sampled sine f1 a7 s6"""), "stepped waves are set aside", "set aside");
            Refused(Small.Replace(@"""kind"": ""B1""", @"""kind"": ""M4"""), "the sampler is set aside", "set aside");

            var p = TestContent.ParseWired(Small);
            Assert.That(p.Wiring, Is.True);
            Assert.That(p.Blocks.All(b => b.Locked), Is.True, "the player wires blocks but never sets them");
            Assert.That(p.Outputs[0].Device, Is.EqualTo(Device.Display));
            Assert.That(p.Outputs[0].Want, Is.Not.Null, "the target comes from the solution");
            Assert.That(TestContent.ParseWired(Small, marked: false).Marked, Is.False);
        }

        [Test]
        public void BlocksAreNumberedNotNamed()
        {
            // The player sees K1, K2 ... on blocks, never their kind, and hints name
            // blocks as {id} so they always match the numbers on the board.
            var hinted = Small.Replace(@"""name"": ""Test"",", @"""name"": ""Test"", ""hint"": ""Wire {g}, then {h}."",");
            var p = TestContent.ParseWired(hinted);
            Assert.That(p.Blocks.Select(b => b.Label), Is.EqualTo(new[] { "K1", "K2" }));
            Assert.That(p.Hint, Is.EqualTo("Wire K1, then K2."));
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
            Assert.That(puzzle.Blocks.All(b => b.Kind.Wireable), Is.True);
            Assert.That(puzzle.Hint, Is.Not.Empty, puzzle.Id);
            Assert.That(System.Text.RegularExpressions.Regex.IsMatch(puzzle.Hint, @"\b[BDSWQM][1-8]\b"), Is.False, $"{puzzle.Id}: the hint names a kind code: {puzzle.Hint}");
            Assert.That(puzzle.Hint, Does.Not.Contain("{"), $"{puzzle.Id}: the hint names a block the loader didn't label");
            for (int b = 0; b < puzzle.Blocks.Count; b++)
                Assert.That(puzzle.Blocks[b].Label, Is.EqualTo("K" + (b + 1)), $"{puzzle.Id}: blocks are numbered in order");
            foreach (var o in puzzle.Outputs)
            {
                Assert.That(o.Want, Is.Not.Null, $"{puzzle.Id}: {o.Id}");
                bool straight = puzzle.Inputs.Any(i => Devices.Same(o.Device, i.Signal.Trace, o.Want));
                Assert.That(straight, Is.False, $"{puzzle.Id}: wiring an input straight to {o.Id} already does what its target shows");
            }
            var goal = GoalText.For(puzzle);
            Assert.That(goal, Is.Not.Empty);
            foreach (var o in puzzle.Outputs) Assert.That(goal, Does.Contain(o.Id), $"the goal names {o.Id}");
        }

        [Test]
        public void BlocksAreNamedBeforeTheyLoseTheirNames()
        {
            // Every kind an unmarked circuit uses was met by name earlier: in Part
            // One for the bit, digit and shape kinds, in Combine for the M kinds.
            var named = new HashSet<string>();
            foreach (var p in TestContent.Library.AllPuzzles)
            {
                if (!p.Wiring || p.Marked)
                {
                    foreach (var b in p.Blocks) named.Add(b.Kind.Id);
                    continue;
                }
                foreach (var b in p.Blocks) Assert.That(named, Does.Contain(b.Kind.Id), $"{p.Id}: {b.Kind.Id} turns up unnamed before it was ever named");
            }
            foreach (var kind in KindCatalog.All.Where(k => k.Mixing && !k.Shelved))
                Assert.That(named, Does.Contain(kind.Id), $"{kind.Id} is taught in Combine");
        }

        [Test]
        public void EveryDeviceIsUsed()
        {
            var used = new HashSet<Device>(WiredPuzzles().SelectMany(p => p.Outputs).Select(o => o.Device));
            foreach (var d in Devices.All.Where(d => !Devices.Shelved.Contains(d))) Assert.That(used, Does.Contain(d), Devices.Noun(d));
        }
    }
}
