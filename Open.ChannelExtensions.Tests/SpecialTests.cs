using System.Collections.Concurrent;

namespace Open.ChannelExtensions.Tests;

public class SpecialTests
{
	[Fact]
	public async Task PossibleSourceLoadingIssue()
	{
		const int expectedCount = 10000000;
		int count = 0;

		var queue = new BlockingCollection<int>();
		var processingTask = Task.Run(() => StartProcessingTask2(queue.GetConsumingEnumerable()));

		Console.WriteLine("Starting to fill queue.");
		for (int i = 0; i < expectedCount; i++)
			queue.Add(i);

		Console.WriteLine("Queue fill complete.");

		queue.CompleteAdding();

		await processingTask;

		Assert.Equal(expectedCount, count);

		Task StartProcessingTask2(IEnumerable<int> source)
			=> Channel.CreateUnbounded<int>()
				.Source(source, true)
				.ReadAll(IncrementCount2)
				.AsTask();

		void IncrementCount2(int _)
			=> Interlocked.Increment(ref count);
	}
}
