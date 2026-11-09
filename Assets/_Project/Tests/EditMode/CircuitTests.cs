using System.Linq;
using Blackbox.Components;
using Blackbox.Core;
using Blackbox.Puzzles;
using NUnit.Framework;

namespace Blackbox.Tests
{
    /// <summary>Loading circuits, evaluating them, and playing them through a session.</summary>
    public class CircuitTests
    {
        // A fixed input B and an adjustable input A feed an and/or block g, whose
        // signal also passes through a locked inverter k. g must send 1 and X must
        // show 0, so g has to be set to "or", or A set to 1.
        const string Fork = @"{
            ""id"": ""fork"", ""name"": ""Fork"", ""probes"": 2,
            ""inputs"": {
                ""A"": { ""at"": [0, 0], ""value"": 0, ""adjustable"": true },
                ""B"": { ""at"": [0, 1], ""value"": 1 }
            },
            ""blocks"": {
                ""g"": { ""kind"": ""B2"", ""state"": 0, ""at"": [1, 0], ""target"": 1 },
                ""k"": { ""kind"": ""B1"", ""state"": 0, ""locked"": true, ""at"": [2, 0] }
            },
            ""outputs"": {
                ""X"": { ""at"": [3, 0], ""target"": 0 },
                ""Y"": [3, 1]
            },
            ""wires"": [""A -> g.0"", ""B -> g.1"", ""g -> k"", ""k -> X"", ""B -> Y""]
        }";

        static PuzzleDef Load(string json = Fork, Medium medium = Medium.Binary) => TestContent.Parse(json, medium);

        [Test]
        public void LoadsNodesWiresAndTargets()
        {
            var p = Load();
            Assert.That(p.Inputs.Select(i => i.Id), Is.EqualTo(new[] { "A", "B" }));
            Assert.That(p.Blocks.Select(b => b.Id), Is.EqualTo(new[] { "g", "k" }));
            Assert.That(p.Outputs.Select(o => o.Id), Is.EqualTo(new[] { "X", "Y" }));
            Assert.That(p.Wires.Count, Is.EqualTo(5));
            Assert.That(p.Wires[1].ToPort, Is.EqualTo(1));
            Assert.That(p.Targets.Count, Is.EqualTo(2), "g and X; Y has no target");
            Assert.That(p.Targets.Any(t => t.Node.Id == "g" && t.Want == 1), Is.True);
            Assert.That(p.InputChangeable(0), Is.True);
            Assert.That(p.InputChangeable(1), Is.False);
            Assert.That(p.BlockChangeable(1), Is.False, "k is locked");
        }

        [Test]
        public void EvaluatesThroughTheBlocks()
        {
            var p = Load();
            var circuit = new Blackbox.Circuits.CompiledCircuit(p);
            var values = circuit.Evaluate(new[] { 0, 1 }, new[] { 0, 0 });
            Assert.That(values[circuit.BlockNode(0)], Is.EqualTo(0), "0 and 1");
            Assert.That(values[circuit.OutputNode(0)], Is.EqualTo(1), "inverted");
            Assert.That(values[circuit.OutputNode(1)], Is.EqualTo(1), "B passes straight to Y");
            values = circuit.Evaluate(new[] { 0, 1 }, new[] { 1, 0 });
            Assert.That(values[circuit.OutputNode(0)], Is.EqualTo(0), "0 or 1, inverted");
        }

        [Test]
        public void DownstreamFollowsTheWires()
        {
            var circuit = new Blackbox.Circuits.CompiledCircuit(Load());
            var down = circuit.Downstream(0);
            Assert.That(down[circuit.BlockNode(0)], Is.True);
            Assert.That(down[circuit.OutputNode(0)], Is.True);
            Assert.That(down[circuit.OutputNode(1)], Is.False, "A never reaches Y");
        }

        [Test]
        public void SessionSolvesWhenEveryTargetMatches()
        {
            var s = new PuzzleSession(Load());
            Assert.That(s.Solved, Is.False);
            Assert.That(s.MatchedTargets, Is.EqualTo(0));
            Assert.That(s.AtStart, Is.True);

            Assert.That(s.CycleState(0, 1), Is.True);
            Assert.That(s.Solved, Is.True, "g on or: g sends 1, X shows 0");
            Assert.That(s.Moves, Is.EqualTo(1));
            Assert.That(s.EverSolved, Is.True);

            Assert.That(s.Undo(), Is.True);
            Assert.That(s.Solved, Is.False);
            Assert.That(s.State(0), Is.EqualTo(0));

            Assert.That(s.SetInput(0, 1), Is.True);
            Assert.That(s.Solved, Is.True, "A at 1: 1 and 1");
        }

