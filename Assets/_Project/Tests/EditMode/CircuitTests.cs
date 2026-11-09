using Blackbox.Circuits;
using Blackbox.Puzzles;
using NUnit.Framework;

namespace Blackbox.Tests
{
    public class CircuitTests
    {
        const string Nand = @"{
            ""id"": ""t-nand"", ""name"": ""Nand"",
            ""inputs"": { ""A"": [0, 0], ""B"": [0, 1] },
            ""boxes"": { ""g"": { ""kind"": ""K3"", ""setting"": 0, ""at"": [1, 0.5] },
                         ""n"": { ""kind"": ""K1"", ""setting"": 1, ""at"": [2, 0.5] } },
            ""outputs"": { ""X"": [3, 0.5] },
            ""wires"": [ ""A -> g.0"", ""B -> g.1"", ""g -> n"", ""n -> X"" ],
            ""table"": [ ""00 > 1"", ""01 > 1"", ""10 > 1"", ""11 > 0"" ]
        }";

        [Test]
        public void TableCircuitEvaluatesEveryRow()
        {
            var p = TestContent.Parse(Nand);
            var circuit = new CompiledCircuit(p);

            var run = circuit.Run(new[] { 0, 1 }); // or, pass
            Assert.That(run.Cases.Length, Is.EqualTo(4));
            Assert.That(run.Cases[0].Output(0, 0), Is.False);
            Assert.That(run.Cases[3].Output(0, 0), Is.True);
            Assert.That(run.AllMatched, Is.False);

            Assert.That(circuit.Run(new[] { 1, 0 }).AllMatched, Is.True, "and, invert is nand");
            Assert.That(circuit.Passes(new[] { 1, 0 }), Is.True);
        }

        [Test]
        public void BoxInputsAreVisibleForProbing()
        {
            var p = TestContent.Parse(Nand);
            var run = new CompiledCircuit(p).Run(new[] { 1, 0 });
            var row = run.Cases[2]; // A=1, B=0
            Assert.That(row.BoxInputs(0, 0), Is.EqualTo(new[] { true, false }));
            Assert.That(row.BoxOutput(0, 0), Is.False);
            Assert.That(row.BoxInputs(1, 0), Is.EqualTo(new[] { false }));
            Assert.That(row.BoxOutput(1, 0), Is.True);
        }

        [Test]
        public void DelayLoopOscillates()
        {
            // A delay feeding an inverter feeding the delay: 0 1 0 1 ...
            var p = TestContent.Parse(@"{
                ""id"": ""t-clock"",
                ""inputs"": { ""A"": [0, 0] },
                ""boxes"": { ""d"": { ""kind"": ""K6"", ""setting"": 0, ""at"": [1, 0] },
                             ""n"": { ""kind"": ""K1"", ""setting"": 0, ""at"": [2, 0] },
                             ""g"": { ""kind"": ""K3"", ""setting"": 1, ""at"": [3, 0] } },
                ""outputs"": { ""X"": [4, 0] },
                ""wires"": [ ""d -> n"", { ""from"": ""n"", ""to"": ""d"", ""via"": 1 }, ""A -> g.0"", ""d -> g.1"", ""g -> X"" ],
                ""trace"": { ""A"": ""111111"", ""X"": ""010101"" }
            }");
            var circuit = new CompiledCircuit(p);
            Assert.That(circuit.Run(p.StartSettings()).AllMatched, Is.True);
            Assert.That(circuit.Passes(new[] { 1, 0, 1 }), Is.False, "a two tick delay makes a slower rhythm");
        }

        [Test]
        public void LoopWithoutDelayIsRejected()
        {
            var p = TestContent.Parse(@"{
                ""id"": ""t-loop"",
                ""inputs"": { ""A"": [0, 0] },
                ""boxes"": { ""a"": { ""kind"": ""K3"", ""at"": [1, 0] }, ""b"": { ""kind"": ""K1"", ""at"": [2, 0] } },
                ""outputs"": { ""X"": [3, 0] },
                ""wires"": [ ""A -> a.0"", { ""from"": ""b"", ""to"": ""a.1"", ""via"": 1 }, ""a -> b"", ""b -> X"" ],
                ""table"": [ ""0 > 0"", ""1 > 1"" ]
            }");
            Assert.Throws<CircuitException>(() => new CompiledCircuit(p));
        }

