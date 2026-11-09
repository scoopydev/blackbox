using System;
using System.Collections.Generic;
using System.Linq;

namespace Blackbox.Puzzles
{
    /// <summary>
    /// Gives every node a place on the board grid. Hand-placed positions from the
    /// puzzle file always win; anything without one is placed automatically, in
    /// columns by distance from the inputs and in rows ordered to keep wires short.
    /// </summary>
    public static class BoardLayout
    {
        public static void Apply(PuzzleDef p)
        {
            bool allPlaced = p.AllNodes.All(n => n.HasPosition);
            if (allPlaced) return;

            // Column: longest path from an input, following wires forwards only.
            var col = new Dictionary<NodeDef, int>();
            foreach (var n in p.Inputs) col[n] = 0;
            var incoming = p.Wires.GroupBy(w => w.To).ToDictionary(g => g.Key, g => g.Select(w => w.From).ToList());

            int ColumnOf(NodeDef n, HashSet<NodeDef> visiting)
            {
                if (col.TryGetValue(n, out int c)) return c;
                if (!visiting.Add(n)) return 0; // a loop: break it here
                int best = 0;
                if (incoming.TryGetValue(n, out var sources))
                    foreach (var s in sources) best = Math.Max(best, ColumnOf(s, visiting) + 1);
                visiting.Remove(n);
                col[n] = Math.Max(1, best);
                return col[n];
            }

            foreach (var b in p.Boxes) ColumnOf(b, new HashSet<NodeDef>());
            int lastBoxCol = p.Boxes.Count == 0 ? 0 : p.Boxes.Max(b => col[b]);
            foreach (var o in p.Outputs) col[o] = lastBoxCol + 1;

            // Respect hand-placed columns.
            foreach (var n in p.AllNodes) if (n.HasPosition) col[n] = (int)Math.Round(n.At.Col);

            // Rows: order each column by the average row of what feeds it.
            var row = new Dictionary<NodeDef, float>();
            int columns = col.Values.Max() + 1;
            int tallest = 0;
            var byColumn = new List<List<NodeDef>>();
            for (int c = 0; c < columns; c++)
            {
                var members = p.AllNodes.Where(n => col[n] == c).ToList();
                byColumn.Add(members);
                tallest = Math.Max(tallest, members.Count);
            }

            for (int c = 0; c < columns; c++)
            {
                var members = byColumn[c];
                if (c > 0)
                {
                    members = members.OrderBy(n =>
                    {
                        if (!incoming.TryGetValue(n, out var sources)) return 0f;
                        var placed = sources.Where(row.ContainsKey).ToList();
                        return placed.Count == 0 ? 0f : placed.Average(s => row[s]);
                    }).ToList();
                }
                float offset = (tallest - members.Count) * 0.5f;
                for (int i = 0; i < members.Count; i++) row[members[i]] = offset + i;
            }

            foreach (var n in p.AllNodes)
            {
                if (n.HasPosition) continue;
                n.At = new GridPoint(col[n], row[n]);
                n.HasPosition = true;
            }
        }

        /// <summary>The extent of the board in grid units.</summary>
        public static void Bounds(PuzzleDef p, out float minCol, out float maxCol, out float minRow, out float maxRow)
        {
            minCol = float.MaxValue; maxCol = float.MinValue; minRow = float.MaxValue; maxRow = float.MinValue;
            foreach (var n in p.AllNodes)
            {
                minCol = Math.Min(minCol, n.At.Col); maxCol = Math.Max(maxCol, n.At.Col);
                minRow = Math.Min(minRow, n.At.Row); maxRow = Math.Max(maxRow, n.At.Row);
            }
            foreach (var w in p.Wires)
            {
                if (!w.ViaRow.HasValue) continue;
                minRow = Math.Min(minRow, w.ViaRow.Value);
                maxRow = Math.Max(maxRow, w.ViaRow.Value);
            }
        }
    }
}
