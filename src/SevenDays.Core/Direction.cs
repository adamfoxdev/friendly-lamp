namespace SevenDays.Core;

public enum Direction { Up, Down, Left, Right }

public static class DirectionExtensions
{
    public static (int Dx, int Dy) Delta(this Direction d) => d switch
    {
        Direction.Up => (0, -1),
        Direction.Down => (0, 1),
        Direction.Left => (-1, 0),
        _ => (1, 0),
    };
}
