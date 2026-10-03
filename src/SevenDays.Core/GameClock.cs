namespace SevenDays.Core;

/// <summary>One tick is ten in-game minutes. The game starts at 08:00 on day 1.</summary>
public sealed class GameClock
{
    public const int TicksPerHour = 6;
    public const int TicksPerDay = 24 * TicksPerHour;
    public const int BloodMoonInterval = 7;

    public int TotalTicks { get; private set; } = 8 * TicksPerHour;

    public int Day => TotalTicks / TicksPerDay + 1;
    public int Hour => TotalTicks % TicksPerDay / TicksPerHour;
    public int Minute => TotalTicks % TicksPerHour * 10;

    public bool IsNight => Hour >= 20 || Hour < 5;

    /// <summary>Blood moon runs 22:00 on every 7th day until 04:00 the next morning.</summary>
    public bool IsBloodMoon =>
        (Day % BloodMoonInterval == 0 && Hour >= 22) ||
        (Day > 1 && Day % BloodMoonInterval == 1 && Hour < 4);

    public bool IsBloodMoonDay => Day % BloodMoonInterval == 0;

    public void Advance() => TotalTicks++;

    public override string ToString() => $"Day {Day} {Hour:00}:{Minute:00}";
}
