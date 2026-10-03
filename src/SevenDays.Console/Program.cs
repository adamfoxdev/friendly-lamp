using SevenDays.Core;
using SevenDays.ConsoleApp;

int seed = Environment.TickCount;
bool snapshot = false;
for (int i = 0; i < args.Length; i++)
{
    if (args[i] == "--seed" && i + 1 < args.Length) seed = int.Parse(args[++i]);
    else if (args[i] == "--snapshot") snapshot = true;
}

var game = new Game(seed);

if (snapshot || Console.IsInputRedirected)
{
    Console.Write(Renderer.FrameAsText(game));
    return;
}

Console.Clear();
while (!game.IsOver)
{
    Renderer.Draw(game);
    var key = Console.ReadKey(true).Key;
    switch (key)
    {
        case ConsoleKey.W or ConsoleKey.UpArrow: game.Move(Direction.Up); break;
        case ConsoleKey.S or ConsoleKey.DownArrow: game.Move(Direction.Down); break;
        case ConsoleKey.A or ConsoleKey.LeftArrow: game.Move(Direction.Left); break;
        case ConsoleKey.D or ConsoleKey.RightArrow: game.Move(Direction.Right); break;
        case ConsoleKey.OemPeriod or ConsoleKey.Spacebar: game.Wait(); break;
        case ConsoleKey.C: CraftMenu(); break;
        case ConsoleKey.B: BuildMenu(); break;
        case ConsoleKey.U: UseMenu(); break;
        case ConsoleKey.X:
            if (AskDirection("Dismantle which direction?") is { } d) game.Dismantle(d);
            break;
        case ConsoleKey.Q: return;
    }
}

Renderer.Draw(game);
Console.WriteLine();
Console.WriteLine($"GAME OVER - you lasted until day {game.Clock.Day} with {game.Player.Kills} kills and {game.BloodMoonsSurvived} blood moon(s) survived.");

Direction? AskDirection(string prompt)
{
    Console.SetCursorPosition(0, 31);
    Console.Write(prompt.PadRight(70) + " (WASD, other = cancel)");
    Direction? result = Console.ReadKey(true).Key switch
    {
        ConsoleKey.W or ConsoleKey.UpArrow => Direction.Up,
        ConsoleKey.S or ConsoleKey.DownArrow => Direction.Down,
        ConsoleKey.A or ConsoleKey.LeftArrow => Direction.Left,
        ConsoleKey.D or ConsoleKey.RightArrow => Direction.Right,
        _ => null,
    };
    Console.SetCursorPosition(0, 31);
    Console.Write(new string(' ', 100));
    return result;
}

T? Pick<T>(string title, IReadOnlyList<(string Label, T Value)> options) where T : struct
{
    Console.Clear();
    Console.WriteLine(title);
    for (int i = 0; i < options.Count; i++) Console.WriteLine($" {i + 1}) {options[i].Label}");
    Console.WriteLine(" Any other key to cancel.");
    var c = Console.ReadKey(true).KeyChar;
    Console.Clear();
    return c >= '1' && c < '1' + options.Count ? options[c - '1'].Value : null;
}

void CraftMenu()
{
    var opts = Recipes.All.Select(r =>
        ($"{Items.Get(r.Result).Name,-14} {(r.CanCraft(game.Player.Inventory) ? "" : "(missing) ")}{r.CostText}", r)).ToList();
    var idx = Pick("Craft:", opts.Select((o, i) => (o.Item1, i)).ToList());
    if (idx is { } i) game.Craft(Recipes.All[i]);
}

void BuildMenu()
{
    var placeable = game.Player.Inventory.Entries.Where(e => Items.Get(e.Key).Places != null).ToList();
    if (placeable.Count == 0) { game.Say("You have nothing to build with. Craft walls first."); return; }
    var item = Pick("Build:", placeable.Select(e => ($"{Items.Get(e.Key).Name} x{e.Value}", e.Key)).ToList());
    if (item is { } it && AskDirection("Build in which direction?") is { } d) game.Place(it, d);
}

void UseMenu()
{
    var usable = game.Player.Inventory.Entries.Where(e => Items.Get(e.Key).IsConsumable).ToList();
    if (usable.Count == 0) { game.Say("You have nothing to eat, drink or heal with."); return; }
    var item = Pick("Use:", usable.Select(e => ($"{Items.Get(e.Key).Name} x{e.Value}", e.Key)).ToList());
    if (item is { } it) game.Use(it);
}
