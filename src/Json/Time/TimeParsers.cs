using System.Diagnostics;
using System.Globalization;

using Superpower;
using Superpower.Model;
using Superpower.Parsers;

using SurrealDB.Common;

namespace SurrealDB.Json.Time;

public static class TimeParsers {

    private class GreedyResultComparer<T> : IComparer<Result<T>> {
        public int Compare(Result<T> x, Result<T> y) {
            // 1 Success is best
            int state = x.HasValue.CompareTo(y.HasValue);
            if (state != 0) {
                return state;
            }

            // 2 Shortest remainder is best
            int rem = -x.Remainder.Length.CompareTo(y.Remainder.Length);
            if (rem != 0) {
                return rem;
            }

            // 3 Longest result is best
            return x.Location.Length.CompareTo(y.Location.Length);
        }

        private static readonly Lazy<GreedyResultComparer<T>> s_instance = new(static () => new());
        public static GreedyResultComparer<T> Instance => s_instance.Value;
    }

    /// <summary>
    /// Applies all parsers on the same input, greedily returns the best result.
    /// </summary>
    public static TextParser<T> Alt<T>(params TextParser<T>[] parsers) => input => {
        Result<T> best = Result.Empty<T>(input, "no parser in Alt branch matched the input");
        GreedyResultComparer<T> cmp = GreedyResultComparer<T>.Instance;
        foreach (TextParser<T> p in parsers) {
            Result<T> res = p(input);
            if (cmp.Compare(best, res) >= 0) {
                continue;
            }

            best = res;
            if (best.Remainder.IsAtEnd) {
                break; // no more greedy then taking it all
            }
        }

        return best;
    };

    public static TextParser<(T1, T2)> Combine<T1, T2>(this TextParser<T1> parser1, TextParser<T2> parser2) => input => {
        var res1 = parser1(input);
        if (!res1.HasValue) {
            return Result.CastEmpty<T1, (T1, T2)>(res1);
        }
        var res2 = parser2(res1.Remainder);
        if (!res2.HasValue) {
            return Result.CastEmpty<T1, (T1, T2)>(Result.CombineEmpty(res1, Result.CastEmpty<T2, T1>(res2)));
        }

        return Result.Value((res1.Value, res2.Value), res1.Location.Combine(res2.Location), res2.Remainder);
    };
    public static TextParser<(T1, T2, T3)> Combine<T1, T2, T3>(this TextParser<T1> parser1, TextParser<T2> parser2, TextParser<T3> parser3) => 
        parser1.Combine(parser2).Combine(parser3).Select(static t => t.Flatten());

    public static TextSpan Combine(in this TextSpan lhs, in TextSpan rhs) {
        Debug.Assert(lhs.Source == rhs.Source);
        Debug.Assert(lhs.Position.Absolute <= rhs.Position.Absolute);
        int endLhs = lhs.Position.Absolute + lhs.Length;
        int endRhs = rhs.Position.Absolute + rhs.Length;
        return new(lhs.Source!, lhs.Position, (endLhs < endRhs ? endRhs : endLhs) - lhs.Position.Absolute);
    }

    public static (T1, T2, T3) Flatten<T1, T2, T3>(in this ((T1, T2), T3) v) => (v.Item1.Item1, v.Item1.Item2, v.Item2);

    private static TextParser<char> Sign { get; } = Character.In('+', '-');
    private static TextParser<char> FracSep { get; } = Character.EqualTo('.');
    private static TextParser<char> DateSep { get; } = Character.In('-', '/');
    private static TextParser<char> TimeSep { get; } = Character.EqualTo(':');
    private static TextParser<char> TimeSegSep { get; } = Character.In('t', 'T', ' ');
    private static TextParser<int> IntDigits { get; } =
        Character.Digit.AtLeastOnce().Select(static c => Int32.Parse(c));

    public static TextParser<double> Float { get; } =
        from sign in Character.EqualTo('-').Value(-1.0).OptionalOrDefault(1.0)
        from whole in Numerics.Natural.Select(n => double.Parse(n.ToStringValue()))
        from frac in Character.EqualTo('.')
           .IgnoreThen(Numerics.Natural)
           .Select(n => double.Parse(n.ToStringValue()) * Math.Pow(10, -n.Length))
           .OptionalOrDefault()
        from exp in Character.EqualToIgnoreCase('e')
           .IgnoreThen(Character.EqualTo('+').Value(1.0)
               .Or(Character.EqualTo('-').Value(-1.0))
               .OptionalOrDefault(1.0))
           .Then(expsign => Numerics.Natural.Select(n => double.Parse(n.ToStringValue()) * expsign))
           .OptionalOrDefault()
        select (whole + frac) * sign * Math.Pow(10, exp);

    /// <summary>
    /// Parses any ISO8601 like <see cref="DateOnly"/> `{year}-{month}-{day}`
    /// </summary>
    public static TextParser<DateOnly> IsoDate { get; } =
        from year in IntDigits
        from month in DateSep.IgnoreThen(IntDigits)
        from day in DateSep.IgnoreThen(IntDigits)
        select new DateOnly(year, month, day);

