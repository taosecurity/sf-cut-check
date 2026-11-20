// The walk: from every root, follow links (A links to B: if the game uses A, it can use B). Strong roots go first,
// then weak, then dev, so each record keeps the strongest way it can be reached. Each reached record remembers the
// record it was reached from, so `why` can print the whole path.
namespace SfCutCheck;

public enum Verdict : byte
{
    Reached,        // reached from a strong root
    Maybe,          // reached only from a weak root (recipe, research, perk)
    DevOnly,        // reached only from developer worlds or cells
    Orphaned,       // something links to it, but nothing the game reaches
    Unreferenced,   // nothing links to it at all
    CannotCheck,    // Mutagen couldn't read its links
}

public sealed class Walk
{
    public readonly RefIndex Index;
    readonly int[] outStart, outTo, inStart, inFrom;
    readonly byte[] outKind, inKind;
    public readonly Tier[] TierOf;
    public readonly int[] Pred;            // the record each one was reached from; -1 for roots and unreached
    public readonly RootRule?[] RootOf;    // the rule that made a record a root (roots only)
    readonly int[] containerOf;            // the record directly holding each one ("contains"); -1 if none

    public Walk(RefIndex ix)
    {
        Index = ix;
        var n = ix.NodeCount;
        (outStart, outTo, outKind) = Csr(n, ix.From, ix.To, ix.Kind);
        (inStart, inFrom, inKind) = Csr(n, ix.To, ix.From, ix.Kind);
        TierOf = Enumerable.Repeat(Tier.None, n).ToArray();
        Pred = Enumerable.Repeat(-1, n).ToArray();
        RootOf = new RootRule?[n];
        containerOf = Enumerable.Repeat(-1, n).ToArray();
        for (var i = 0; i < ix.From.Count; i++)
            if (ix.Kind[i] == LinkKind.Contains && containerOf[ix.To[i]] == -1) containerOf[ix.To[i]] = ix.From[i];
        var checkStops = new bool[n];   // records a condition check alone doesn't reach (Rules.CheckDoesntReach)
        for (var i = 0; i < n; i++) checkStops[i] = Rules.CheckDoesntReach.Contains(ix.TypeOf(i));
        // Strong pass: a check on content (checkStops) isn't followed, but remembered. The weak pass starts from those
        // remembered records (rule "checked-only") as well as the weak roots, and follows every link, checks included.
        var checkedFromStrong = new List<(int Target, int From)>();
        foreach (var tier in new[] { Tier.Strong, Tier.Weak, Tier.Dev })
        {
            var queue = new Queue<int>();
            foreach (var (id, rule) in ix.Roots)
                if (rule.Tier == tier && TierOf[id] == Tier.None)
                {
                    TierOf[id] = tier;
                    RootOf[id] = rule;
                    queue.Enqueue(id);
                }
            if (tier == Tier.Weak)
                foreach (var (target, from) in checkedFromStrong)
                    if (TierOf[target] == Tier.None)
                    {
                        TierOf[target] = Tier.Weak;
                        RootOf[target] = Rules.CheckedOnly;
                        Pred[target] = from;
                        queue.Enqueue(target);
                    }
            while (queue.Count > 0)
            {
                var a = queue.Dequeue();
                for (var e = outStart[a]; e < outStart[a + 1]; e++)
                {
                    var b = outTo[e];
                    if (TierOf[b] != Tier.None) continue;
                    if (tier == Tier.Strong && outKind[e] == (byte)LinkKind.Checks && checkStops[b])
                    {
                        checkedFromStrong.Add((b, a));
                        continue;
                    }
                    TierOf[b] = tier;
                    Pred[b] = a;
                    queue.Enqueue(b);
                }
            }
        }
    }

    static (int[] start, int[] to, byte[] kind) Csr(int n, List<int> from, List<int> to, List<LinkKind> kind)
    {
        var start = new int[n + 1];
        foreach (var f in from) start[f + 1]++;
        for (var i = 0; i < n; i++) start[i + 1] += start[i];
        var fill = (int[])start.Clone();
        var t = new int[from.Count];
        var k = new byte[from.Count];
        for (var i = 0; i < from.Count; i++)
        {
            var slot = fill[from[i]]++;
            t[slot] = to[i];
            k[slot] = (byte)kind[i];
        }
        // A record overridden by the DLC or a creation adds its links once per version: drop the repeats (same
        // target, same kind) so counts and evidence name each record once (code review finding 5, 2026-11-11).
        var outStart = new int[n + 1];
        var w = 0;
        var buf = new List<long>();
        for (var a = 0; a < n; a++)
        {
            outStart[a] = w;
            int s = start[a], e = start[a + 1];
            if (e - s == 1) { t[w] = t[s]; k[w] = k[s]; w++; continue; }
            buf.Clear();
            for (var i = s; i < e; i++) buf.Add(((long)t[i] << 8) | k[i]);
            buf.Sort();
            long prev = -1;
            foreach (var v in buf)
            {
                if (v == prev) continue;
                prev = v;
                t[w] = (int)(v >> 8);
                k[w] = (byte)(v & 0xFF);
                w++;
            }
        }
        outStart[n] = w;
        Array.Resize(ref t, w);
        Array.Resize(ref k, w);
        return (outStart, t, k);
    }

