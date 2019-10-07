using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace HashidsNet.perf
{
    [MemoryDiagnoser]
    public class Encode
    {
        Hashids h;

        static Encode()
        {
            Hashids.ExploitArrayVariance = true;
        }

        public Encode()
        {
            h = new Hashids();
        }

        [Benchmark]
        public string EncodeInt32()
        {
            return h.Encode(int.MaxValue);
        }

        [Benchmark]
        public string EncodeInt64()
        {
            return h.EncodeLong(long.MaxValue);
        }

        [Benchmark]
        public string EncodeUInt64()
        {
            return h.EncodeUnsignedLong(ulong.MaxValue);
        }
    }
}
