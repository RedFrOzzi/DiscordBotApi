using System.Diagnostics.CodeAnalysis;

namespace DiscordBotApi.Utilities;

public struct TimeStamp : IEquatable<TimeStamp>
{
    public int Hours { get; set; }
    public int Minutes { get; set; }
    public int Seconds { get; set; }
    public int Miliseconds { get; set; }
    public bool IsTillTheEnd { get; set; }

    public readonly bool Equals(TimeStamp other)
    {
        if (Hours != other.Hours
            || Minutes != other.Minutes
            || Seconds != other.Minutes
            || Miliseconds != other.Miliseconds
            || IsTillTheEnd != other.IsTillTheEnd)
        {
            return false;
        }

        return true;
    }

    public override readonly bool Equals([NotNullWhen(true)] object? obj)
    {
        if (obj == null || obj is not TimeStamp ts)
            return false;

        return Equals(ts);
    }

    public override readonly int GetHashCode()
    {
        return HashCode.Combine(Hours, Minutes, Seconds, Miliseconds, IsTillTheEnd);
    }

    public override readonly string ToString()
    {
        if (IsTillTheEnd)
        {
            return "inf";
        }

        return $"{Hours}:{Minutes}:{Seconds}.{Miliseconds}";
    }

    public static bool operator ==(TimeStamp left, TimeStamp right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(TimeStamp left, TimeStamp right)
    {
        return !(left == right);
    }
}