        [Test]
        public void FixedInputsAndLockedBlocksDontChange()
        {
            var s = new PuzzleSession(Load());
            Assert.That(s.CycleInput(1, 1), Is.False, "B is fixed");
            Assert.That(s.CycleState(1, 1), Is.False, "k is locked");
            Assert.That(s.SetState(1, 1), Is.False);
            Assert.That(s.Moves, Is.EqualTo(0));
            Assert.That(s.CanUndo, Is.False);
        }

        [Test]
        public void InputsWrapRoundTheirMedium()
        {
            const string json = @"{
                ""id"": ""count"", ""inputs"": { ""A"": { ""at"": [0, 0], ""value"": 9, ""adjustable"": true } },
                ""blocks"": { ""b"": { ""kind"": ""D1"", ""at"": [1, 0] } },
                ""outputs"": { ""X"": { ""at"": [2, 0], ""target"": 5 } },
                ""wires"": [""A -> b"", ""b -> X""]
            }";
            var s = new PuzzleSession(Load(json, Medium.Decimal));
            Assert.That(s.ValueOf(s.Puzzle.Outputs[0]), Is.EqualTo(0), "9 + 1 wraps to 0");
            s.CycleInput(0, 1);
            Assert.That(s.InputValue(0), Is.EqualTo(0));
            s.CycleInput(0, -1);
            Assert.That(s.InputValue(0), Is.EqualTo(9));
            s.CycleState(0, -1);
            Assert.That(s.State(0), Is.EqualTo(2), "states wrap too");
        }

        [Test]
        public void ResetPutsInputsAndBlocksBackButKeepsProbes()
        {
            var s = new PuzzleSession(Load());
            int resets = 0;
            s.Reset += () => resets++;
            s.SetInput(0, 1);
            s.SetState(0, 1);
            s.AddProbe(2, 0.5f);
            s.ResetToStart();
            Assert.That(resets, Is.EqualTo(1));
            Assert.That(s.AtStart, Is.True);
            Assert.That(s.CanUndo, Is.False);
            Assert.That(s.Probes.Count, Is.EqualTo(1));
        }

        [Test]
        public void ProbesReadTheirWireAndAreLimited()
        {
            var s = new PuzzleSession(Load());
            int changes = 0;
            s.ProbesChanged += () => changes++;
            Assert.That(s.ProbesLeft, Is.EqualTo(2));
            Assert.That(s.AddProbe(2, 0.5f), Is.True, "on g -> k");
            Assert.That(s.ProbeValue(0), Is.EqualTo(0), "g sends 0 and 1 = 0");
            s.SetState(0, 1);
            Assert.That(s.ProbeValue(0), Is.EqualTo(1), "probes read live");
            Assert.That(s.AddProbe(3, 2f), Is.True);
            Assert.That(s.Probes[1].At, Is.EqualTo(1f), "clamped onto the wire");
            Assert.That(s.AddProbe(0, 0.5f), Is.False, "only two probes");
            Assert.That(s.ProbesLeft, Is.EqualTo(0));
            Assert.That(s.RemoveProbe(0), Is.True);
            Assert.That(s.ProbesLeft, Is.EqualTo(1));
            Assert.That(changes, Is.EqualTo(3));
            Assert.That(s.Moves, Is.EqualTo(1), "probing isn't a move");
        }

        [Test]
        public void SnapshotResumesWhereThePlayerLeft()
        {
            var p = Load();
            var s = new PuzzleSession(p);
            s.SetInput(0, 1);
            s.SetState(0, 1);
            s.AddProbe(3, 0.25f);
            var snap = s.Snapshot();

            var again = new PuzzleSession(p, snap);
            Assert.That(again.InputValue(0), Is.EqualTo(1));
            Assert.That(again.State(0), Is.EqualTo(1));
            Assert.That(again.Probes.Count, Is.EqualTo(1));
            Assert.That(again.Probes[0].Wire, Is.EqualTo(3));
            Assert.That(again.Probes[0].At, Is.EqualTo(0.25f).Within(0.001f));
        }

        [Test]
        public void ResumeIgnoresWhatThePlayerCantChange()
        {
            var p = Load();
            var damaged = new CircuitState { Inputs = new[] { 1, 0 }, States = new[] { 1, 1 } };
            damaged.Probes.Add((99, 500));
            for (int i = 0; i < 5; i++) damaged.Probes.Add((0, 500));
            var s = new PuzzleSession(p, damaged);
            Assert.That(s.InputValue(1), Is.EqualTo(1), "B is fixed at 1");
            Assert.That(s.State(1), Is.EqualTo(0), "k is locked on its first state");
            Assert.That(s.Probes.Count, Is.EqualTo(2), "no more probes than the circuit allows, none on missing wires");

            var wrongShape = new CircuitState { Inputs = new[] { 1 }, States = new[] { 5, 0 } };
            s = new PuzzleSession(p, wrongShape);
            Assert.That(s.AtStart, Is.True);
        }

        [Test]
        public void ReturnTargetsSitOnInputs()
        {
            // A starts where it must end, so it has to be changed and put back.
            const string json = @"{
                ""id"": ""return"",
                ""inputs"": { ""A"": { ""at"": [0, 0], ""value"": 1, ""adjustable"": true, ""target"": 1 } },
                ""blocks"": { ""b"": { ""kind"": ""B1"", ""at"": [1, 0] } },
                ""outputs"": { ""X"": { ""at"": [2, 0], ""target"": 1 } },
                ""wires"": [""A -> b"", ""b -> X""]
            }";
            var s = new PuzzleSession(Load(json));
            Assert.That(s.MatchedTargets, Is.EqualTo(1), "A is already where it must end");
            s.SetInput(0, 0);
            Assert.That(s.Solved, Is.False, "X shows 1, but A is wrong");
            s.SetInput(0, 1);
            s.SetState(0, 1);
            Assert.That(s.Solved, Is.True);
        }

        [Test]
        public void GoalSentenceNamesEveryTarget()
        {
            var goal = GoalText.For(Load());
            Assert.That(goal, Does.Contain("X show 0"));
            Assert.That(goal, Does.Contain("block g send 1"));

            const string json = @"{
                ""id"": ""shapes"",
                ""inputs"": { ""A"": { ""at"": [0, 0], ""value"": ""solid square"", ""adjustable"": true, ""target"": ""solid square"" } },
                ""blocks"": { ""b"": { ""kind"": ""S1"", ""at"": [1, 0] } },
                ""outputs"": { ""X"": { ""at"": [2, 0], ""target"": ""hollow triangle"" } },
                ""wires"": [""A -> b"", ""b -> X""]
            }";
            goal = GoalText.For(Load(json, Medium.Shape));
            Assert.That(goal, Does.Contain("hollow triangle"));
            Assert.That(goal, Does.Contain("End with A at"));
        }

        [TestCase(@"""wires"": [""A -> X"", ""b -> b2"", ""b2 -> b""]", "loop")]
        [TestCase(@"""wires"": [""A -> b"", ""A -> X"", ""b -> X""]", "two wires")]
        [TestCase(@"""wires"": [""A -> b""]", "nothing wired")]
        [TestCase(@"""wires"": [""A -> b"", ""b -> Q""]", "nothing is called")]
        [TestCase(@"""wires"": [""A -> b"", ""X -> b2"", ""b -> X""]", "can't drive")]
        public void BrokenWiringIsRejected(string wires, string message)
        {
            string json = @"{
                ""id"": ""broken"",
                ""inputs"": { ""A"": [0, 0] },
                ""blocks"": { ""b"": { ""kind"": ""B1"", ""at"": [1, 0] }, ""b2"": { ""kind"": ""B1"", ""at"": [1, 1] } },
                ""outputs"": { ""X"": [2, 0] },
                " + wires + @"
            }";
            var e = Assert.Throws<PuzzleFormatException>(() => Load(json));
            Assert.That(e.Message, Does.Contain(message));
        }

        [Test]
        public void ValuesAndKindsMustSuitTheMedium()
        {
            const string wrongKind = @"{
                ""id"": ""k"", ""inputs"": { ""A"": [0, 0] },
                ""blocks"": { ""b"": { ""kind"": ""D1"", ""at"": [1, 0] } },
                ""outputs"": { ""X"": [2, 0] }, ""wires"": [""A -> b"", ""b -> X""]
            }";
            Assert.That(Assert.Throws<PuzzleFormatException>(() => Load(wrongKind)).Message, Does.Contain("decimal"));

            const string wrongValue = @"{
                ""id"": ""v"", ""inputs"": { ""A"": { ""at"": [0, 0], ""value"": 3 } },
                ""outputs"": { ""X"": [1, 0] }, ""wires"": [""A -> X""]
            }";
            Assert.Throws<PuzzleFormatException>(() => Load(wrongValue));

            const string wrongState = @"{
                ""id"": ""s"", ""inputs"": { ""A"": [0, 0] },
                ""blocks"": { ""b"": { ""kind"": ""B1"", ""state"": 2, ""at"": [1, 0] } },
                ""outputs"": { ""X"": [2, 0] }, ""wires"": [""A -> b"", ""b -> X""]
            }";
            Assert.That(Assert.Throws<PuzzleFormatException>(() => Load(wrongState)).Message, Does.Contain("out of range"));
        }
    }
}
