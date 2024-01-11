namespace Open.ChannelExtensions.Tests;
public static class SourceTests
{
	[Fact]
	public static async Task ToChannelCancelledAfterwriteStarts()
	{
		var cts = new CancellationTokenSource();
		var reader = Enumerable.Range(0, 10_000).ToChannel(10, true, cts.Token);
		cts.Cancel();

		try
		{
			await reader.ReadAll(_ => { }, cts.Token);
		}
		catch (OperationCanceledException)
		{ }

		await reader.ReadAll(_ => { });
		await Assert.ThrowsAsync<TaskCanceledException>(() => reader.Completion);
	}

	[Fact]
	public static async Task ToChannelCancelledBeforeWriteStarts()
	{
		var cts = new CancellationTokenSource();
		cts.Cancel();
		var reader = Enumerable.Range(0, 10_000).ToChannel(10, true, cts.Token);

		var count = await reader.ReadAll(_ => { });
		Assert.Equal(0, count);
		await Assert.ThrowsAsync<TaskCanceledException>(() => reader.Completion);
	}
}
