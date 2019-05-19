using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Open.ChannelExtensions.Tests
{
    class Program
    {
        static async Task Main()
        {
            const int repeat = 100;
            {
                Console.WriteLine("Standard operation test...");
                var sw = Stopwatch.StartNew();
                await Enumerable
                    .Repeat((Func<int, ValueTask<int>>)Delay, repeat)
                    .Select((t, i) => t(i))
                    .ToChannelAsync(singleReader: true)
                    .ReadAll(Dummy);
                sw.Stop();
                Console.WriteLine(sw.Elapsed);
                Console.WriteLine();
            }

            {
                Console.WriteLine("Concurrent operation test...");
                var sw = Stopwatch.StartNew();
                await Enumerable
                    .Repeat((Func<int, ValueTask<int>>)Delay, repeat)
                    .Select((t, i) => t(i))
                    .ToChannelAsync(singleReader: false, maxConcurrency: 4)
                    .ReadAllConcurrently(4, Dummy);
                sw.Stop();
                Console.WriteLine(sw.Elapsed);
                Console.WriteLine();
            }

            {
                Console.WriteLine("Pipe operation test...");
                var sw = Stopwatch.StartNew();
                await Enumerable
                    .Repeat((Func<int, ValueTask<int>>)Delay, repeat)
                    .Select((t, i) => t(i))
                    .ToChannelAsync()
                    .Pipe(i => i * 2)
                    .ReadAll(Dummy);
                sw.Stop();
                Console.WriteLine(sw.Elapsed);
                Console.WriteLine();
            }

        }

        static void Dummy(int i)
        {

        }

        static async ValueTask<int> Delay(int i)
        {
            await Task.Delay(100);
            return i;
        }
    }
}
