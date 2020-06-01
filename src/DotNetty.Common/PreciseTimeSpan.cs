// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System;
    using System.Diagnostics;

    public readonly struct PreciseTimeSpan : IComparable<PreciseTimeSpan>, IEquatable<PreciseTimeSpan>
    {
        private readonly long _ticks;

        PreciseTimeSpan(long ticks)
            : this()
        {
            _ticks = ticks;
        }

        public long Ticks => _ticks;

        public static readonly PreciseTimeSpan Zero = new PreciseTimeSpan(0);

        public static readonly PreciseTimeSpan MinusOne = new PreciseTimeSpan(-1);

        public static PreciseTimeSpan FromTicks(long ticks) => new PreciseTimeSpan(ticks);

        public static PreciseTimeSpan FromStart => new PreciseTimeSpan(GetTimeChangeSinceStart());

        public static PreciseTimeSpan FromTimeSpan(TimeSpan timeSpan) => new PreciseTimeSpan(TicksToPreciseTicks(timeSpan.Ticks));

        public static PreciseTimeSpan Deadline(TimeSpan deadline) => new PreciseTimeSpan(GetTimeChangeSinceStart() + TicksToPreciseTicks(deadline.Ticks));

        public static PreciseTimeSpan Deadline(in PreciseTimeSpan deadline) => new PreciseTimeSpan(GetTimeChangeSinceStart() + deadline._ticks);

        static long TicksToPreciseTicks(long ticks) => Stopwatch.IsHighResolution ? (long)(ticks * PreciseTimeSpanHelper.PrecisionRatio) : ticks;

        public TimeSpan ToTimeSpan() => TimeSpan.FromTicks((long)(_ticks * PreciseTimeSpanHelper.ReversePrecisionRatio));

        static long GetTimeChangeSinceStart() => Stopwatch.GetTimestamp() - PreciseTimeSpanHelper.StartTime;

        public bool Equals(PreciseTimeSpan other) => _ticks == other._ticks;

        public override bool Equals(object obj)
        {
            return obj is PreciseTimeSpan preciseTimeSpan && Equals(preciseTimeSpan);
        }

        public override int GetHashCode() => _ticks.GetHashCode();

        public int CompareTo(PreciseTimeSpan other) => _ticks.CompareTo(other._ticks);

        public static bool operator ==(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1._ticks == t2._ticks;

        public static bool operator !=(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1._ticks != t2._ticks;

        public static bool operator >(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1._ticks > t2._ticks;

        public static bool operator <(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1._ticks < t2._ticks;

        public static bool operator >=(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1._ticks >= t2._ticks;

        public static bool operator <=(PreciseTimeSpan t1, PreciseTimeSpan t2) => t1._ticks <= t2._ticks;

        public static PreciseTimeSpan operator +(PreciseTimeSpan t, TimeSpan duration)
        {
            long ticks = t._ticks + TicksToPreciseTicks(duration.Ticks);
            return new PreciseTimeSpan(ticks);
        }

        public static PreciseTimeSpan operator -(PreciseTimeSpan t, TimeSpan duration)
        {
            long ticks = t._ticks - TicksToPreciseTicks(duration.Ticks);
            return new PreciseTimeSpan(ticks);
        }

        public static PreciseTimeSpan operator -(PreciseTimeSpan t1, PreciseTimeSpan t2)
        {
            long ticks = t1._ticks - t2._ticks;
            return new PreciseTimeSpan(ticks);
        }
    }

    sealed class PreciseTimeSpanHelper
    {
        public static readonly long StartTime = Stopwatch.GetTimestamp();
        public static readonly double PrecisionRatio = (double)Stopwatch.Frequency / TimeSpan.TicksPerSecond;
        public static readonly double ReversePrecisionRatio = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;
    }
}