    /// <summary>
    /// Parses any ISo8601 like time fraction into ticks.
    /// A tick is 100 ns (nanoseconds), that is 0.0000001 seconds.
    /// The fraction is multiplied by 7 then divided by the number of digits.
    /// </summary>
    public static TextParser<long> IsoTimeFraction { get; } =
        Character.Digit.AtLeastOnce().Select(static c => (long)(double.Parse(c.AsSpan(0, Math.Min(7, c.Length)), NumberStyles.None) * Math.Pow(10, Math.Max(0, 7 - c.Length))));
    
    /// <summary>
    /// Parses any ISO8601 like <see cref="TimeOnly"/> `{hour}:{minute}:{second}.{fraction}`
    /// </summary>
    public static TextParser<TimeOnly> IsoTime { get; } =
        from hour in IntDigits
        from minute in TimeSep.IgnoreThen(IntDigits)
        from second in TimeSep
           .IgnoreThen(IntDigits)
           .OptionalOrDefault()
        from fraction in FracSep.IgnoreThen(IsoTimeFraction).OptionalOrDefault()
        select new TimeOnly(new TimeOnly(hour, minute, second).Ticks + fraction);

    /// <summary>
    /// Parses the ISO8601 UTC timezone `Z`
    /// </summary>
    private static TextParser<TimeSpan> IsoTimezoneUtc => 
        Character.EqualToIgnoreCase('Z').Value(TimeSpan.Zero);

    /// <summary>
    /// Parses any ISO8601 offset `[+-]\d+(:\d+)?`
    /// </summary>
    private static TextParser<TimeSpan> IsoTimezone { get; } =
        from sign in Sign.OptionalOrDefault()
        from hours in IntDigits
        from minutes in TimeSep.OptionalOrDefault().IgnoreThen(IntDigits).OptionalOrDefault()
        select new TimeSpan(hours, minutes, 0) * (sign == '-' ? -1 : 1);

    public static TextParser<DateTime> IsoDateTimeUtc { get; } =
        from date in IsoDate
        from time in TimeSegSep.IgnoreThen(IsoTime).OptionalOrDefault()
        select date.ToDateTime(time, DateTimeKind.Utc);

    /// <summary>
    /// Parses any ISO8601 <see cref="DateTimeOffset"/> `{year}-{month}-{day}T{hour}:{minute}:{second}{offset}`, 
    /// where `{offset}` is `(Z)|([+-]\d+(\:?\d+)?)`
    /// </summary>
    public static TextParser<DateTimeOffset> IsoDateTimeOffset { get; } =
        from dt in IsoDateTimeUtc
        from tz in IsoTimezoneUtc.Or(IsoTimezone)
        select dt.WithOffset(tz);

    private static TextParser<double> TimeUnitParser { get; } =
        Alt(
            Span.EqualToIgnoreCase("ns").Value(10d / 1000d),
            Span.EqualToIgnoreCase("us").Value(10d),
            Span.EqualToIgnoreCase("µs").Value(10d),
            Span.EqualToIgnoreCase("ms").Value((double)TimeSpan.TicksPerMillisecond),
            Span.EqualToIgnoreCase("s").Value((double)TimeSpan.TicksPerSecond),
            Span.EqualToIgnoreCase("m").Value((double)TimeSpan.TicksPerMinute),
            Span.EqualToIgnoreCase("h").Value((double)TimeSpan.TicksPerHour),
            Span.EqualToIgnoreCase("d").Value((double)TimeSpan.TicksPerDay)
            );

    private static TextParser<TimeSpan> SpecificTimeSegment { get; } =
        from value in Float
        from unit in TimeUnitParser
        select new TimeSpan(unchecked((long)(value * unit)));

    /// <summary>
    /// Parses a time span from a set of specific time unit tuples, e.g. `1h`, `59m`, `19s`, `1ms`
    /// Valid units are: day := `d`, hour := `h`, minute := `m`, second := `s`,
    /// milliseconds := `ms`, microseconds := `us` |`µs`, nanoseconds := `ns`,
    /// ticks := `ticks` | &lt;empty&gt;
    /// </summary>
    public static TextParser<TimeSpan> SpecificTimeSpan { get; } =
        from unit in SpecificTimeSegment.AtLeastOnce()
        select unit.Aggregate(static (lhs, rhs) => lhs + rhs);


    /// <summary>
    /// Parses any ISO8601 like <see cref="TimeSpan"/> `{sign}{hour}:{minute}:{second}.{fraction}`
    /// Where `{sign}` is `[+-]?`. 
    /// </summary>
    public static TextParser<TimeSpan> IsoTimeSpan { get; } =
        from sign in Sign.OptionalOrDefault()
        from time in Alt(
            Combine(IntDigits, FracSep, IsoTime).Select(t => t.Item3.ToTimeSpan() + TimeSpan.FromDays(t.Item1)),
            IsoTime.Select(t => t.ToTimeSpan())
            )
        select time * (sign == '-' ? -1 : 1);

    public static TextParser<TimeSpan> AnyTimeSpan { get; } =
        Alt(IsoTimeSpan, SpecificTimeSpan);
}
