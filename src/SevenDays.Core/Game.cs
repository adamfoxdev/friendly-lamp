namespace SevenDays.Core;

public sealed class Game
{
    private readonly List<string> _log = new();
    private int _hordeRemaining;

    public World World { get; }
    public Player Player { get; } = new();
    public GameClock Clock { get; } = new();
    public Random Rng { get; }
    public List<Zombie> Zombies { get; } = new();
    public IReadOnlyList<string> Log => _log;
    public bool IsOver { get; private set; }
    public int BloodMoonsSurvived { get; private set; }

    public Game(int seed = 0)
    {
        Rng = new Random(seed);
        World = World.Generate(seed);
        (Player.X, Player.Y) = World.Start;
        Say("You wake up in the wasteland. Gather wood, craft, and fortify before the blood moon on day 7.");
    }

    public void Say(string message)
    {
        _log.Add($"[{Clock}] {message}");
        if (_log.Count > 200) _log.RemoveAt(0);
    }

    // ---------------------------------------------------------------- Player actions

    /// <summary>Move in a direction, or interact with whatever is in the way.</summary>
    public bool Move(Direction dir)
    {
        if (IsOver) return false;
        var (dx, dy) = dir.Delta();
        int x = Player.X + dx, y = Player.Y + dy;
        if (!World.InBounds(x, y)) { Say("You've reached the edge of the world."); return false; }

        var zombie = ZombieAt(x, y);
        if (zombie != null) { Attack(zombie); Tick(); return true; }

        ref var tile = ref World[x, y];
        switch (tile.Kind)
        {
            case TileKind.Grass or TileKind.Road or TileKind.Floor or TileKind.WoodSpikes:
                Player.X = x; Player.Y = y;
                if (tile.Kind == TileKind.WoodSpikes) Hurt(3, "You step on your own spikes.");
                break;
            case TileKind.Tree:
                Gather(ref tile, ItemId.Wood, "tree");
                break;
            case TileKind.Rock:
                Gather(ref tile, ItemId.Stone, "rock");
                break;
            case TileKind.Water:
                Player.Water = Math.Min(Player.MaxStat, Player.Water + 15);
                Say("You drink from the water. (Careful: it's murky.)");
                break;
            case TileKind.Crate:
                LootCrate(x, y);
                break;
            case TileKind.EmptyCrate:
                Say("The crate is empty.");
                return false;
            case TileKind.RuinWall:
                Say("The ruined wall is too sturdy to break.");
                return false;
            default:
                Say("Your own wall blocks the way. Dismantle it if you want through.");
                return false;
        }
        Tick();
        return true;
    }

    public bool Craft(Recipe recipe)
    {
        if (IsOver) return false;
        if (!recipe.TryCraft(Player.Inventory))
        {
            Say($"You need {recipe.CostText} to craft {Items.Get(recipe.Result).Name}.");
            return false;
        }
        Say($"Crafted {recipe.Amount}x {Items.Get(recipe.Result).Name}.");
        Tick();
        return true;
    }

    public bool Place(ItemId item, Direction dir)
    {
        if (IsOver) return false;
        var def = Items.Get(item);
        if (def.Places is not { } kind) { Say($"{def.Name} can't be placed."); return false; }
        if (!Player.Inventory.Has(item)) { Say($"You have no {def.Name}."); return false; }

        var (dx, dy) = dir.Delta();
        int x = Player.X + dx, y = Player.Y + dy;
        if (!World.InBounds(x, y) || !TileInfo.IsBuildable(World[x, y].Kind) || ZombieAt(x, y) != null)
        {
            Say("You can't build there.");
            return false;
        }
        Player.Inventory.TryRemove(item);
        World.Set(x, y, kind);
        Say($"Placed {def.Name}.");
        Tick();
        return true;
    }

    public bool Dismantle(Direction dir)
    {
        if (IsOver) return false;
        var (dx, dy) = dir.Delta();
        int x = Player.X + dx, y = Player.Y + dy;
        if (!World.InBounds(x, y) || TileInfo.DroppedItem(World[x, y].Kind) is not { } item)
        {
            Say("There's nothing of yours to dismantle there.");
            return false;
        }
        Player.Inventory.Add(item);
        World.Set(x, y, TileKind.Grass);
        Say($"Recovered {Items.Get(item).Name}.");
        Tick();
        return true;
    }

