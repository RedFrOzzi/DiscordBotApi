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
        return Hours == other.Hours
            && Minutes == other.Minutes
            && Seconds == other.Minutes
            && Miliseconds == other.Miliseconds
            && IsTillTheEnd == other.IsTillTheEnd;
    }

    public override readonly bool Equals(object? obj)
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

    public static bool operator <(TimeStamp left, TimeStamp right)
    {
        if (left.Equals(right))
        {
            return false;
        }

        if (left.IsTillTheEnd && right.IsTillTheEnd)
        {
            return false;
        }

        if (!left.IsTillTheEnd && right.IsTillTheEnd)
        {
            return true;
        }

        if (left.Hours > right.Hours)
            return false;

        if (left.Minutes > right.Minutes)
            return false;

        if (left.Seconds > right.Seconds)
            return false;

        if (left.Miliseconds > right.Miliseconds)
            return false;

        return true;
    }

    public static bool operator >(TimeStamp left, TimeStamp right)
    {
        if (left.Equals(right))
        {
            return false;
        }

        if (left.IsTillTheEnd && right.IsTillTheEnd)
        {
            return false;
        }

        if (left.IsTillTheEnd && !right.IsTillTheEnd)
        {
            return true;
        }

        if (left.Hours < right.Hours)
            return false;

        if (left.Minutes < right.Minutes)
            return false;

        if (left.Seconds < right.Seconds)
            return false;

        if (left.Miliseconds < right.Miliseconds)
            return false;

        return true;
    }
}
