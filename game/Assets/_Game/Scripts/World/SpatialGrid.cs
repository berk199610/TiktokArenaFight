using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// <summary>Uniform grid over the arena; nearby characters are found without everyone scanning everyone (doc §10.4).</summary>
    public class SpatialGrid
    {
        readonly float cell;
        readonly int n;
        readonly float half;
        readonly List<Fighter>[] cells;

        public SpatialGrid(float extent, float cellSize)
        {
            cell = cellSize;
            half = extent;
            n = Mathf.CeilToInt(extent * 2f / cellSize);
            cells = new List<Fighter>[n * n];
            for (int i = 0; i < cells.Length; i++) cells[i] = new List<Fighter>(8);
        }

        int Clamp(int v) => v < 0 ? 0 : v >= n ? n - 1 : v;
        int CellX(float x) => Clamp((int)((x + half) / cell));

        public void Rebuild(List<Fighter> fighters)
        {
            for (int i = 0; i < cells.Length; i++) cells[i].Clear();
            for (int i = 0; i < fighters.Count; i++)
            {
                var f = fighters[i];
                if (!f.active) continue;
                var p = f.motor.pos;
                cells[CellX(p.z) * n + CellX(p.x)].Add(f);
            }
        }

        public void Query(Vector3 p, float r, List<Fighter> results, Fighter exclude)
        {
            results.Clear();
            int x0 = CellX(p.x - r), x1 = CellX(p.x + r), z0 = CellX(p.z - r), z1 = CellX(p.z + r);
            float r2 = r * r;
            for (int z = z0; z <= z1; z++)
                for (int x = x0; x <= x1; x++)
                {
                    var list = cells[z * n + x];
                    for (int i = 0; i < list.Count; i++)
                    {
                        var f = list[i];
                        if (f == exclude || !f.active) continue;
                        float dx = f.motor.pos.x - p.x, dz = f.motor.pos.z - p.z;
                        if (dx * dx + dz * dz <= r2) results.Add(f);
                    }
                }
        }
    }
}
