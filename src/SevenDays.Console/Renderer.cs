using System.Text;
using SevenDays.Core;

namespace SevenDays.ConsoleApp;

public static class Renderer
{
    private const int ViewW = 41, ViewH = 21;

    private static (char Glyph, ConsoleColor Color) TileGlyph(TileKind k) => k switch
    {
        TileKind.Grass => ('.', ConsoleColor.DarkGreen),
        TileKind.Tree => ('T', ConsoleColor.Green),
        TileKind.Rock => ('%', ConsoleColor.Gray),
        TileKind.Water => ('~', ConsoleColor.Blue),
        TileKind.Road => ('=', ConsoleColor.DarkGray),
        TileKind.Floor => (':', ConsoleColor.DarkGray),
        TileKind.RuinWall => ('#', ConsoleColor.DarkGray),
        TileKind.Crate => ('+', ConsoleColor.Yellow),
        TileKind.EmptyCrate => ('x', ConsoleColor.DarkYellow),
        TileKind.WoodWall => ('H', ConsoleColor.DarkYellow),
        TileKind.StoneWall => ('W', ConsoleColor.White),
        TileKind.WoodSpikes => ('*', ConsoleColor.Red),
        _ => ('?', ConsoleColor.Magenta),
    };

    public static int VisionRadius(Game g) =>
        g.Clock.IsBloodMoon ? 7 : g.Clock.IsNight ? 9 : 40;

    /// <summary>Builds the frame as rows of (char, color) cells.</summary>
    public static List<(char Ch, ConsoleColor Color)[]> BuildFrame(Game g)
    {
        var p = g.Player;
        int left = Math.Clamp(p.X - ViewW / 2, 0, Math.Max(0, g.World.Width - ViewW));
        int top = Math.Clamp(p.Y - ViewH / 2, 0, Math.Max(0, g.World.Height - ViewH));
        int vision = VisionRadius(g);

        var rows = new List<(char, ConsoleColor)[]>();
        for (int vy = 0; vy < ViewH; vy++)
        {
            var row = new (char, ConsoleColor)[ViewW];
            for (int vx = 0; vx < ViewW; vx++)
            {
                int x = left + vx, y = top + vy;
                if (!g.World.InBounds(x, y)) { row[vx] = (' ', ConsoleColor.Black); continue; }
                if (Math.Abs(x - p.X) + Math.Abs(y - p.Y) > vision) { row[vx] = (' ', ConsoleColor.Black); continue; }

                if (x == p.X && y == p.Y) { row[vx] = ('@', ConsoleColor.Cyan); continue; }
                var z = g.ZombieAt(x, y);
                if (z != null)
                    row[vx] = z.Kind == ZombieKind.Walker ? ('z', ConsoleColor.Green) : ('Z', ConsoleColor.Red);
                else
                    row[vx] = TileGlyph(g.World[x, y].Kind);
            }
            rows.Add(row);
        }
        return rows;
    }

    public static void Draw(Game g)
    {
        Console.SetCursorPosition(0, 0);
        Console.CursorVisible = false;
        foreach (var row in BuildFrame(g))
        {
            foreach (var (ch, color) in row)
            {
                Console.ForegroundColor = color;
                Console.Write(ch);
            }
            Console.ResetColor();
            Console.WriteLine();
        }
        Console.ResetColor();
        foreach (var line in StatusLines(g)) Console.WriteLine(line.PadRight(Math.Max(ViewW, 78)));
    }

    public static List<string> StatusLines(Game g)
    {
        var p = g.Player;
        var lines = new List<string>
        {
            $"{g.Clock}  {(g.Clock.IsBloodMoon ? "** BLOOD MOON **" : g.Clock.IsNight ? "Night" : "Day")}" +
            $"   Next blood moon: day {(g.Clock.Day + GameClock.BloodMoonInterval - 1) / GameClock.BloodMoonInterval * GameClock.BloodMoonInterval}",
            $"HP {p.Health,5:0}  Food {p.Food,5:0}  Water {p.Water,5:0}  Kills {p.Kills}  Zombies nearby: {g.Zombies.Count(z => Math.Abs(z.X - p.X) + Math.Abs(z.Y - p.Y) < 15)}",
            "Inventory: " + (p.Inventory.Entries.Any()
                ? string.Join(", ", p.Inventory.Entries.Select(e => $"{Items.Get(e.Key).Name} x{e.Value}"))
                : "(empty)"),
            "",
        };
        foreach (var m in g.Log.TakeLast(6)) lines.Add(m.Length > 76 ? m[..76] : m);
        while (lines.Count < 10) lines.Add("");
        lines.Add("WASD/arrows move+interact  C craft  B build  X dismantle  U use  . wait  Q quit");
        return lines;
    }

    public static string FrameAsText(Game g)
    {
        var sb = new StringBuilder();
        foreach (var row in BuildFrame(g)) sb.AppendLine(new string(row.Select(c => c.Ch).ToArray()));
        foreach (var l in StatusLines(g)) sb.AppendLine(l);
        return sb.ToString();
    }
}
