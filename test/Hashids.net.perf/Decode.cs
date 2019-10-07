using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace HashidsNet.perf
{
    [MemoryDiagnoser]
    public class Decode
    {
        Hashids h;

        static Decode()
        {
            Hashids.ExploitArrayVariance = true;
        }

        public Decode()
        {
            h = new Hashids("this is my salt");
        }

        [Benchmark]
        public int DecodeInt32()
        {
            return h.Decode("ykJWW1g")[0];
        }

        [Benchmark]
        public long DecodeInt64()
        {
            return h.DecodeLong("jvNx4BjM5KYjv")[0];
        }

        [Benchmark]
        public ulong DecodeUInt64()
        {
            return h.DecodeUnsignedLong("zXVjmzBamYlqX")[0];
        }
    }
}
