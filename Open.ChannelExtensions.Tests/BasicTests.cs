using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests;

public static class BasicTests
{
	const int testSize1 = 10000001;
	const int testSize2 = 30000001;

	[Theory]
	[InlineData(testSize1)]
	[InlineData(testSize2)]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "Testing only.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "<Pending>")]
	public static async Task DeferredWriteRead(int testSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result = new List<int>(testSize);

		var sw = Stopwatch.StartNew();
		ChannelReader<int> reader = range
			.ToChannel(singleReader: true, deferredExecution: true);

		_ = reader.ReadAll(i => result.Add(i), true);

		await reader.Completion;
		sw.Stop();

		Console.WriteLine("ReadAll(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(range));
		result.Clear();
	}

	[Theory]
	[InlineData(testSize1)]
	[InlineData(testSize2)]
	public static async Task ReadAll(int testSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result = new List<int>(testSize);

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.ReadAll(i => result.Add(i));
		sw.Stop();

		Console.WriteLine("ReadAll(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(range));
		result.Clear();
	}

	[Fact]
	public static async Task ReadAllAsEnumerables1()
	{
		var channel = Channel.CreateUnbounded<int>();
		var read = channel.Reader.ReadAllAsEnumerables(e =>
		{
			Thread.Sleep(100);
			Assert.Equal(1000, e.Count());
		});

		await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		for (var i = 0; i < 5; i++)
		{
			await Task.Delay(1000);
			await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		}

		channel.Writer.Complete();
		await read;
	}

	[Fact]
	public static async Task ReadAllAsEnumerablesAsync1()
	{
		var channel = Channel.CreateUnbounded<int>();
		var read = channel.Reader.ReadAllAsEnumerablesAsync(async e =>
		{
			await Task.Delay(100);
			Assert.Equal(1000, e.Count());
		});

		await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		for (var i = 0; i < 5; i++)
		{
			await Task.Delay(1000);
			await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		}

		channel.Writer.Complete();
		await read;
	}

	[Fact]
	public static async Task ReadAllConcurrentlyAsEnumerablesAsync()
	{
		var channel = Channel.CreateUnbounded<int>();
		int total = 0;
		var read = channel.Reader.ReadAllConcurrentlyAsEnumerablesAsync(3, async e =>
		{
			foreach(var i in e)
			{
				await Task.Delay(1);
				Interlocked.Increment(ref total);
			}
		});

		await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		for (var i = 0; i < 5; i++)
		{
			await Task.Delay(1000);
			await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		}

		channel.Writer.Complete();
		await read;

		Assert.Equal(6000, total);
	}


	[Fact]
	public static async Task ReadAllConcurrentlyAsEnumerables()
	{
		var channel = Channel.CreateUnbounded<int>();
		int total = 0;
		var read = channel.Reader.ReadAllConcurrentlyAsEnumerables(3, e =>
		{
			foreach (var i in e)
			{
				for(var n = 0; n < 2000000; n++)
				{
					// loop delay
				}
				Interlocked.Increment(ref total);
			}
		});

		await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		for (var i = 0; i < 5; i++)
		{
			await Task.Delay(1000);
			await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		}

		channel.Writer.Complete();
		await read;

		Assert.Equal(6000, total);
	}


	[Fact]
	public static async Task ReadAllAsEnumerables2()
	{
		var channel = Channel.CreateUnbounded<int>();
		int total = 0;
		var read = channel.Reader.ReadAllAsEnumerables(e => total += e.Count());

		await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		for (var i = 0; i < 5; i++)
		{
			await Task.Delay(1000);
			await channel.Writer.WriteAll(Enumerable.Range(0, 1000));
		}

		channel.Writer.Complete();
		await read;

		Assert.Equal(6000, total);
	}

	[Theory]
	[InlineData(testSize1)]
	[InlineData(testSize2)]
	public static async Task ReadAllAsync(int testSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result = new List<int>(testSize);

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.ReadAllAsync(i =>
			{
				result.Add(i);
				return new ValueTask();
			});
		sw.Stop();

		Console.WriteLine("Channel.ReadAllAsync(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(range));
		result.Clear();
	}

	[Theory]
	[InlineData(testSize1)]
	[InlineData(testSize2)]
	public static async Task PipeToBounded(int testSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result = new List<int>(testSize);

		var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
		{
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = true
		});

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.PipeTo(channel)
			.ReadAllAsync(i =>
			{
				result.Add(i);
				return new ValueTask();
			});
		sw.Stop();

		Console.WriteLine("Channel.ReadAllAsync(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(range));
		result.Clear();
	}

	[Theory]
	[InlineData(testSize1)]
	[InlineData(testSize2)]
	public static async Task PipeToUnbound(int testSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result = new List<int>(testSize);

		var channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions()
		{
			SingleReader = true,
			SingleWriter = true,
			AllowSynchronousContinuations = true
		});

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.PipeTo(channel)
			.ReadAllAsync(i =>
			{
				result.Add(i);
				return new ValueTask();
			});
		sw.Stop();

		Console.WriteLine("Channel.ReadAllAsync(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(range));
		result.Clear();
	}

	[Theory]
	[InlineData(1000, 51)]
	[InlineData(50, 1000)]
	[InlineData(1001, 51)]
	[InlineData(51, 5001)]
	[InlineData(75, 50)]
	public static async Task Batch(int testSize, int batchSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		int expectedBatchCount = testSize / batchSize + (testSize % batchSize == 0 ? 0 : 1);
		var result1 = new List<List<int>>(expectedBatchCount);

		long total = await range
			.ToChannel(singleReader: true)
			.Batch(batchSize, singleReader: true)
			.ReadAll(result1.Add);

		Assert.Equal(expectedBatchCount, result1.Count);

		var r = result1.SelectMany(e => e).ToList();
		Assert.Equal(testSize, r.Count);
		Assert.True(r.SequenceEqual(range));
	}


	[Theory]
	[InlineData(1000, 51)]
	[InlineData(50, 1000)]
	[InlineData(1001, 51)]
	[InlineData(51, 5001)]
	[InlineData(75, 50)]
	public static async Task BatchToQueues(int testSize, int batchSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		int expectedBatchCount = testSize / batchSize + (testSize % batchSize == 0 ? 0 : 1);
		var result1 = new List<Queue<int>>(expectedBatchCount);

		long total = await range
			.ToChannel(singleReader: true)
			.BatchToQueues(batchSize, singleReader: true)
			.ReadAll(result1.Add);

		Assert.Equal(expectedBatchCount, result1.Count);

		var r = result1.SelectMany(e => e).ToList();
		Assert.Equal(testSize, r.Count);
		Assert.True(r.SequenceEqual(range));
	}

	[Theory]
	[InlineData(testSize1, 51)]
	[InlineData(testSize1, 5001)]
	[InlineData(testSize2, 51)]
	[InlineData(testSize2, 5001)]
	[InlineData(100, 100)]
	public static async Task BatchThenJoin(int testSize, int batchSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result1 = new List<List<int>>(testSize / batchSize + 1);

		{
			var sw = Stopwatch.StartNew();
			long total = await range
				.ToChannel(singleReader: true)
				.Batch(batchSize, singleReader: true)
				.ReadAll(i => result1.Add(i));
			sw.Stop();

			Console.WriteLine("Channel.Batch({1}): {0}", sw.Elapsed, batchSize);
			Console.WriteLine();

			var r = result1.SelectMany(e => e).ToList();
			Assert.Equal(testSize, r.Count);
			Assert.True(r.SequenceEqual(range));
		}

		{
			var result2 = new List<int>(testSize);
			var sw = Stopwatch.StartNew();
			long total = await result1
				.ToChannel(singleReader: true)
				.Join(singleReader: true)
				.ReadAll(i => result2.Add(i));
			sw.Stop();

			Console.WriteLine("Channel.Join(): {0}", sw.Elapsed);
			Console.WriteLine();

			Assert.Equal(testSize, result2.Count);
			Assert.True(result2.SequenceEqual(range));
			result2.Clear();
			result2.TrimExcess();
		}

		result1.Clear();
		result1.TrimExcess();
	}

	[Fact]
	public static async Task BatchThenJoinChaosTest()
	{
		var random = new Random();

		for (int i = 0; i < 1000; i++)
		{
			await BatchThenJoin(random.Next(1000) + 1, random.Next(1000) + 1);
		}

		for (int i = 0; i < 10000; i++)
		{
			await BatchThenJoin(random.Next(500) + 1, random.Next(5000) + 1);
		}
	}

	[Theory]
	[InlineData(testSize1, 51)]
	[InlineData(testSize1, 5001)]
	[InlineData(testSize2, 51)]
	[InlineData(testSize2, 5001)]
	public static async Task BatchJoin(int testSize, int batchSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		var result = new List<int>(testSize);

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.Batch(batchSize, singleReader: true)
			.Join(singleReader: true)
			.ReadAll(i => result.Add(i));
		sw.Stop();

		Console.WriteLine("Channel.Batch({1}).Join(): {0}", sw.Elapsed, batchSize);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(range));
	}

#if NET5_0_OR_GREATER
	[Theory]
	[InlineData(11)]
	[InlineData(51)]
	[InlineData(101)]
	[InlineData(1001)]
	public static async Task JoinAsync(int repeat)
	{
		int testSize = repeat * repeat;
		IEnumerable<IAsyncEnumerable<int>> range = Enumerable.Repeat(Samples(), repeat);
		var result = new List<int>(testSize);

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.Join(singleReader: true)
			.ReadAll(i => result.Add(i));
		sw.Stop();

		Console.WriteLine("Channel<IAsyncEnumerable>.Join(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(testSize, result.Count);
		Assert.True(result.SequenceEqual(Enumerable.Repeat(Enumerable.Range(0, repeat), repeat).SelectMany(i => i)));

		async IAsyncEnumerable<int> Samples()
		{
			for (int i = 0; i < repeat; i++)
			{
				var x = new ValueTask<int>(i);
				yield return await x;
			}
		}
	}
#endif

	[Theory]
	[InlineData(testSize1)]
	[InlineData(testSize2)]
	public static async Task Filter(int testSize)
	{
		IEnumerable<int> range = Enumerable.Range(0, testSize);
		int count = testSize / 2;
		var result = new List<int>(count);

		var sw = Stopwatch.StartNew();
		long total = await range
			.ToChannel(singleReader: true)
			.Filter(i => i % 2 == 1)
			.ReadAll(i => result.Add(i));
		sw.Stop();

		Console.WriteLine("Channel.Filter(): {0}", sw.Elapsed);
		Console.WriteLine();

		Assert.Equal(count, result.Count);
		Assert.True(result.SequenceEqual(range.Where(i => i % 2 == 1)));
	}
}
