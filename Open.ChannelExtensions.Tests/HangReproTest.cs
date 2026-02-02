using System.Runtime.CompilerServices;

namespace Open.ChannelExtensions.Tests;

public static class HangReproTest
{
	/// <summary>
	/// Simple test to verify batch with timeout completes correctly.
	/// </summary>
	[Fact]
	public static async Task SimpleBatchWithTimeoutCompletes()
	{
		// Source yields 75 items, batch size 50 = 1 full batch + 25 remaining
		// Timeout should flush the remaining 25 items
		var items = Enumerable.Range(0, 75);
		var batches = new List<int>();
		
		await items
			.ToChannel(singleReader: true)
			.Batch(50)
			.WithTimeout(TimeSpan.FromMilliseconds(100))
			.ReadAllAsync(batch =>
			{
				batches.Add(batch.Count);
				return default;
			})
			.AsTask()
			.WaitAsync(TimeSpan.FromSeconds(5));

		Assert.Equal(2, batches.Count);
		Assert.Equal(50, batches[0]);
		Assert.Equal(25, batches[1]);
	}

	/// <summary>
	/// Stress test for concurrent batch readers with timeout.
	/// Runs 10 concurrent readers × 1000 iterations = 10,000 pipeline executions
	/// with randomized batch sizes, source sizes, and delays.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Uses bounded channel (capacity: 10000) instead of unbounded to avoid
	/// a known .NET runtime bug: https://github.com/dotnet/runtime/issues/123544
	/// </para>
	/// <para>
	/// The <c>.WithTimeout()</c> is required when batching to ensure partial batches
	/// are flushed when the source completes. Without it, if the remaining items
	/// don't fill a complete batch, the reader will wait indefinitely.
	/// </para>
	/// </remarks>
	[Fact]
	public static async Task MultipleBatchReadersDoNotHang()
	{
		const int readerCount = 10;
		const int iterations = 1000;

		var completedIterations = new int[readerCount];
		var lastProcessedBatchTs = new long[readerCount];
		var tasks = Enumerable.Range(0, readerCount)
			.Select(x => Task.Run(async () =>
			{
				for (int i = 0; i < iterations; i++)
				{
					try
					{
						await GetSource()
							// Use bounded channel to avoid .NET runtime bug with unbounded channels.
							// See: https://github.com/dotnet/runtime/issues/123544
							.ToChannel(capacity: 10_000_000, singleReader: true)
							.Batch(Random.Shared.Next(25, 50))
							// WithTimeout is required to flush partial batches when source completes.
							.WithTimeout(TimeSpan.FromMilliseconds(100))
							.ReadAllAsync(async _ =>
							{
								await Task.Delay(Random.Shared.Next(1, 10));
								lastProcessedBatchTs[x] = Stopwatch.GetTimestamp();
							})
							.AsTask()
							.WaitAsync(TimeSpan.FromSeconds(10));
					}
					catch (TimeoutException)
					{
						break;
					}
					
					completedIterations[x] += 1;
				}
			})).ToArray();

		await Task.WhenAll(tasks);

		Assert.All(completedIterations, (count, index) =>
		{
			var elapsedSinceLastProcessedBatch = Stopwatch.GetElapsedTime(lastProcessedBatchTs[index]);
			
			Assert.True(count == iterations, 
				$"Reader completed {count}/{iterations} iterations. " +
				$"Time since last processed batch: {elapsedSinceLastProcessedBatch}");
		});
	}

	private static async IAsyncEnumerable<int> GetSource([EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var value in Enumerable.Range(0, Random.Shared.Next(50, 100)))
		{
			if (cancellationToken.IsCancellationRequested)
				yield break;

			yield return value;

			if (value % Random.Shared.Next(15, 25) == 0)
				await Task.Delay(Random.Shared.Next(1, 10), cancellationToken);
		}
	}
}