    public Verdict VerdictOf(int id)
    {
        if (Index.FailedIds.Contains(id) && TierOf[id] != Tier.Strong) return Verdict.CannotCheck;
        switch (TierOf[id])
        {
            case Tier.Strong: return Verdict.Reached;
            case Tier.Weak: return Verdict.Maybe;
            case Tier.Dev: return Verdict.DevOnly;
        }
        // An interior cell's own placed references ("in" links) and the records inside it (its navmeshes name their
        // cell) don't count: a cell nothing outside leads to is Unreferenced.
        foreach (var (from, _) in InboundFromOutside(id))
            if (Index.Defined.Contains(from)) return Verdict.Orphaned;
        return Verdict.Unreferenced;
    }

    /// <summary>The records that link to this one from outside it: without "in" links and without links from the
    /// records it holds directly, for cells (0.1.9, 2026-11-20: a cell's navmeshes link back to their cell, which made
    /// cells nothing leads to Orphaned). Used for Orphaned and the reports' linked-from list.</summary>
    public IEnumerable<(int From, LinkKind Kind)> InboundFromOutside(int id)
    {
        // Cells only: a quest's own topics linking back to it stay as they were (Orphaned), as before step 3b.
        var cell = Index.TypeOf(id) == "Cell";
        for (var e = inStart[id]; e < inStart[id + 1]; e++)
            if (inKind[e] != (byte)LinkKind.In && !(cell && containerOf[inFrom[e]] == id)) yield return (inFrom[e], (LinkKind)inKind[e]);
    }

    /// <summary>The records that link to this one, with the kind of link.</summary>
    public IEnumerable<(int From, LinkKind Kind)> Inbound(int id)
    {
        for (var e = inStart[id]; e < inStart[id + 1]; e++) yield return (inFrom[e], (LinkKind)inKind[e]);
    }

    public int OutCount(int id) => outStart[id + 1] - outStart[id];

    /// <summary>The records this one links to, with the kind of link.</summary>
    public IEnumerable<(int To, LinkKind Kind)> Outbound(int id)
    {
        for (var e = outStart[id]; e < outStart[id + 1]; e++) yield return (outTo[e], (LinkKind)outKind[e]);
    }

    /// <summary>Links after repeats from overrides are dropped.</summary>
    public int LinkCount => outTo.Length;

    /// <summary>The rule that decides this record's verdict: the nearest rule on its path back to the root (a
    /// checked-only record keeps its own rule even though its path continues to the strong record that checks it).</summary>
    public RootRule? ReasonOf(int id)
    {
        if (TierOf[id] == Tier.None) return null;
        for (var cur = id; cur != -1; cur = Pred[cur])
            if (RootOf[cur] != null) return RootOf[cur];
        return null;
    }

    /// <summary>For a record whose rule is checked-only: the record on its path that a running record only checks
    /// (the record itself, or one it's reached through) and the record that checks it. Null otherwise.
    /// 0.1.9, 2026-11-20: the report named the record's own predecessor as the checker, which is wrong for a
    /// record reached through a checked one (a spell's effects were "checked by" the spell that lists them).</summary>
    public (int Checked, int Checker)? CheckedBy(int id)
    {
        if (TierOf[id] == Tier.None) return null;
        for (var cur = id; cur != -1; cur = Pred[cur])
            if (RootOf[cur] != null)
                return RootOf[cur] == Rules.CheckedOnly && Pred[cur] >= 0 ? (cur, Pred[cur]) : null;
        return null;
    }

    /// <summary>The short reason shown in report.csv, report.json, report.html, `why` and `find`. One wording for all.
    /// "checked-only: checked by X" when a running record checks this one; "via checked-only: Y, checked by X" when
    /// it's reached through Y, which X checks; "via rule: root" for other rules.</summary>
    public string ReasonText(int id)
    {
        var ix = Index;
        if (CheckedBy(id) is var (chk, by))
            return chk == id ? $"{Rules.CheckedOnly.Name}: checked by {ix.Describe(by)}"
                : $"via {Rules.CheckedOnly.Name}: {ix.Describe(chk)}, checked by {ix.Describe(by)}";
        var p = PathTo(id);
        // Short, so the report's Why column stays readable; the rule's full meaning is in the legend (and the
        // page's hover text). User's screenshot 2026-11-11: the long form squeezed the table.
        if (p.Count > 0) return $"via {ReasonOf(id)!.Name}: {ix.Describe(p[0])}";
        if (VerdictOf(id) == Verdict.CannotCheck) return "Mutagen couldn't read its links";
        return VerdictOf(id) == Verdict.Orphaned ? "only records the game never reaches link to it" : "nothing links to it";
    }

    /// <summary>The path from a root to this record (root first). Empty if it isn't reached.</summary>
    public List<int> PathTo(int id)
    {
        var path = new List<int>();
        if (TierOf[id] == Tier.None) return path;
        for (var cur = id; cur != -1; cur = Pred[cur]) path.Add(cur);
        path.Reverse();
        return path;
    }

    /// <summary>The link kind from a to b (the first one found).</summary>
    public LinkKind KindBetween(int a, int b)
    {
        for (var e = outStart[a]; e < outStart[a + 1]; e++)
            if (outTo[e] == b) return (LinkKind)outKind[e];
        return LinkKind.Ref;
    }
}
