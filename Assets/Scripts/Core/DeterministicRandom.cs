using System;

namespace ChromeDino.Core
{
    // The explicit state makes a checkpoint independent of Unity/System.Random versions.
    public sealed class DeterministicRandom
    {
        public uint State { get; private set; }

        public DeterministicRandom(uint seed) { State = seed == 0 ? 0x6D2B79F5u : seed; }

        public uint NextUInt()
        {
            uint value = State;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return State = value;
        }

        public double NextDouble() { return NextUInt() / 4294967296.0; }

        public int NextInt(int exclusiveMax)
        {
            if (exclusiveMax <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMax));
            return (int)(NextDouble() * exclusiveMax);
        }

        public double Gaussian()
        {
            return Math.Sqrt(-2 * Math.Log(1 - NextDouble())) * Math.Cos(2 * Math.PI * NextDouble());
        }
    }
}
