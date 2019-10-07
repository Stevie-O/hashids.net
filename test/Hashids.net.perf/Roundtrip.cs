using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace HashidsNet.perf
{
    [MemoryDiagnoser]
    public class Roundtrip
    {
        Hashids h;

        static Roundtrip()
        {
            Hashids.ExploitArrayVariance = true;
        }

        public Roundtrip()
        {
            h = new Hashids();
        }

        [Benchmark]
        public int RoundTripInt32()
        {
            return h.Decode(h.Encode(int.MaxValue))[0];
        }

        [Benchmark]
        public long RoundTripInt64()
        {
            return h.DecodeLong(h.EncodeLong(long.MaxValue))[0];
        }

        [Benchmark]
        public ulong RoundTripUInt64()
        {
            return h.DecodeUnsignedLong(h.EncodeUnsignedLong(ulong.MaxValue))[0];
        }
    }
}

