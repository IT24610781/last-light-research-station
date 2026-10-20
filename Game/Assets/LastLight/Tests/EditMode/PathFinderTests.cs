using NUnit.Framework;
using UnityEngine;
using LastLight.Navigation;

public class PathFinderTests
{
    // Diamond graph: 0 -> (1 or 2) -> 3. Route via 1 is cheaper because 0-2 has a penalty.
    private static WaypointGraph BuildDiamond()
    {
        WaypointGraph g = new WaypointGraph();
        int n0 = g.AddNode(new Vector3(0, 0, 0));
        int n1 = g.AddNode(new Vector3(5, 0, 5));
        int n2 = g.AddNode(new Vector3(5, 0, -5));
        int n3 = g.AddNode(new Vector3(10, 0, 0));
        g.AddConnection(n0, n1);
        g.AddConnection(n1, n3);
        g.AddConnection(n0, n2, 4f);
        g.AddConnection(n2, n3);
        return g;
    }

    [Test]
    public void FindsKnownShortestRoute()
    {
        WaypointGraph g = BuildDiamond();
        PathResult r = PathFinder.FindPath(g, 0, 3);
        Assert.IsTrue(r.Success);
        CollectionAssert.AreEqual(new[] { 0, 1, 3 }, r.NodeIds);
    }

    [Test]
    public void StartEqualsGoal_ReturnsSingleNodeZeroCost()
    {
        WaypointGraph g = BuildDiamond();
        PathResult r = PathFinder.FindPath(g, 2, 2);
        Assert.IsTrue(r.Success);
        Assert.AreEqual(1, r.NodeIds.Count);
        Assert.AreEqual(0f, r.Cost);
    }

    [Test]
    public void UnreachableGoal_ReturnsExplicitFailure()
    {
        WaypointGraph g = BuildDiamond();
        int isolated = g.AddNode(new Vector3(50, 0, 50));
        PathResult r = PathFinder.FindPath(g, 0, isolated);
        Assert.IsFalse(r.Success);
        Assert.IsNotEmpty(r.FailureReason);
    }

    [Test]
    public void InvalidNodes_ReturnFailureNotException()
    {
        WaypointGraph g = BuildDiamond();
        Assert.IsFalse(PathFinder.FindPath(g, -1, 3).Success);
        Assert.IsFalse(PathFinder.FindPath(g, 0, 99).Success);
        Assert.IsFalse(PathFinder.FindPath(null, 0, 1).Success);
    }

    [Test]
    public void ClosedConnection_ForcesDetour_ThenFailure_ThenReopens()
    {
        WaypointGraph g = BuildDiamond();
        int revisionBefore = g.Revision;

        g.SetConnectionEnabled(1, 3, false);
        Assert.Greater(g.Revision, revisionBefore, "Revision must change so stale paths can be detected");
        PathResult detour = PathFinder.FindPath(g, 0, 3);
        Assert.IsTrue(detour.Success);
        CollectionAssert.AreEqual(new[] { 0, 2, 3 }, detour.NodeIds);

        g.SetConnectionEnabled(2, 3, false);
        Assert.IsFalse(PathFinder.FindPath(g, 0, 3).Success);

        g.SetConnectionEnabled(1, 3, true);
        PathResult reopened = PathFinder.FindPath(g, 0, 3);
        CollectionAssert.AreEqual(new[] { 0, 1, 3 }, reopened.NodeIds);
    }

    [Test]
    public void AStarCost_MatchesDijkstraBaseline_OnRandomGraph()
    {
        System.Random rng = new System.Random(42);
        WaypointGraph g = new WaypointGraph();
        int count = 40;
        for (int i = 0; i < count; i++)
            g.AddNode(new Vector3((float)rng.NextDouble() * 50f, 0, (float)rng.NextDouble() * 50f));

        for (int i = 0; i < count; i++)
        {
            for (int k = 0; k < 3; k++)
            {
                int other = rng.Next(count);
                g.AddConnection(i, other, (float)rng.NextDouble() * 3f);
            }
        }

        for (int trial = 0; trial < 200; trial++)
        {
            int s = rng.Next(count);
            int t = rng.Next(count);
            PathResult astar = PathFinder.FindPath(g, s, t, true);
            PathResult dijkstra = PathFinder.FindPath(g, s, t, false);

            Assert.AreEqual(dijkstra.Success, astar.Success, "Reachability must agree for " + s + "->" + t);
            if (dijkstra.Success)
                Assert.AreEqual(dijkstra.Cost, astar.Cost, 0.001f, "Costs must match for " + s + "->" + t);
        }
    }
}
