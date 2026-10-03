using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using SevenDays.Core;

namespace SevenDays.GodotApp;

/// <summary>Godot front-end for SevenDays.Core: draws the world, handles input and menus.</summary>
public partial class Main : Node2D
{
    private const int TileSize = 32;

    private enum Mode { Play, Menu, PickDirection }

    private Game _game = null!;
    private Mode _mode = Mode.Play;
    private string _menuTitle = "";
    private List<(string Label, Action Run)> _menu = new();
    private string _dirPrompt = "";
    private Action<Direction>? _onDirection;
    private Font _font = null!;

    public override void _Ready()
    {
        _font = ThemeDB.FallbackFont;
        StartGame();
    }

    private void StartGame()
    {
        int seed = System.Environment.TickCount;
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--seed=") && int.TryParse(arg[7..], out var s)) seed = s;
        _game = new Game(seed);
        _mode = Mode.Play;
        QueueRedraw();
    }

    // ------------------------------------------------------------------ Input

    private static Direction? DirectionFor(Key key) => key switch
    {
        Key.W or Key.Up => Direction.Up,
        Key.S or Key.Down => Direction.Down,
        Key.A or Key.Left => Direction.Left,
        Key.D or Key.Right => Direction.Right,
        _ => null,
    };

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true } e) return;

        if (_game.IsOver)
        {
            if (e.Keycode == Key.R && !e.Echo) StartGame();
            return;
        }

        switch (_mode)
        {
            case Mode.Play: HandlePlayKey(e); break;
            case Mode.Menu: HandleMenuKey(e); break;
            case Mode.PickDirection: HandleDirectionKey(e); break;
        }
        QueueRedraw();
    }

    private void HandlePlayKey(InputEventKey e)
    {
        if (DirectionFor(e.Keycode) is { } dir) { _game.Move(dir); return; }
        if (e.Echo) return;

        switch (e.Keycode)
        {
            case Key.Period or Key.Space: _game.Wait(); break;
            case Key.C: OpenCraftMenu(); break;
            case Key.B: OpenBuildMenu(); break;
            case Key.U: OpenUseMenu(); break;
            case Key.X: AskDirection("Dismantle which direction?", d => _game.Dismantle(d)); break;
            case Key.Escape: GetTree().Quit(); break;
        }
    }

    private void HandleMenuKey(InputEventKey e)
    {
        if (e.Echo) return;
        int index = (int)(e.Keycode - Key.Key1);
        if (index >= 0 && index < _menu.Count)
        {
            var run = _menu[index].Run;
            _mode = Mode.Play;
            run();
        }
        else if (e.Keycode == Key.Escape) _mode = Mode.Play;
    }

    private void HandleDirectionKey(InputEventKey e)
    {
        if (e.Echo) return;
        var action = _onDirection;
        _mode = Mode.Play;
        if (DirectionFor(e.Keycode) is { } dir) action?.Invoke(dir);
    }

    private void AskDirection(string prompt, Action<Direction> onPicked)
    {
        _dirPrompt = prompt;
        _onDirection = onPicked;
        _mode = Mode.PickDirection;
    }

    private void OpenMenu(string title, List<(string, Action)> items)
    {
        _menuTitle = title;
        _menu = items;
        _mode = Mode.Menu;
    }

    private void OpenCraftMenu() => OpenMenu("Craft",
        Recipes.All.Select<Recipe, (string, Action)>(r => (
            $"{Items.Get(r.Result).Name}  [{r.CostText}]{(r.CanCraft(_game.Player.Inventory) ? "" : "  (missing)")}",
            () => _game.Craft(r))).ToList());

    private void OpenBuildMenu()
    {
        var placeable = _game.Player.Inventory.Entries.Where(e => Items.Get(e.Key).Places != null).ToList();
        if (placeable.Count == 0) { _game.Say("You have nothing to build with. Craft walls first."); return; }
        OpenMenu("Build", placeable.Select<KeyValuePair<ItemId, int>, (string, Action)>(e => (
            $"{Items.Get(e.Key).Name} x{e.Value}",
            () => AskDirection("Build in which direction?", d => _game.Place(e.Key, d)))).ToList());
    }

    private void OpenUseMenu()
    {
        var usable = _game.Player.Inventory.Entries.Where(e => Items.Get(e.Key).IsConsumable).ToList();
        if (usable.Count == 0) { _game.Say("You have nothing to eat, drink or heal with."); return; }
        OpenMenu("Use", usable.Select<KeyValuePair<ItemId, int>, (string, Action)>(e => (
            $"{Items.Get(e.Key).Name} x{e.Value}", () => _game.Use(e.Key))).ToList());
    }

    // ------------------------------------------------------------------ Drawing

    private static int VisionRadius(GameClock c) => c.IsBloodMoon ? 7 : c.Hour >= 20 || c.Hour < 5 ? 9 : 60;

    public override void _Draw()
    {
        var size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), Colors.Black);

        var p = _game.Player;
        var origin = size / 2 - new Vector2((p.X + 0.5f) * TileSize, (p.Y + 0.5f) * TileSize);
        int x0 = Math.Max(0, (int)Math.Floor(-origin.X / TileSize));
        int y0 = Math.Max(0, (int)Math.Floor(-origin.Y / TileSize));
        int x1 = Math.Min(_game.World.Width - 1, (int)Math.Ceiling((size.X - origin.X) / TileSize));
        int y1 = Math.Min(_game.World.Height - 1, (int)Math.Ceiling((size.Y - origin.Y) / TileSize));
        int vision = VisionRadius(_game.Clock);

        for (int x = x0; x <= x1; x++)
        {
            for (int y = y0; y <= y1; y++)
            {
                int dist = Math.Abs(x - p.X) + Math.Abs(y - p.Y);
                if (dist > vision) continue;
                DrawTile(_game.World[x, y].Kind, origin + new Vector2(x, y) * TileSize);
            }
        }

        foreach (var z in _game.Zombies)
        {
            if (Math.Abs(z.X - p.X) + Math.Abs(z.Y - p.Y) > vision) continue;
            DrawZombie(z, origin + new Vector2(z.X, z.Y) * TileSize);
        }

        var pc = origin + new Vector2(p.X + 0.5f, p.Y + 0.5f) * TileSize;
        DrawCircle(pc, TileSize * 0.38f, new Color("38d9f5"));
        DrawArc(pc, TileSize * 0.38f, 0, Mathf.Tau, 24, Colors.White, 2);

        DrawLighting(size);
        DrawHud(size);
        if (_mode == Mode.Menu) DrawMenu(size);
        if (_mode == Mode.PickDirection) DrawBanner(size, $"{_dirPrompt}  (WASD / arrows, other = cancel)");
        if (_game.IsOver) DrawGameOver(size);
    }

    private void DrawTile(TileKind kind, Vector2 pos)
    {
        var rect = new Rect2(pos, new Vector2(TileSize, TileSize));
        var c = pos + new Vector2(TileSize, TileSize) / 2;
        Color ground = kind switch
        {
            TileKind.Road => new Color("4a4a4f"),
            TileKind.Floor or TileKind.Crate or TileKind.EmptyCrate => new Color("6b6256"),
            TileKind.Water => new Color("2457b5"),
            _ => new Color("2f6b33"),
        };
        DrawRect(rect, ground);
        DrawRect(rect, new Color(0, 0, 0, 0.12f), false);

        switch (kind)
        {
            case TileKind.Tree:
                DrawRect(new Rect2(c + new Vector2(-3, 2), new Vector2(6, 12)), new Color("5b3a1a"));
                DrawCircle(c + new Vector2(0, -4), 12, new Color("1f8f2e"));
                break;
            case TileKind.Rock:
                DrawColoredPolygon(new[] { c + new Vector2(-12, 10), c + new Vector2(-8, -8), c + new Vector2(6, -11), c + new Vector2(13, 8) }, new Color("8a8f98"));
                break;
            case TileKind.Water:
                DrawLine(c + new Vector2(-10, -3), c + new Vector2(10, -3), new Color("5c8cf0"), 2);
                break;
            case TileKind.Road:
                DrawLine(c + new Vector2(-12, 0), c + new Vector2(-4, 0), new Color("c9b94a"), 2);
                break;
            case TileKind.RuinWall:
                DrawRect(rect.Grow(-1), new Color("3a3a40"));
                DrawLine(pos + new Vector2(0, 16), pos + new Vector2(TileSize, 16), new Color("27272b"), 1);
                break;
            case TileKind.Crate:
                DrawRect(new Rect2(c - new Vector2(10, 10), new Vector2(20, 20)), new Color("c28b2c"));
                DrawRect(new Rect2(c - new Vector2(10, 10), new Vector2(20, 20)), new Color("6f4a10"), false, 2);
                break;
            case TileKind.EmptyCrate:
                DrawRect(new Rect2(c - new Vector2(10, 10), new Vector2(20, 20)), new Color("5d4a22"), false, 2);
                break;
            case TileKind.WoodWall:
                DrawRect(rect.Grow(-2), new Color("a06a2c"));
                DrawLine(pos + new Vector2(2, 10), pos + new Vector2(TileSize - 2, 10), new Color("6f4a1c"), 2);
                DrawLine(pos + new Vector2(2, 22), pos + new Vector2(TileSize - 2, 22), new Color("6f4a1c"), 2);
                break;
            case TileKind.StoneWall:
                DrawRect(rect.Grow(-1), new Color("b8bcc4"));
                DrawRect(rect.Grow(-1), new Color("6b6f78"), false, 2);
                break;
            case TileKind.WoodSpikes:
                for (int i = 0; i < 3; i++)
                {
                    float sx = pos.X + 6 + i * 10;
                    DrawColoredPolygon(new[] { new Vector2(sx - 4, pos.Y + 26), new Vector2(sx, pos.Y + 6), new Vector2(sx + 4, pos.Y + 26) }, new Color("d9b36a"));
                }
                break;
        }
    }

    private void DrawZombie(Zombie z, Vector2 pos)
    {
        var c = pos + new Vector2(TileSize, TileSize) / 2;
        var body = z.Kind == ZombieKind.Walker ? new Color("6dbb4a") : new Color("d93a3a");
        DrawCircle(c, TileSize * 0.36f, body);
        DrawCircle(c + new Vector2(-4, -3), 2.5f, Colors.Black);
        DrawCircle(c + new Vector2(4, -3), 2.5f, Colors.Black);
        // Health bar once damaged.
        int max = z.Kind == ZombieKind.Walker ? 30 : 45;
        if (z.Hp < max)
        {
            DrawRect(new Rect2(pos + new Vector2(3, 0), new Vector2(TileSize - 6, 3)), Colors.Black);
            DrawRect(new Rect2(pos + new Vector2(3, 0), new Vector2((TileSize - 6) * Mathf.Max(0, z.Hp) / max, 3)), Colors.LimeGreen);
        }
    }

    private void DrawLighting(Vector2 size)
    {
        var clock = _game.Clock;
        float alpha = clock.Hour switch
        {
            >= 21 or < 4 => 0.55f,
            20 or 4 => 0.35f,
            19 or 5 => 0.15f,
            _ => 0f,
        };
        var tint = clock.IsBloodMoon ? new Color(0.35f, 0f, 0f, 0.5f) : new Color(0.02f, 0.03f, 0.15f, alpha);
        if (tint.A > 0) DrawRect(new Rect2(Vector2.Zero, size), tint);
    }

    private void Text(string s, Vector2 pos, int fontSize = 16, Color? color = null) =>
        DrawString(_font, pos, s, HorizontalAlignment.Left, -1, fontSize, color ?? Colors.White);

    private void Bar(Vector2 pos, string label, double value, Color fill)
    {
        DrawRect(new Rect2(pos, new Vector2(180, 16)), new Color(0, 0, 0, 0.6f));
        DrawRect(new Rect2(pos, new Vector2((float)(180 * Math.Clamp(value / Player.MaxStat, 0, 1)), 16)), fill);
        Text($"{label} {value:0}", pos + new Vector2(6, 13), 13);
    }

    private void DrawHud(Vector2 size)
    {
        var p = _game.Player;
        var clock = _game.Clock;

        DrawRect(new Rect2(8, 8, 196, 78), new Color(0, 0, 0, 0.45f));
        Bar(new Vector2(14, 14), "HP", p.Health, new Color("d93a3a"));
        Bar(new Vector2(14, 36), "Food", p.Food, new Color("d9a33a"));
        Bar(new Vector2(14, 58), "Water", p.Water, new Color("3a8fd9"));

        int nextMoon = (clock.Day + GameClock.BloodMoonInterval - 1) / GameClock.BloodMoonInterval * GameClock.BloodMoonInterval;
        string phase = clock.IsBloodMoon ? "BLOOD MOON" : clock.IsNight ? "Night" : "Day";
        DrawRect(new Rect2(size.X - 268, 8, 260, 62), new Color(0, 0, 0, 0.45f));
        Text($"{clock}  -  {phase}", new Vector2(size.X - 258, 30), 16, clock.IsBloodMoon ? Colors.OrangeRed : Colors.White);
        Text($"Next blood moon: day {nextMoon}   Kills: {p.Kills}", new Vector2(size.X - 258, 54), 13);

        // Inventory
        var items = p.Inventory.Entries.ToList();
        float invH = 28 + Math.Max(1, items.Count) * 20;
        DrawRect(new Rect2(size.X - 208, 80, 200, invH), new Color(0, 0, 0, 0.45f));
        Text("Inventory", new Vector2(size.X - 198, 100), 15, Colors.Gold);
        if (items.Count == 0) Text("(empty)", new Vector2(size.X - 198, 122), 14, Colors.Gray);
        for (int i = 0; i < items.Count; i++)
            Text($"{Items.Get(items[i].Key).Name} x{items[i].Value}", new Vector2(size.X - 198, 122 + i * 20), 14);

        // Message log
        var log = _game.Log.TakeLast(6).ToList();
        DrawRect(new Rect2(8, size.Y - 20 - log.Count * 20, 760, 14 + log.Count * 20), new Color(0, 0, 0, 0.5f));
        for (int i = 0; i < log.Count; i++)
            Text(log[i], new Vector2(16, size.Y - 6 - (log.Count - i) * 20 + 4), 14, i == log.Count - 1 ? Colors.White : new Color(1, 1, 1, 0.7f));

        Text("WASD move/interact  C craft  B build  X dismantle  U use  . wait  Esc quit",
            new Vector2(8, size.Y - 4), 12, new Color(1, 1, 1, 0.6f));
    }

    private void DrawMenu(Vector2 size)
    {
        float h = 50 + _menu.Count * 24;
        var rect = new Rect2(size.X / 2 - 260, size.Y / 2 - h / 2, 520, h);
        DrawRect(rect, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        DrawRect(rect, Colors.Gold, false, 2);
        Text(_menuTitle + "  (number to choose, Esc to cancel)", rect.Position + new Vector2(14, 28), 17, Colors.Gold);
        for (int i = 0; i < _menu.Count; i++)
            Text($"{i + 1})  {_menu[i].Label}", rect.Position + new Vector2(14, 56 + i * 24), 15);
    }

    private void DrawBanner(Vector2 size, string text)
    {
        var rect = new Rect2(size.X / 2 - 260, 90, 520, 36);
        DrawRect(rect, new Color(0.05f, 0.05f, 0.08f, 0.92f));
        DrawRect(rect, Colors.Gold, false, 2);
        Text(text, rect.Position + new Vector2(14, 24), 16);
    }

    private void DrawGameOver(Vector2 size)
    {
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0, 0, 0, 0.7f));
        Text("YOU DIED", new Vector2(size.X / 2 - 90, size.Y / 2 - 20), 40, Colors.OrangeRed);
        Text($"Survived until day {_game.Clock.Day}  -  {_game.Player.Kills} kills  -  {_game.BloodMoonsSurvived} blood moon(s)",
            new Vector2(size.X / 2 - 260, size.Y / 2 + 20), 18);
        Text("Press R to restart", new Vector2(size.X / 2 - 80, size.Y / 2 + 52), 16, Colors.Gray);
    }
}
