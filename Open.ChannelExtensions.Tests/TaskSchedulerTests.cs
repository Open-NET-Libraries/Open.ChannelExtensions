namespace Open.ChannelExtensions.Tests;

public static class TaskSchedulerTests
{
	[Fact]
	public static async Task ReadAllConcurrentlyAsync_UsesCurrentScheduler()
	{
		// Arrange
		var channel = Channel.CreateUnbounded<int>();
		var testScheduler = new TestTaskScheduler();
		const int maxConcurrency = 3;
		const int itemsToProcess = 10000;

		// Populate the channel with test data
		for (int i = 0; i < itemsToProcess; i++)
		{
			await channel.Writer.WriteAsync(i);
		}
		channel.Writer.Complete();

		// Act
		var itemsProcessed = await channel.Reader
			.ReadAllConcurrentlyAsync(
				maxConcurrency,
				testScheduler,
				_ =>
				{
					Assert.True(testScheduler == TaskScheduler.Current, "The custom scheduler was not used.");
					return ValueTask.CompletedTask;
				});

		// Assert
		Assert.Equal(itemsToProcess, itemsProcessed);
	}

	private class TestTaskScheduler : TaskScheduler
	{
		protected override IEnumerable<Task> GetScheduledTasks() => null;

		protected override void QueueTask(Task task)
			=> Task.Factory.StartNew(() => TryExecuteTask(task),
				CancellationToken.None,
				TaskCreationOptions.None,
				TaskScheduler.Default);

		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
			=> TryExecuteTask(task);
	}
}
