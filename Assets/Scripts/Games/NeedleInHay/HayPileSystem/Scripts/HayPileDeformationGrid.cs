using System.Collections.Generic;
using UnityEngine;
using HayPileSystem;

public class HayPileDeformationGrid
{
    // ============================================================
    // CELL
    // ============================================================

    private class Cell
    {
        public readonly List<HayStraw> straws =
            new List<HayStraw>(32);
    }

    // ============================================================
    // CONFIG
    // ============================================================

    private int gridSize = 32;

    private float radius;
    private float diameter;

    private float worldMin;

    private Transform pileRoot;

    // ============================================================
    // GRID
    // ============================================================

    private Cell[] cells;

    // ============================================================
    // STRAW -> CELL DATA
    // ============================================================

    private readonly Dictionary<HayStraw, GridRecord> records =
        new Dictionary<HayStraw, GridRecord>(4096);

    private class GridRecord
    {
        public HayStraw straw;

        public int minX;
        public int maxX;

        public int minZ;
        public int maxZ;
    }

    // ============================================================
    // QUERY CACHE
    // ============================================================

    private readonly List<HayStraw> queryResults =
        new List<HayStraw>(128);

    private readonly HashSet<HayStraw> querySet =
        new HashSet<HayStraw>();

    // ============================================================
    // INITIALIZATION
    // ============================================================

    public void Initialize(
        float pileRadius,
        int size)
    {
        Initialize(
            null,
            pileRadius,
            size);
    }

    public void Initialize(
        Transform root,
        float pileRadius,
        int size)
    {
        pileRoot = root;

        radius = Mathf.Max(
            0.01f,
            pileRadius);

        diameter = radius * 2f;

        gridSize = Mathf.Clamp(
            size,
            4,
            128);

        worldMin = -radius;

        int cellCount =
            gridSize * gridSize;

        cells = new Cell[cellCount];

        for (int i = 0; i < cellCount; i++)
        {
            cells[i] = new Cell();
        }

        records.Clear();

        queryResults.Clear();
        querySet.Clear();
    }

    // ============================================================
    // BUILD
    // ============================================================

    public void Build(
        IReadOnlyList<HayStraw> visibleStraws,
        IReadOnlyList<HayStraw> reserveStraws)
    {
        ClearCells();

        records.Clear();

        if (visibleStraws != null)
        {
            for (int i = 0; i < visibleStraws.Count; i++)
            {
                Add(visibleStraws[i]);
            }
        }

        if (reserveStraws != null)
        {
            for (int i = 0; i < reserveStraws.Count; i++)
            {
                Add(reserveStraws[i]);
            }
        }
    }

