namespace Open.ChannelExtensions.Tests;
public static class PropagationTests
{
	[Fact]
	public static async Task PropagateCompleteionTest()
	{
		var inputChannel = Channel.CreateBounded<int>(100);
		var outputChannel = Channel.CreateBounded<int>(100);

		inputChannel.PropagateCompletion(outputChannel);
		inputChannel.Writer.Complete();
		await outputChannel.Reader.Completion;
	}
}
