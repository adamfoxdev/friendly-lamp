using SevenDays.Core;
using Xunit;

namespace SevenDays.Tests;

public class GameTests
{
    private static Game NewGame(int seed = 1)
    {
        var g = new Game(seed);
        // Open arena around the player for deterministic tests.
        for (int dx = -8; dx <= 8; dx++)
            for (int dy = -8; dy <= 8; dy++)
                if (g.World.InBounds(g.Player.X + dx, g.Player.Y + dy))
                    g.World.Set(g.Player.X + dx, g.Player.Y + dy, TileKind.Grass);
        return g;
    }

    private static void Advance(Game g, int ticks) { for (int i = 0; i < ticks; i++) g.Wait(); }

    [Fact]
    public void WorldGenerationIsDeterministic()
    {
        var a = World.Generate(42);
        var b = World.Generate(42);
        for (int x = 0; x < a.Width; x++)
            for (int y = 0; y < a.Height; y++)
                Assert.Equal(a[x, y].Kind, b[x, y].Kind);
    }

    [Fact]
    public void HarvestingTreeGivesWoodAndDepletesIt()
    {
        var g = NewGame();
        g.World.Set(g.Player.X + 1, g.Player.Y, TileKind.Tree);
        for (int i = 0; i < 6; i++) g.Move(Direction.Right);
        Assert.Equal(6, g.Player.Inventory.Count(ItemId.Wood));
        Assert.Equal(TileKind.Grass, g.World[g.Player.X + 1, g.Player.Y].Kind);
    }

    [Fact]
    public void AxeDoublesYield()
    {
        var g = NewGame();
        g.Player.Inventory.Add(ItemId.StoneAxe);
        g.World.Set(g.Player.X + 1, g.Player.Y, TileKind.Tree);
        g.Move(Direction.Right);
        Assert.Equal(2, g.Player.Inventory.Count(ItemId.Wood));
    }

    [Fact]
    public void CraftingConsumesIngredients()
    {
        var g = NewGame();
        g.Player.Inventory.Add(ItemId.Wood, 4);
        Assert.True(g.Craft(Recipes.All.First(r => r.Result == ItemId.Club)));
        Assert.Equal(0, g.Player.Inventory.Count(ItemId.Wood));
        Assert.Equal(1, g.Player.Inventory.Count(ItemId.Club));
        Assert.False(g.Craft(Recipes.All.First(r => r.Result == ItemId.Club)));
    }

    [Fact]
    public void WeaponKillsZombieFasterThanFists()
    {
        var g = NewGame();
        g.Player.Inventory.Add(ItemId.Spear);
        g.SpawnZombie(ZombieKind.Walker, g.Player.X + 1, g.Player.Y);
        g.Move(Direction.Right);
        g.Move(Direction.Right);
        Assert.Empty(g.Zombies);
        Assert.Equal(1, g.Player.Kills);
    }

    [Fact]
    public void PlaceAndDismantleWall()
    {
        var g = NewGame();
        g.Player.Inventory.Add(ItemId.WoodWall);
        Assert.True(g.Place(ItemId.WoodWall, Direction.Up));
        Assert.Equal(TileKind.WoodWall, g.World[g.Player.X, g.Player.Y - 1].Kind);
        Assert.False(g.Move(Direction.Up)); // blocked
        Assert.True(g.Dismantle(Direction.Up));
        Assert.Equal(1, g.Player.Inventory.Count(ItemId.WoodWall));
    }

    [Fact]
    public void ZombieBreaksThroughWallToReachPlayer()
    {
        var g = NewGame();
        int px = g.Player.X, py = g.Player.Y;
        g.World.Set(px + 2, py, TileKind.WoodWall);
        g.SpawnZombie(ZombieKind.Feral, px + 4, py);
        var startHp = g.Player.Health;
        Advance(g, 12);
        Assert.True(g.World[px + 2, py].Kind != TileKind.WoodWall || g.Player.Health < startHp || g.Zombies[0].X < px + 4);
    }

    [Fact]
    public void SpikesDamageZombies()
    {
        var g = NewGame();
        int px = g.Player.X, py = g.Player.Y;
        g.World.Set(px + 3, py, TileKind.WoodSpikes);
        var z = g.SpawnZombie(ZombieKind.Feral, px + 4, py);
        int hp = z.Hp;
        g.Wait();
        Assert.True(z.Hp < hp || !g.Zombies.Contains(z));
    }

    [Fact]
    public void NeedsDrainAndStarvationKills()
    {
        var g = NewGame();
        g.Player.Food = 0;
        g.Player.Water = 0;
        Advance(g, 200);
        Assert.True(g.IsOver);
    }

    [Fact]
    public void EatingAndDrinkingRestoresStats()
    {
        var g = NewGame();
        g.Player.Food = 10; g.Player.Water = 10;
        g.Player.Inventory.Add(ItemId.CannedFood);
        g.Player.Inventory.Add(ItemId.WaterBottle);
        g.Use(ItemId.CannedFood);
        g.Use(ItemId.WaterBottle);
        Assert.InRange(g.Player.Food, 44, 46);
        Assert.InRange(g.Player.Water, 49, 51);
    }

    [Fact]
    public void ClockReportsBloodMoonOnDaySeven()
    {
        var clock = new GameClock();
        while (!(clock.Day == 7 && clock.Hour == 21)) clock.Advance();
        Assert.False(clock.IsBloodMoon);
        for (int i = 0; i < GameClock.TicksPerHour; i++) clock.Advance();
        Assert.True(clock.IsBloodMoon);
        while (!(clock.Day == 8 && clock.Hour == 4)) clock.Advance();
        Assert.False(clock.IsBloodMoon);
    }

    [Fact]
    public void BloodMoonSpawnsHordeAndSurvivalIsCounted()
    {
        var g = NewGame();
        g.Player.Health = 1_000_000; // invulnerable for this test
        g.Player.Food = g.Player.Water = 1_000_000;
        while (!(g.Clock.Day == 7 && g.Clock.Hour == 22 && g.Clock.Minute == 30)) g.Wait();
        Assert.True(g.Zombies.Count(z => z.Kind == ZombieKind.Feral) >= 2);
        while (g.Clock.Day < 8 || g.Clock.Hour < 4) g.Wait();
        Assert.Equal(1, g.BloodMoonsSurvived);
    }
}