    public bool Use(ItemId item)
    {
        if (IsOver) return false;
        var def = Items.Get(item);
        if (!def.IsConsumable) { Say($"You can't use {def.Name}."); return false; }
        if (!Player.Inventory.TryRemove(item)) { Say($"You have no {def.Name}."); return false; }
        Player.Food = Math.Min(Player.MaxStat, Player.Food + def.Food);
        Player.Water = Math.Min(Player.MaxStat, Player.Water + def.Water);
        Player.Health = Math.Min(Player.MaxStat, Player.Health + def.Heal);
        Say($"You use {def.Name}.");
        Tick();
        return true;
    }

    public void Wait()
    {
        if (!IsOver) Tick();
    }

    public Zombie? ZombieAt(int x, int y) => Zombies.FirstOrDefault(z => z.X == x && z.Y == y);

    public Zombie SpawnZombie(ZombieKind kind, int x, int y)
    {
        var z = new Zombie(kind, x, y);
        Zombies.Add(z);
        return z;
    }

    // ---------------------------------------------------------------- Internals

    private void Gather(ref Tile tile, ItemId item, string what)
    {
        int yield = 1 + Player.Inventory.BestGatherBonus();
        Player.Inventory.Add(item, yield);
        tile.Hp--;
        Say($"You harvest {yield} {Items.Get(item).Name} from the {what}.");
        if (tile.Hp <= 0) tile.Kind = TileKind.Grass;
    }

    private void LootCrate(int x, int y)
    {
        World.Set(x, y, TileKind.EmptyCrate);
        var table = new (ItemId Item, int Weight, int Max)[]
        {
            (ItemId.CannedFood, 25, 2), (ItemId.WaterBottle, 25, 2), (ItemId.Cloth, 25, 3),
            (ItemId.Bandage, 12, 1), (ItemId.Wood, 8, 4), (ItemId.Stone, 8, 3),
            (ItemId.Club, 3, 1), (ItemId.Spear, 2, 1),
        };
        int total = table.Sum(t => t.Weight);
        var found = new List<string>();
        for (int i = 0; i < Rng.Next(2, 4); i++)
        {
            int roll = Rng.Next(total);
            foreach (var (item, weight, max) in table)
            {
                if ((roll -= weight) >= 0) continue;
                int n = Rng.Next(1, max + 1);
                Player.Inventory.Add(item, n);
                found.Add($"{n}x {Items.Get(item).Name}");
                break;
            }
        }
        Say($"You search the crate: {string.Join(", ", found)}.");
    }

    private void Attack(Zombie z)
    {
        int dmg = Player.Inventory.BestDamage();
        z.Hp -= dmg;
        if (z.Hp > 0) { Say($"You hit the {z.Name} for {dmg}."); return; }
        Zombies.Remove(z);
        Player.Kills++;
        Say($"You kill the {z.Name}.");
        if (Rng.NextDouble() < 0.4) Player.Inventory.Add(ItemId.Cloth);
        if (Rng.NextDouble() < 0.1) Player.Inventory.Add(ItemId.CannedFood);
    }

    private void Hurt(double amount, string message)
    {
        Player.Health -= amount;
        Say(message);
        if (Player.Health <= 0 && !IsOver)
        {
            Player.Health = 0;
            IsOver = true;
            Say($"You died on day {Clock.Day}, having killed {Player.Kills} zombies and survived {BloodMoonsSurvived} blood moon(s).");
        }
    }

    private void Tick()
    {
        if (IsOver) return;
        bool wasBloodMoon = Clock.IsBloodMoon;
        Clock.Advance();

        UpdateNeeds();
        UpdateTimeEvents(wasBloodMoon);
        SpawnZombies();
        foreach (var z in Zombies.ToList())
        {
            if (IsOver) break;
            if (Clock.TotalTicks % z.ActEvery == 0) ActZombie(z);
        }
        DespawnAtDawn();
    }

    private void UpdateNeeds()
    {
        Player.Food = Math.Max(0, Player.Food - 0.15);
        Player.Water = Math.Max(0, Player.Water - 0.25);

        if (Player.Food <= 0 || Player.Water <= 0)
            Hurt(1, Player.Food <= 0 ? "You are starving!" : "You are dying of thirst!");
        else if (Player.Food > 50 && Player.Water > 50 && Player.Health < Player.MaxStat)
            Player.Health = Math.Min(Player.MaxStat, Player.Health + 0.25);
    }

    private void UpdateTimeEvents(bool wasBloodMoon)
    {
        if (Clock.IsBloodMoonDay && Clock.Hour == 18 && Clock.Minute == 0)
            Say("The sky is turning red. A blood moon rises tonight. Fortify your base!");

        if (Clock.IsBloodMoon && !wasBloodMoon)
        {
            _hordeRemaining = 8 + 2 * (Clock.Day / GameClock.BloodMoonInterval);
            Say("THE BLOOD MOON RISES! The horde is coming!");
        }
        else if (!Clock.IsBloodMoon && wasBloodMoon)
        {
            _hordeRemaining = 0;
            BloodMoonsSurvived++;
            Say("Dawn breaks. You survived the blood moon.");
        }
    }

