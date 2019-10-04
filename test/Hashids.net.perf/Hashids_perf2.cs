using System;
using System.Collections.Generic;
using System.Text;
using BenchmarkDotNet.Attributes;

namespace HashidsNet.perf
{
    public class Hashids_perf2
    {
        Hashids h;

        static Hashids_perf2()
        {
        }

        public Hashids_perf2()
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
    }
}
