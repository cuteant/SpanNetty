// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Runtime.CompilerServices;

    /// <summary>
    /// Time utility class.
    /// </summary>
    public static class TimeUtil
    {
        /// <summary>
        /// Compare two timespan objects
        /// </summary>
        /// <param name="first">first timespan object</param>
        /// <param name="second">two timespan object</param>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static TimeSpan Max(TimeSpan first, TimeSpan second)
        {
            return first >= second ? first : second;
        }

        public static TimeSpan Min(TimeSpan first, TimeSpan second)
        {
            return first < second ? first : second;
        }

        /// <summary>
        /// Gets the system time.
        /// </summary>
        /// <returns>The system time.</returns>
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static TimeSpan GetSystemTime()
        {
            return TimeSpan.FromMilliseconds(Environment.TickCount);
        }

        public static TimeSpan Multiply(this TimeSpan timeSpan, double value)
        {
            double ticksD = checked((double)timeSpan.Ticks * value);
            long ticks = checked((long)ticksD);
            return TimeSpan.FromTicks(ticks);
        }

        public static TimeSpan Divide(this TimeSpan timeSpan, double value)
        {
            double ticksD = checked((double)timeSpan.Ticks / value);
            long ticks = checked((long)ticksD);
            return TimeSpan.FromTicks(ticks);
        }

        public static double Divide(this TimeSpan first, TimeSpan second)
        {
            double ticks1 = (double)first.Ticks;
            double ticks2 = (double)second.Ticks;
            return ticks1 / ticks2;
        }

        public static TimeSpan NextTimeSpan(this SafeRandom random, TimeSpan timeSpan)
        {
            if (timeSpan <= TimeSpan.Zero) { ThrowHelper.ArgumentOutOfRangeException_NextTimeSpan_Positive(timeSpan, ExceptionArgument.timeSpan); }
            double ticksD = ((double)timeSpan.Ticks) * random.NextDouble();
            long ticks = checked((long)ticksD);
            return TimeSpan.FromTicks(ticks);
        }

        public static TimeSpan NextTimeSpan(this SafeRandom random, TimeSpan minValue, TimeSpan maxValue)
        {
            if (minValue <= TimeSpan.Zero) { ThrowHelper.ArgumentOutOfRangeException_NextTimeSpan_Positive(minValue, ExceptionArgument.minValue); }
            if (minValue >= maxValue) { ThrowHelper.ArgumentOutOfRangeException_NextTimeSpan_minValue(minValue); }
            var span = maxValue - minValue;
            return minValue + random.NextTimeSpan(span);
        }
    }
}

