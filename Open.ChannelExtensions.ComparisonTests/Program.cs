using Open.ChannelExtensions.Tests;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Open.ChannelExtensions.ComparisonTests;

static class Program
{
	static async Task Main()
	{
		const int repeat = 50;
		const int concurrency = 4;
		const int testSize = 30000001;

		{
			Console.WriteLine("Standard DataFlow operation test...");
			var block = new ActionBlock<int>(async i => await Delay(i).ConfigureAwait(false));
			var sw = Stopwatch.StartNew();
			foreach (int i in Enumerable.Range(0, repeat))
				block.Post(i);
			block.Complete();
			await block.Completion.ConfigureAwait(false);
			sw.Stop();
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}

		await BasicTests.ReadAll(testSize).ConfigureAwait(false);
		await BasicTests.ReadAllAsync(testSize).ConfigureAwait(false);
		await BasicTests.BatchThenJoin(testSize, 5001).ConfigureAwait(false);
		await BasicTests.BatchJoin(testSize, 50).ConfigureAwait(false);

		{
			Console.WriteLine("Standard Channel filter test...");
			System.Collections.Generic.IEnumerable<ValueTask<int>> source = Enumerable
				.Repeat((Func<int, ValueTask<int>>)Delay, repeat)
				.Select((t, i) => t(i));

			var sw = Stopwatch.StartNew();
			long total = await source
				.ToChannelAsync(singleReader: true)
				.Filter(i => i % 2 == 0)
				.ReadAll(Dummy).ConfigureAwait(false);
			sw.Stop();

			Debug.Assert(total == repeat / 2);
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}

		{
			Console.WriteLine("Concurrent DataFlow operation test...");
			var sw = Stopwatch.StartNew();
			var block = new ActionBlock<int>(async i => await Delay(i).ConfigureAwait(false), new ExecutionDataflowBlockOptions { MaxDegreeOfParallelism = concurrency });
			foreach (int i in Enumerable.Range(0, repeat))
				block.Post(i);
			block.Complete();
			await block.Completion.ConfigureAwait(false);
			sw.Stop();
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}

		{
			Console.WriteLine("Concurrent Channel operation test...");
			var sw = Stopwatch.StartNew();
			await Enumerable
				.Repeat((Func<int, ValueTask<int>>)Delay, repeat)
				.Select((t, i) => t(i))
				.ToChannelAsync(singleReader: false, maxConcurrency: concurrency)
				.ReadAllConcurrently(4, Dummy).ConfigureAwait(false);
			sw.Stop();
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}

		{
			Console.WriteLine("Pipe operation test...");
			var sw = Stopwatch.StartNew();
			long total = await Enumerable
				.Repeat((Func<int, ValueTask<int>>)Delay, repeat)
				.Select((t, i) => t(i))
				.ToChannelAsync()
				.Pipe(i => i * 2)
				.ReadAll(Dummy).ConfigureAwait(false);
			sw.Stop();
			Debug.Assert(total == repeat);
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}

		{
			Console.WriteLine("Transform operation test...");
			var sw = Stopwatch.StartNew();
			await Enumerable
				.Repeat((Func<int, ValueTask<int>>)Delay, repeat)
				.Select((t, i) => t(i))
				.ToChannelAsync()
				.Transform(i => i * 2L)
				.ReadAll(Dummy).ConfigureAwait(false);
			sw.Stop();
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}

#if NETCOREAPP3_0
		{
			Console.WriteLine("Async Enumerable test...");
			var sw = Stopwatch.StartNew();
			await foreach (var e in Enumerable
				.Repeat((Func<int, ValueTask<int>>)Delay, repeat)
				.Select((t, i) => t(i))
				.ToChannelAsync()
				.ReadAllAsync())
				Dummy(e);
			sw.Stop();
			Console.WriteLine(sw.Elapsed);
			Console.WriteLine();
		}
#endif

	}

	static void Dummy(int i)
	{
	}

	static void Dummy(long i)
	{
	}

	static async ValueTask<int> Delay(int i)
	{
		await Task.Delay(100).ConfigureAwait(false);
		return i;
	}
}
