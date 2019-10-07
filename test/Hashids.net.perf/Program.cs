using System;
using BenchmarkDotNet.Running;

namespace HashidsNet.perf
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            if (args.Length == 1 && args[0] == "-decodeMemory")
            {
                // for running inside VS or 
                return DecodePerf();
            }
            else
            {
                BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
                return 0;
            }
        }

        static int DecodePerf()
        {
            var d = new Decode();
            ulong x = 0;
            for (int i = 0; i < 100_000; i++)
            {
                x = x * 33 + d.DecodeUInt64();
            }
            return x.GetHashCode();
        }
    }
}
