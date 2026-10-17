namespace ValheimGen;

public static partial class SeedLocations
{
    internal sealed partial class BiomeMap
    {
        /// <summary>
        /// A biome's points listed the first time they are asked for. Its order depends on its own points
        /// only (the game's fill never crosses into another biome), so each is listed alone.
        /// </summary>
        private void List(int biome)
        {
            if (All[biome] != null)
            {
                return;
            }
            var all = new List<int>();
            var above = new List<int>();
            void Add(int x, int y)
            {
                all.Add(x | (y << 16));
                if ((_cell[x + y * Size] & Above) != 0)
                {
                    above.Add(x | (y << 16));
                }
            }
            bool Is(int x, int y) => (_cell[x + y * Size] & BiomeBits) == biome;
            if (biome is Ash or North or Sea)
            {
                // Ashlands, Deep North and the ocean: one sector each, in rows.
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        if (Is(x, y))
                        {
                            Add(x, y);
                        }
                    }
                }
            }
            else
            {
                // Each area flood filled from its first point in rows. That first point is not listed (the
                // game only lists the points its fill reaches).
                var stack = new Stack<int>(1024);
                for (int y = 0; y < Size; y++)
                {
                    for (int x = 0; x < Size; x++)
                    {
                        if (!Is(x, y) || (_cell[x + y * Size] & Visited) != 0)
                        {
                            continue;
                        }
                        _cell[x + y * Size] |= Visited;
                        stack.Push(x | (y << 16));
                        while (stack.Count > 0)
                        {
                            int c = stack.Pop();
                            int cx = c & 0xFFFF, cy = c >> 16;
                            Fill(cx + 1, cy);
                            Fill(cx - 1, cy);
                            Fill(cx, cy + 1);
                            Fill(cx, cy - 1);
                        }
                    }
                }
                void Fill(int fx, int fy)
                {
                    if (fx >= 0 && fy >= 0 && fx < Size && fy < Size && Is(fx, fy) && (_cell[fx + fy * Size] & Visited) == 0)
                    {
                        _cell[fx + fy * Size] |= Visited;
                        Add(fx, fy);
                        stack.Push(fx | (fy << 16));
                    }
                }
            }
            All[biome] = [.. all];
            AboveSea[biome] = [.. above];
        }
    }
}