        [Test]
        public void LoaderExplainsMistakes()
        {
            var e = Assert.Throws<PuzzleFormatException>(() => TestContent.Parse(@"{
                ""id"": ""bad"", ""inputs"": { ""A"": null }, ""outputs"": { ""X"": null },
                ""boxes"": { ""g"": { ""kind"": ""K3"" } },
                ""wires"": [ ""A -> g"", ""g -> X"" ], ""table"": [ ""0 > 0"" ] }"));
            StringAssert.Contains("name the port", e.Message);

            e = Assert.Throws<PuzzleFormatException>(() => TestContent.Parse(@"{
                ""id"": ""bad"", ""inputs"": { ""A"": null }, ""outputs"": { ""X"": null },
                ""wires"": [ ""A -> X"" ], ""table"": [ ""01 > 0"" ] }"));
            StringAssert.Contains("2 input values", e.Message);

            e = Assert.Throws<PuzzleFormatException>(() => TestContent.Parse(@"{
                ""id"": ""bad"", ""inputs"": { ""A"": null }, ""outputs"": { ""X"": null },
                ""boxes"": { ""g"": { ""kind"": ""K99"" } },
                ""wires"": [ ""A -> X"" ], ""table"": [ ""0 > 0"" ] }"));
            StringAssert.Contains("unknown kind", e.Message);
        }

        [Test]
        public void AutoLayoutPlacesUnpositionedNodes()
        {
            var p = TestContent.Parse(@"{
                ""id"": ""auto"", ""inputs"": { ""A"": null, ""B"": null }, ""outputs"": { ""X"": null },
                ""boxes"": { ""g"": { ""kind"": ""K3"" }, ""n"": { ""kind"": ""K1"" } },
                ""wires"": [ ""A -> g.0"", ""B -> g.1"", ""g -> n"", ""n -> X"" ], ""table"": [ ""00 > 1"" ] }");
            Assert.That(p.Inputs[0].At.Col, Is.EqualTo(0));
            Assert.That(p.Find("g").At.Col, Is.EqualTo(1));
            Assert.That(p.Find("n").At.Col, Is.EqualTo(2));
            Assert.That(p.Outputs[0].At.Col, Is.EqualTo(3));
            Assert.That(p.Inputs[0].At.Row, Is.LessThan(p.Inputs[1].At.Row));
        }

        [Test]
        public void SessionTracksRunsUndoAndSolving()
        {
            var session = new PuzzleSession(TestContent.Parse(Nand));
            Assert.That(session.Run().AllMatched, Is.False);
            Assert.That(session.RunIsCurrent, Is.True);

            Assert.That(session.Cycle(0, 1), Is.True);
            Assert.That(session.RunIsCurrent, Is.False, "changing a box makes the last run stale");
            Assert.That(session.SolvedNow, Is.False);

            session.Cycle(1, 1);
            Assert.That(session.Run().AllMatched, Is.True);
            Assert.That(session.SolvedNow, Is.True);
            Assert.That(session.Tests, Is.EqualTo(2));

            Assert.That(session.Undo(), Is.True);
            Assert.That(session.SettingOf(1), Is.EqualTo(1));
            Assert.That(session.SolvedNow, Is.False);
            Assert.That(session.EverSolved, Is.True);

            session.ResetToStart();
            Assert.That(session.AtStart, Is.True);
            Assert.That(session.LastRun, Is.Null);
        }

        [Test]
        public void LockedBoxesCannotChange()
        {
            var p = TestContent.Parse(Nand.Replace(@"""setting"": 1, ""at"": [2, 0.5]", @"""setting"": 1, ""locked"": true, ""at"": [2, 0.5]"));
            var session = new PuzzleSession(p, new[] { 1, 0 });
            Assert.That(session.SettingOf(1), Is.EqualTo(1), "resume can't move a locked box");
            Assert.That(session.Cycle(1, 1), Is.False);
            Assert.That(session.SettingOf(0), Is.EqualTo(1));
        }
    }
}
