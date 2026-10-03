namespace SevenDays.Core;

public enum TileKind
{
    Grass, Tree, Rock, Water, Road, Floor,
    RuinWall, Crate, EmptyCrate,
    WoodWall, StoneWall, WoodSpikes,
}

public struct Tile
{
    public TileKind Kind;
    public int Hp;

    public Tile(TileKind kind) { Kind = kind; Hp = TileInfo.MaxHp(kind); }
}

public static class TileInfo
{
    public static bool IsWalkable(TileKind k) =>
        k is TileKind.Grass or TileKind.Road or TileKind.Floor or TileKind.WoodSpikes;

    /// <summary>Blocks the player constructed; zombies will chew through these.</summary>
    public static bool IsPlayerBlock(TileKind k) =>
        k is TileKind.WoodWall or TileKind.StoneWall or TileKind.WoodSpikes;

    /// <summary>Tiles a block may be placed on.</summary>
    public static bool IsBuildable(TileKind k) =>
        k is TileKind.Grass or TileKind.Road or TileKind.Floor;

    public static int MaxHp(TileKind k) => k switch
    {
        TileKind.Tree => 6,
        TileKind.Rock => 8,
        TileKind.RuinWall => 40,
        TileKind.WoodWall => 30,
        TileKind.StoneWall => 80,
        TileKind.WoodSpikes => 20,
        _ => 0,
    };

    public static ItemId? DroppedItem(TileKind k) => k switch
    {
        TileKind.WoodWall => ItemId.WoodWall,
        TileKind.StoneWall => ItemId.StoneWall,
        TileKind.WoodSpikes => ItemId.WoodSpikes,
        _ => null,
    };
}
