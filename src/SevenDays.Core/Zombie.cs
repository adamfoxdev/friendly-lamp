namespace SevenDays.Core;

public enum ZombieKind { Walker, Feral }

public sealed class Zombie
{
    public ZombieKind Kind { get; }
    public int X { get; set; }
    public int Y { get; set; }
    public int Hp { get; set; }
    public int Damage { get; }

    /// <summary>The zombie acts once every this many ticks.</summary>
    public int ActEvery { get; }

    public Zombie(ZombieKind kind, int x, int y)
    {
        Kind = kind; X = x; Y = y;
        (Hp, Damage, ActEvery) = kind == ZombieKind.Walker ? (30, 5, 2) : (45, 8, 1);
    }

    public string Name => Kind == ZombieKind.Walker ? "Walker" : "Feral zombie";
}