    public void Clear()
    {
        if (cells != null)
        {
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i].straws.Clear();
            }
        }

        records.Clear();

        queryResults.Clear();
        querySet.Clear();
    }

    private void ClearCells()
    {
        if (cells == null)
            return;

        for (int i = 0; i < cells.Length; i++)
        {
            cells[i].straws.Clear();
        }
    }

    // ============================================================
    // ADD
    // ============================================================

    public void Add(HayStraw straw)
    {
        if (straw == null)
            return;

        if (records.ContainsKey(straw))
            return;

        if (!straw.TryGetPileLocalBounds(
                pileRoot,
                out Bounds bounds))
        {
            return;
        }

        GridRecord record =
            new GridRecord();

        record.straw = straw;

        GetGridRange(
            bounds,
            out record.minX,
            out record.maxX,
            out record.minZ,
            out record.maxZ);

        records.Add(
            straw,
            record);

        AddRecordToCells(record);
    }

    // ============================================================
    // REMOVE
    // ============================================================

    public void Remove(HayStraw straw)
    {
        if (straw == null)
            return;

        GridRecord record;

        if (!records.TryGetValue(
                straw,
                out record))
        {
            return;
        }

        RemoveRecordFromCells(record);

        records.Remove(straw);
    }

    // ============================================================
    // UPDATE
    // ============================================================

    public void Update(HayStraw straw)
    {
        if (straw == null)
            return;

        GridRecord record;

        if (!records.TryGetValue(
                straw,
                out record))
        {
            Add(straw);
            return;
        }

        if (!straw.TryGetPileLocalBounds(
                pileRoot,
                out Bounds bounds))
        {
            return;
        }

        int minX;
        int maxX;
        int minZ;
        int maxZ;

        GetGridRange(
            bounds,
            out minX,
            out maxX,
            out minZ,
            out maxZ);

        if (record.minX == minX &&
            record.maxX == maxX &&
            record.minZ == minZ &&
            record.maxZ == maxZ)
        {
            return;
        }

        RemoveRecordFromCells(record);

        record.minX = minX;
        record.maxX = maxX;
        record.minZ = minZ;
        record.maxZ = maxZ;

        AddRecordToCells(record);
    }

    // ============================================================
    // QUERY
    // ============================================================

    public IReadOnlyList<HayStraw> Query(
        Vector3 localPosition,
        float queryRadius)
    {
        queryResults.Clear();
        querySet.Clear();

        if (cells == null)
            return queryResults;

        queryRadius = Mathf.Max(
            0f,
            queryRadius);

        Bounds queryBounds =
            new Bounds(
                localPosition,
                new Vector3(
                    queryRadius * 2f,
                    queryRadius * 2f,
                    queryRadius * 2f));

        int minX;
        int maxX;
        int minZ;
        int maxZ;

        GetGridRange(
            queryBounds,
            out minX,
            out maxX,
            out minZ,
            out maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            int row =
                z * gridSize;

            for (int x = minX; x <= maxX; x++)
            {
                List<HayStraw> list =
                    cells[row + x].straws;

                for (int i = 0; i < list.Count; i++)
                {
                    HayStraw straw =
                        list[i];

                    if (straw == null)
                        continue;

                    if (!querySet.Add(straw))
                        continue;

                    queryResults.Add(straw);
                }
            }
        }

        return queryResults;
    }

    // ============================================================
    // QUERY NON-ALLOCATING
    // ============================================================

    public void Query(
        Vector3 localPosition,
        float queryRadius,
        List<HayStraw> results)
    {
        if (results == null)
            return;

        results.Clear();
        querySet.Clear();

        if (cells == null)
            return;

        queryRadius = Mathf.Max(
            0f,
            queryRadius);

        Bounds queryBounds =
            new Bounds(
                localPosition,
                new Vector3(
                    queryRadius * 2f,
                    queryRadius * 2f,
                    queryRadius * 2f));

        int minX;
        int maxX;
        int minZ;
        int maxZ;

        GetGridRange(
            queryBounds,
            out minX,
            out maxX,
            out minZ,
            out maxZ);

        for (int z = minZ; z <= maxZ; z++)
        {
            int row =
                z * gridSize;

            for (int x = minX; x <= maxX; x++)
            {
                List<HayStraw> list =
                    cells[row + x].straws;

                for (int i = 0; i < list.Count; i++)
                {
                    HayStraw straw =
                        list[i];

                    if (straw == null)
                        continue;

                    if (!querySet.Add(straw))
                        continue;

                    results.Add(straw);
                }
            }
        }
    }

    // ============================================================
    // CELL MANAGEMENT
    // ============================================================

    private void AddRecordToCells(
        GridRecord record)
    {
        if (cells == null)
            return;

        for (int z = record.minZ;
             z <= record.maxZ;
             z++)
        {
            int row =
                z * gridSize;

            for (int x = record.minX;
                 x <= record.maxX;
                 x++)
            {
                cells[row + x]
                    .straws
                    .Add(record.straw);
            }
        }
    }

    private void RemoveRecordFromCells(
        GridRecord record)
    {
        if (cells == null)
            return;

        for (int z = record.minZ;
             z <= record.maxZ;
             z++)
        {
            int row =
                z * gridSize;

            for (int x = record.minX;
                 x <= record.maxX;
                 x++)
            {
                List<HayStraw> list =
                    cells[row + x].straws;

                list.Remove(record.straw);
            }
        }
    }

    // ============================================================
    // GRID COORDINATES
    // ============================================================

    private void GetGridRange(
        Bounds bounds,
        out int minX,
        out int maxX,
        out int minZ,
        out int maxZ)
    {
        WorldToGrid(
            bounds.min.x,
            bounds.min.z,
            out minX,
            out minZ);

        WorldToGrid(
            bounds.max.x,
            bounds.max.z,
            out maxX,
            out maxZ);
    }

    private void WorldToGrid(
        float x,
        float z,
        out int gx,
        out int gz)
    {
        if (diameter <= 0.0001f)
        {
            gx = 0;
            gz = 0;
            return;
        }

        float u =
            (x - worldMin) /
            diameter;

        float v =
            (z - worldMin) /
            diameter;

        gx = Mathf.Clamp(
            Mathf.FloorToInt(
                u * gridSize),
            0,
            gridSize - 1);

        gz = Mathf.Clamp(
            Mathf.FloorToInt(
                v * gridSize),
            0,
            gridSize - 1);
    }

    // ============================================================
    // INFO
    // ============================================================

    public int Count
    {
        get
        {
            return records.Count;
        }
    }

    public float Radius
    {
        get
        {
            return radius;
        }
    }
}