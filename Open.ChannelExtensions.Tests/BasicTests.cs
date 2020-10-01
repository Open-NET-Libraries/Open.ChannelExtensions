using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests
{
	public static class BasicTests
	{
		const int TestSize1 = 10000001;
		const int TestSize2 = 30000001;

		[Theory]
		[InlineData(TestSize1)]
		[InlineData(TestSize2)]
		public static async Task DeferredWriteRead(int testSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result = new List<int>(testSize);

			var sw = Stopwatch.StartNew();
			var reader = range
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
		[InlineData(TestSize1)]
		[InlineData(TestSize2)]
		public static async Task ReadAll(int testSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result = new List<int>(testSize);

			var sw = Stopwatch.StartNew();
			var total = await range
				.ToChannel(singleReader: true)
				.ReadAll(i => result.Add(i));
			sw.Stop();

			Console.WriteLine("ReadAll(): {0}", sw.Elapsed);
			Console.WriteLine();

			Assert.Equal(testSize, result.Count);
			Assert.True(result.SequenceEqual(range));
			result.Clear();
		}

		[Theory]
		[InlineData(TestSize1)]
		[InlineData(TestSize2)]
		public static async Task ReadAllAsync(int testSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result = new List<int>(testSize);

			var sw = Stopwatch.StartNew();
			var total = await range
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
		[InlineData(TestSize1)]
		[InlineData(TestSize2)]
		public static async Task PipeToBounded(int testSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result = new List<int>(testSize);

			var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(100)
			{
				SingleReader = true,
				SingleWriter = true,
				AllowSynchronousContinuations = true
			});

			var sw = Stopwatch.StartNew();
			var total = await range
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
		[InlineData(TestSize1)]
		[InlineData(TestSize2)]
		public static async Task PipeToUnbound(int testSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result = new List<int>(testSize);

			var channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions()
			{
				SingleReader = true,
				SingleWriter = true,
				AllowSynchronousContinuations = true
			});

			var sw = Stopwatch.StartNew();
			var total = await range
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
			var range = Enumerable.Range(0, testSize).ToList();
			var expectedBatchCount = (testSize / batchSize) + (testSize % batchSize == 0 ? 0 : 1);
			var result1 = new List<List<int>>(expectedBatchCount);

			var total = await range
				.ToChannel(singleReader: true)
				.Batch(batchSize, singleReader: true)
				.ReadAll(result1.Add);

			Assert.Equal(expectedBatchCount, result1.Count);

			var r = result1.SelectMany(e => e).ToList();
			Assert.Equal(testSize, r.Count);
			Assert.True(r.SequenceEqual(range));
		}

		[Theory]
		[InlineData(TestSize1, 51)]
		[InlineData(TestSize1, 5001)]
		[InlineData(TestSize2, 51)]
		[InlineData(TestSize2, 5001)]
		[InlineData(100, 100)]
		public static async Task BatchThenJoin(int testSize, int batchSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result1 = new List<List<int>>(testSize / batchSize + 1);

			{
				var sw = Stopwatch.StartNew();
				var total = await range
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
				var total = await result1
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

			for (var i = 0; i < 1000; i++)
			{
				await BatchThenJoin(random.Next(1000) + 1, random.Next(1000) + 1);
			}

			for (var i = 0; i < 10000; i++)
			{
				await BatchThenJoin(random.Next(500) + 1, random.Next(5000) + 1);
			}
		}

		[Theory]
		[InlineData(TestSize1, 51)]
		[InlineData(TestSize1, 5001)]
		[InlineData(TestSize2, 51)]
		[InlineData(TestSize2, 5001)]
		public static async Task BatchJoin(int testSize, int batchSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var result = new List<int>(testSize);

			var sw = Stopwatch.StartNew();
			var total = await range
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

		[Theory]
		[InlineData(11)]
		[InlineData(51)]
		[InlineData(101)]
		[InlineData(1001)]
		public static async Task JoinAsync(int repeat)
		{
			var testSize = repeat * repeat;
			var range = Enumerable.Repeat(Samples(), repeat);
			var result = new List<int>(testSize);

			var sw = Stopwatch.StartNew();
			var total = await range
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
				for (var i = 0; i < repeat; i++)
				{
					var x = new ValueTask<int>(i);
					yield return await x;
				}
			}
		}

		[Theory]
		[InlineData(TestSize1)]
		[InlineData(TestSize2)]
		public static async Task Filter(int testSize)
		{
			var range = Enumerable.Range(0, testSize).ToList();
			var count = testSize / 2;
			var result = new List<int>(count);

			var sw = Stopwatch.StartNew();
			var total = await range
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
}
