namespace Open.ChannelExtensions.Tests;
public static class PipelineExceptionTests
{
	const int BatchSize = 20;
	const int Elements = 100;

	[Theory]
	[InlineData(BatchSize - 1)]
	[InlineData(BatchSize)]
	[InlineData(BatchSize + 1)]
	[InlineData(-1)]
	[InlineData(Elements)]
	public static async Task Regular(int elementToThrow)
	{
		var channel = Channel.CreateBounded<int>(10000);

		for (int i = 0; i < Elements; i++)
		{
			await channel.Writer.WriteAsync(i);
		}

		var task = channel.Reader
			.Pipe(1, element => elementToThrow == -1 || element == elementToThrow ? throw new Exception() : element)
			.Pipe(2, evt => evt * 2)
			//.Batch(20)
			.PipeAsync(1, evt => new ValueTask<int>(evt))
			.ReadAll(_ => { });

		if (elementToThrow == Elements)
			channel.Writer.Complete(new Exception());
		else
			channel.Writer.Complete();

		await Assert.ThrowsAsync<AggregateException>(async () => await task);
		await Assert.ThrowsAsync<ChannelClosedException>(async () => await channel.CompleteAsync());
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(BatchSize - 1)]
	[InlineData(BatchSize)]
	[InlineData(BatchSize + 1)]
	[InlineData(-1)]
	public static async Task Batched(int elementToThrow)
	{
		var channel = Channel.CreateBounded<int>(10000);

		for (int i = 0; i < Elements; i++)
		{
			await channel.Writer.WriteAsync(i);
		}

		var task = channel.Reader
			.Pipe(1, element => elementToThrow == -1 || element == elementToThrow ? throw new Exception() : element)
			.Pipe(2, evt => evt * 2)
			.Batch(20)
			.PipeAsync(1, evt => new ValueTask<List<int>>(evt))
			.ReadAll(_ => { });

		if (elementToThrow == Elements)
			channel.Writer.Complete(new Exception());
		else
			channel.Writer.Complete();

		await Assert.ThrowsAsync<AggregateException>(async () => await task);
		await Assert.ThrowsAsync<ChannelClosedException>(async () => await channel.CompleteAsync());
	}
}