namespace SevenDays.Core;

public sealed class Player
{
    public const double MaxStat = 100;

    public int X { get; set; }
    public int Y { get; set; }
    public double Health { get; set; } = MaxStat;
    public double Food { get; set; } = 80;
    public double Water { get; set; } = 80;
    public int Kills { get; set; }
    public Inventory Inventory { get; } = new();
}