    private void SpawnZombies()
    {
        if (Clock.IsBloodMoon)
        {
            for (int i = 0; i < 2 && _hordeRemaining > 0; i++)
                if (TrySpawnNearPlayer(ZombieKind.Feral, 14, 20)) _hordeRemaining--;
            return;
        }

        int cap = Clock.IsNight ? 10 : 2;
        double chance = Clock.IsNight ? 0.15 : 0.02;
        if (Zombies.Count < cap && Rng.NextDouble() < chance)
            TrySpawnNearPlayer(Rng.NextDouble() < 0.25 ? ZombieKind.Feral : ZombieKind.Walker, 10, 16);
    }

    private bool TrySpawnNearPlayer(ZombieKind kind, int minDist, int maxDist)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            double angle = Rng.NextDouble() * Math.PI * 2;
            int dist = Rng.Next(minDist, maxDist + 1);
            int x = Player.X + (int)Math.Round(Math.Cos(angle) * dist);
            int y = Player.Y + (int)Math.Round(Math.Sin(angle) * dist);
            if (!World.IsWalkable(x, y) || ZombieAt(x, y) != null) continue;
            SpawnZombie(kind, x, y);
            return true;
        }
        return false;
    }

    private void DespawnAtDawn()
    {
        if (Clock.Hour is >= 6 and < 20 && !Clock.IsBloodMoon)
            Zombies.RemoveAll(z => Rng.NextDouble() < 0.05 && Distance(z) > 8);
    }

    private int Distance(Zombie z) => Math.Abs(z.X - Player.X) + Math.Abs(z.Y - Player.Y);

    private void ActZombie(Zombie z)
    {
        int dist = Distance(z);
        if (dist == 1)
        {
            Hurt(z.Damage, $"The {z.Name} hits you for {z.Damage}!");
            return;
        }

        bool hunting = Clock.IsBloodMoon || dist <= 14;
        if (!hunting)
        {
            // Shamble around aimlessly.
            var (rx, ry) = ((Direction)Rng.Next(4)).Delta();
            TryStep(z, z.X + rx, z.Y + ry);
            return;
        }

        int dx = Math.Sign(Player.X - z.X), dy = Math.Sign(Player.Y - z.Y);
        bool horizontalFirst = Math.Abs(Player.X - z.X) >= Math.Abs(Player.Y - z.Y);
        var candidates = new List<(int X, int Y)>();
        if (horizontalFirst)
        {
            if (dx != 0) candidates.Add((z.X + dx, z.Y));
            if (dy != 0) candidates.Add((z.X, z.Y + dy));
            else { candidates.Add((z.X, z.Y + 1)); candidates.Add((z.X, z.Y - 1)); }
        }
        else
        {
            if (dy != 0) candidates.Add((z.X, z.Y + dy));
            if (dx != 0) candidates.Add((z.X + dx, z.Y));
            else { candidates.Add((z.X + 1, z.Y)); candidates.Add((z.X - 1, z.Y)); }
        }

        foreach (var (cx, cy) in candidates)
        {
            if (!World.InBounds(cx, cy) || ZombieAt(cx, cy) != null) continue;
            if (TryStep(z, cx, cy)) return;

            ref var tile = ref World[cx, cy];
            if (TileInfo.IsPlayerBlock(tile.Kind)) // spikes are walkable, so this means a wall
            {
                tile.Hp -= z.Damage;
                if (tile.Hp <= 0)
                {
                    Say($"A {z.Name} smashes through your {tile.Kind}!");
                    World.Set(cx, cy, TileKind.Grass);
                }
                return;
            }
        }
    }

    private bool TryStep(Zombie z, int x, int y)
    {
        if (!World.IsWalkable(x, y) || ZombieAt(x, y) != null || (Player.X == x && Player.Y == y)) return false;
        z.X = x; z.Y = y;

        ref var tile = ref World[x, y];
        if (tile.Kind == TileKind.WoodSpikes)
        {
            z.Hp -= 8;
            tile.Hp -= 2;
            if (tile.Hp <= 0) World.Set(x, y, TileKind.Grass);
            if (z.Hp <= 0)
            {
                Zombies.Remove(z);
                Player.Kills++;
                Say($"A {z.Name} is impaled on your spikes.");
            }
        }
        return true;
    }
}
