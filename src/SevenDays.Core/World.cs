namespace SevenDays.Core;

public sealed class World
{
    public int Width { get; }
    public int Height { get; }
    public (int X, int Y) Start { get; private set; }

    private readonly Tile[,] _tiles;

    public World(int width, int height)
    {
        Width = width; Height = height;
        _tiles = new Tile[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                _tiles[x, y] = new Tile(TileKind.Grass);
    }

    public ref Tile this[int x, int y] => ref _tiles[x, y];

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public bool IsWalkable(int x, int y) => InBounds(x, y) && TileInfo.IsWalkable(_tiles[x, y].Kind);

    public void Set(int x, int y, TileKind kind) => _tiles[x, y] = new Tile(kind);

    public static World Generate(int seed, int width = 80, int height = 50)
    {
        var world = new World(width, height);
        var rng = new Random(seed);

        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                double water = Noise(seed, x / 9.0, y / 9.0);
                double veg = Noise(seed + 1, x / 6.0, y / 6.0);
                TileKind kind = TileKind.Grass;
                if (water < 0.27) kind = TileKind.Water;
                else if (veg > 0.72 && rng.NextDouble() < 0.7) kind = TileKind.Rock;
                else if (veg < 0.45 && rng.NextDouble() < 0.5) kind = TileKind.Tree;
                world.Set(x, y, kind);
            }
        }

        // Roads cross the middle of the map (and bridge any water).
        int midX = width / 2, midY = height / 2;
        for (int x = 0; x < width; x++) world.Set(x, midY, TileKind.Road);
        for (int y = 0; y < height; y++) world.Set(midX, y, TileKind.Road);

        for (int attempt = 0; attempt < 40; attempt++)
            world.TryPlaceRuin(rng);

        world.Start = (midX + 2, midY + 2);
        for (int dx = -2; dx <= 2; dx++)
            for (int dy = -2; dy <= 2; dy++)
                world.Set(world.Start.X + dx, world.Start.Y + dy, TileKind.Grass);

        return world;
    }

    private int _ruins;

    private void TryPlaceRuin(Random rng)
    {
        if (_ruins >= 9) return;
        int w = rng.Next(5, 9), h = rng.Next(4, 7);
        int x0 = rng.Next(2, Width - w - 2), y0 = rng.Next(2, Height - h - 2);

        // Keep ruins clear of water, roads and the spawn area.
        for (int x = x0 - 1; x <= x0 + w; x++)
            for (int y = y0 - 1; y <= y0 + h; y++)
            {
                var k = _tiles[x, y].Kind;
                if (k is TileKind.Water or TileKind.Road or TileKind.RuinWall or TileKind.Floor) return;
                if (Math.Abs(x - (Width / 2 + 2)) < 6 && Math.Abs(y - (Height / 2 + 2)) < 6) return;
            }

        for (int x = x0; x < x0 + w; x++)
            for (int y = y0; y < y0 + h; y++)
            {
                bool edge = x == x0 || y == y0 || x == x0 + w - 1 || y == y0 + h - 1;
                Set(x, y, edge ? TileKind.RuinWall : TileKind.Floor);
            }

        // A doorway on a random side.
        switch (rng.Next(4))
        {
            case 0: Set(x0 + rng.Next(1, w - 1), y0, TileKind.Floor); break;
            case 1: Set(x0 + rng.Next(1, w - 1), y0 + h - 1, TileKind.Floor); break;
            case 2: Set(x0, y0 + rng.Next(1, h - 1), TileKind.Floor); break;
            default: Set(x0 + w - 1, y0 + rng.Next(1, h - 1), TileKind.Floor); break;
        }

        int crates = rng.Next(1, 4);
        for (int i = 0; i < crates; i++)
        {
            int cx = rng.Next(x0 + 1, x0 + w - 1), cy = rng.Next(y0 + 1, y0 + h - 1);
            if (_tiles[cx, cy].Kind == TileKind.Floor) Set(cx, cy, TileKind.Crate);
        }
        _ruins++;
    }

    // Bilinear value noise in [0,1).
    private static double Noise(int seed, double x, double y)
    {
        int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
        double tx = Smooth(x - x0), ty = Smooth(y - y0);
        double a = Hash(seed, x0, y0), b = Hash(seed, x0 + 1, y0);
        double c = Hash(seed, x0, y0 + 1), d = Hash(seed, x0 + 1, y0 + 1);
        return Lerp(Lerp(a, b, tx), Lerp(c, d, tx), ty);
    }

    private static double Smooth(double t) => t * t * (3 - 2 * t);
    private static double Lerp(double a, double b, double t) => a + (b - a) * t;

    private static double Hash(int seed, int x, int y)
    {
        unchecked
        {
            uint h = (uint)(seed * 374761393 + x * 668265263 + y * 2147483647);
            h = (h ^ (h >> 13)) * 1274126177;
            h ^= h >> 16;
            return h / (double)uint.MaxValue;
        }
    }
}
