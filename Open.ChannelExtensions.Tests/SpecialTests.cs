using System.Collections.Concurrent;

namespace Open.ChannelExtensions.Tests;

public class SpecialTests
{
	[Fact]
	public void PossibleSourceLoadingIssue()
	{
		const int expectedCount = 10000000;
		int count_ = 0;

		var queue = new BlockingCollection<int>();
		var processingTask = StartProcessingTask2(queue.GetConsumingEnumerable());

		Console.WriteLine("Starting to fill queue.");
		for (var i = 0; i < expectedCount; i++)
			queue.Add(i);

		Console.WriteLine("Queue fill complete.");

		queue.CompleteAdding();

#pragma warning disable xUnit1031 // Do not use blocking task operations in test method
		processingTask.Wait();
#pragma warning restore xUnit1031 // Do not use blocking task operations in test method

		Assert.Equal(expectedCount, count_);

		Task StartProcessingTask2(IEnumerable<int> source)
			=> Channel.CreateUnbounded<int>()
				.Source(source, true)
				.ReadAll(IncrementCount2)
				.AsTask();

		void IncrementCount2(int _)
			=> Interlocked.Increment(ref count_);
	}
}
