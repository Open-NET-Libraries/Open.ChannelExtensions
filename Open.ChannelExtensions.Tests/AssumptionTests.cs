namespace Open.ChannelExtensions.Tests;

public static class AssumptionTests
{
	[Fact]
	public static async Task WaitCancellation()
	{
		var channel = Channel.CreateUnbounded<int>();
		using (var tokenSource = new CancellationTokenSource())
		{
			CancellationToken token = tokenSource.Token;

			var t = channel.Reader.WaitToReadAsync(token);
			tokenSource.Cancel();

			// NOTE: a cancelled WaitToReadAsync will throw. 
			await Assert.ThrowsAsync<OperationCanceledException>(async () => await t);
		}

		using (var tokenSource = new CancellationTokenSource())
		{
			CancellationToken token = tokenSource.Token;

			var t1 = channel.Reader.WaitToReadAsync(token);
			var t2 = channel.Reader.WaitToReadAsync();
			tokenSource.Cancel();

			// NOTE: a cancelled WhenAny will not throw!
			var result = await Task.WhenAny(t1.AsTask(), t2.AsTask());
			Assert.True(result.IsCanceled);
		}
	}
}
