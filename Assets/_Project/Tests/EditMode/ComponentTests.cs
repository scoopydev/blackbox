using System.Collections.Generic;
using System.Linq;
using Blackbox.Components;
using NUnit.Framework;

namespace Blackbox.Tests
{
    /// <summary>The block kinds and the four signal media.</summary>
    public class ComponentTests
    {
        static IEnumerable<BlockKind> AllKinds() => KindCatalog.All;

        static int Apply(string kind, int state, params int[] inputs) => KindCatalog.Get(kind).Apply(inputs, state);

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
        public void EachMediumHasFourKinds()
        {
            foreach (Medium m in new[] { Medium.Binary, Medium.Decimal, Medium.Shape, Medium.Wave })
                Assert.That(KindCatalog.All.Count(k => k.Medium == m), Is.EqualTo(4), m.ToString());
        }

        [TestCaseSource(nameof(AllKinds))]
        public void KindIsWellFormed(BlockKind kind)
        {
            Assert.That(kind.InputCount, Is.InRange(1, 2));
            Assert.That(kind.StateCount, Is.GreaterThanOrEqualTo(2));
            Assert.That(kind.Spoilers.Count, Is.EqualTo(kind.StateCount));
            Assert.That(KindCatalog.Get(kind.Id), Is.SameAs(kind));
        }

        [TestCaseSource(nameof(AllKinds))]
        public void KindAlwaysSendsAValueOfItsMedium(BlockKind kind)
        {
            foreach (var inputs in InputsFor(kind))
                for (int s = 0; s < kind.StateCount; s++)
                {
                    int v = kind.Apply(inputs, s);
                    Assert.That(Media.IsValid(kind.Medium, v), Is.True, $"{kind.Id} state {s} on [{string.Join(",", inputs)}] gave {v}");
                }
        }

        [TestCaseSource(nameof(AllKinds))]
        public void EveryStateBehavesDifferently(BlockKind kind)
        {
            // If two states agree on every input, one of them is a wasted click.
            for (int s = 0; s < kind.StateCount; s++)
                for (int t = s + 1; t < kind.StateCount; t++)
                {
                    bool differ = InputsFor(kind).Any(x => kind.Apply(x, s) != kind.Apply(x, t));
                    Assert.That(differ, Is.True, $"{kind.Id}: states {s} and {t} are the same");
                }
        }

        [Test]
        public void BinaryRules()
        {
            Assert.That(Apply("B1", 0, 1), Is.EqualTo(0), "invert");
            Assert.That(Apply("B1", 1, 1), Is.EqualTo(1), "pass");
            Assert.That(Apply("B2", 0, 1, 0), Is.EqualTo(0), "and");
            Assert.That(Apply("B2", 1, 1, 0), Is.EqualTo(1), "or");
            Assert.That(Apply("B3", 0, 0), Is.EqualTo(1), "always 1");
            Assert.That(Apply("B3", 2, 1), Is.EqualTo(0), "always 0");
            Assert.That(Apply("B4", 0, 1, 1), Is.EqualTo(0), "xor");
            Assert.That(Apply("B4", 2, 1, 0), Is.EqualTo(1), "top and not bottom");
            Assert.That(Apply("B4", 2, 1, 1), Is.EqualTo(0), "top and not bottom");
        }

        [Test]
        public void DecimalRulesWrap()
        {
            Assert.That(Apply("D1", 0, 9), Is.EqualTo(0), "add 1 wraps");
            Assert.That(Apply("D1", 2, 8), Is.EqualTo(1), "add 3 wraps");
            Assert.That(Apply("D2", 0, 7, 5), Is.EqualTo(2), "sum wraps");
            Assert.That(Apply("D2", 1, 2, 5), Is.EqualTo(7), "difference wraps");
            Assert.That(Apply("D3", 0, 6), Is.EqualTo(2), "double wraps");
            Assert.That(Apply("D3", 1, 7), Is.EqualTo(3), "halve rounds down");
            Assert.That(Apply("D3", 2, 2), Is.EqualTo(7), "mirror");
            Assert.That(Apply("D4", 0, 3, 8), Is.EqualTo(8), "larger");
            Assert.That(Apply("D4", 1, 3, 8), Is.EqualTo(3), "smaller");
        }

        [Test]
        public void ShapeRules()
        {
            int hollowCircle = Shapes.Make(0, false), solidCircle = Shapes.Make(0, true);
            int solidTriangle = Shapes.Make(1, true), hollowSquare = Shapes.Make(2, false), solidHexagon = Shapes.Make(3, true);
            Assert.That(Apply("S1", 0, hollowCircle), Is.EqualTo(solidCircle), "flip fill");
            Assert.That(Apply("S2", 0, solidHexagon), Is.EqualTo(solidCircle), "next form wraps and keeps fill");
            Assert.That(Apply("S2", 1, solidCircle), Is.EqualTo(solidHexagon), "previous form wraps");
            Assert.That(Apply("S3", 0, hollowSquare, solidTriangle), Is.EqualTo(Shapes.Make(2, true)), "form of top, fill of bottom");
            Assert.That(Apply("S3", 1, hollowSquare, solidTriangle), Is.EqualTo(Shapes.Make(1, false)), "form of bottom, fill of top");
            Assert.That(Apply("S4", 0, solidTriangle, hollowSquare), Is.EqualTo(hollowSquare), "more corners");
            Assert.That(Apply("S4", 1, solidTriangle, hollowSquare), Is.EqualTo(solidTriangle), "fewer corners");
            Assert.That(Apply("S4", 1, hollowCircle, solidCircle), Is.EqualTo(hollowCircle), "top on a tie");
        }

        [Test]
        public void WaveRules()
        {
            int sineHigh = Waves.Make(Waves.Sine, 2), sawLow = Waves.Make(Waves.Saw, 0), squareMid = Waves.Make(Waves.SquareWave, 1);
            Assert.That(Apply("W1", 0, sineHigh), Is.EqualTo(Waves.Make(Waves.Sine, 0)), "raise pitch wraps");
            Assert.That(Apply("W1", 1, sawLow), Is.EqualTo(Waves.Make(Waves.Saw, 2)), "lower pitch wraps");
            Assert.That(Apply("W2", 2, sineHigh), Is.EqualTo(Waves.Make(Waves.Saw, 2)), "make saw keeps pitch");
            Assert.That(Apply("W3", 0, sawLow, sineHigh), Is.EqualTo(Waves.Make(Waves.Saw, 2)), "form of top, pitch of bottom");
            Assert.That(Apply("W4", 0, sawLow, squareMid), Is.EqualTo(squareMid), "higher");
            Assert.That(Apply("W4", 1, sawLow, squareMid), Is.EqualTo(sawLow), "lower");
        }

        [Test]
        public void ValuesReadBackFromTheirDescriptions()
        {
            foreach (Medium m in new[] { Medium.Binary, Medium.Decimal, Medium.Shape, Medium.Wave })
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
        public void EncodingsRoundTrip()
        {
            for (int f = 0; f < Shapes.Forms; f++)
                foreach (bool solid in new[] { false, true })
                {
                    int v = Shapes.Make(f, solid);
                    Assert.That(Shapes.Form(v), Is.EqualTo(f));
                    Assert.That(Shapes.Solid(v), Is.EqualTo(solid));
                }
            for (int f = 0; f < Waves.Forms; f++)
                for (int p = 0; p < Waves.Pitches; p++)
                {
                    int v = Waves.Make(f, p);
                    Assert.That(Waves.Form(v), Is.EqualTo(f));
                    Assert.That(Waves.Pitch(v), Is.EqualTo(p));
                }
        }
    }
}
