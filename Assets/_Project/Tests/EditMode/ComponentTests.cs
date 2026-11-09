using System.Linq;
using Blackbox.Components;
using NUnit.Framework;

namespace Blackbox.Tests
{
    public class ComponentTests
    {
        static bool[] Run(string kind, int setting, params bool[] ins)
        {
            var logic = KindCatalog.Get(kind).CreateLogic(setting);
            logic.Reset();
            return new[] { logic.Step(ins) };
        }

        static string Sequence(string kind, int setting, string input, string input2 = null)
        {
            var logic = KindCatalog.Get(kind).CreateLogic(setting);
            logic.Reset();
            var output = new char[input.Length];
            for (int t = 0; t < input.Length; t++)
            {
                var ins = input2 == null ? new[] { input[t] == '1' } : new[] { input[t] == '1', input2[t] == '1' };
                output[t] = logic.Step(ins) ? '1' : '0';
            }
            return new string(output);
        }

        static string Table(string kind, int setting)
        {
            var k = KindCatalog.Get(kind);
            var chars = new char[1 << k.InputCount];
            for (int i = 0; i < chars.Length; i++)
            {
                var ins = new bool[k.InputCount];
                for (int p = 0; p < k.InputCount; p++) ins[p] = ((i >> (k.InputCount - 1 - p)) & 1) == 1;
                var logic = k.CreateLogic(setting);
                logic.Reset();
                chars[i] = logic.Step(ins) ? '1' : '0';
            }
            return new string(chars);
        }

        [Test]
        public void EveryKindHasSettingsAndUniqueBehaviours()
        {
            Assert.That(KindCatalog.All.Count, Is.GreaterThanOrEqualTo(9));
            foreach (var kind in KindCatalog.All)
            {
                Assert.That(kind.SettingCount, Is.GreaterThan(0), kind.Id);
                Assert.That(kind.Spoilers.Distinct().Count(), Is.EqualTo(kind.SettingCount), kind.Id);
            }
        }

        [Test]
        public void LogicKindsMatchTheirTruthTables()
        {
            Assert.That(Table("K1", 0), Is.EqualTo("10"));
            Assert.That(Table("K1", 1), Is.EqualTo("01"));
            Assert.That(Table("K2", 0), Is.EqualTo("11"));
            Assert.That(Table("K2", 2), Is.EqualTo("00"));
            Assert.That(Table("K3", 0), Is.EqualTo("0111"));
            Assert.That(Table("K3", 1), Is.EqualTo("0001"));
            Assert.That(Table("K4", 0), Is.EqualTo("0110"));
            Assert.That(Table("K4", 2), Is.EqualTo("0010"), "top and not bottom: only 10 is on");
        }

        [Test]
        public void ThresholdCountsInputs()
        {
            Assert.That(Table("K5", 0), Is.EqualTo("01111111"));
            Assert.That(Table("K5", 1), Is.EqualTo("00010111"));
            Assert.That(Table("K5", 2), Is.EqualTo("00000001"));
        }

        [Test]
        public void DelayRepeatsInputLater()
        {
            Assert.That(Sequence("K6", 0, "1011000"), Is.EqualTo("0101100"));
            Assert.That(Sequence("K6", 1, "1011000"), Is.EqualTo("0010110"));
            Assert.That(Sequence("K6", 2, "1011000"), Is.EqualTo("0001011"));
            Assert.That(KindCatalog.Get("K6").CreateLogic(0).Registered, Is.True);
        }

        [Test]
        public void EdgeWatchesChanges()
        {
            Assert.That(Sequence("K7", 0, "0110100"), Is.EqualTo("0101110"));
            Assert.That(Sequence("K7", 1, "0110100"), Is.EqualTo("0100100"));
            Assert.That(Sequence("K7", 2, "0110100"), Is.EqualTo("0001010"));
            Assert.That(Sequence("K7", 1, "1100"), Is.EqualTo("1000"), "starts as if the input was off");
        }

        [Test]
        public void ToggleFlips()
        {
            Assert.That(Sequence("K8", 0, "1110011"), Is.EqualTo("1011101"));
            Assert.That(Sequence("K8", 1, "1110011"), Is.EqualTo("1111100"));
        }

        [Test]
        public void LatchSetsAndClears()
        {
            Assert.That(Sequence("K9", 0, "0100100", "0001000"), Is.EqualTo("0110111"));
            Assert.That(Sequence("K9", 1, "0001000", "0100100"), Is.EqualTo("0110111"));
            Assert.That(Sequence("K9", 0, "110", "010"), Is.EqualTo("111"), "both on keeps the bit");
        }

        [Test]
        public void ResetForgetsState()
        {
            var logic = KindCatalog.Get("K8").CreateLogic(0);
            logic.Reset();
            logic.Step(new[] { true });
            logic.Reset();
            Assert.That(logic.Step(new[] { false }), Is.False);
        }
    }
}
