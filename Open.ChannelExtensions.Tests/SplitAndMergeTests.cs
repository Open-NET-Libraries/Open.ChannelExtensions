namespace Open.ChannelExtensions.Tests;

public class SplitAndMergeTests
{
	[Fact]
	public async Task SplitAndMergeTest()
	{
		var cts = new CancellationTokenSource();

		List<bool> range = new List<bool>();
		for (int i = 0; i < 10_000; i++)
		{
			range.Add(i % 2 == 0);
		}

		int countTest1 = 0;
		int countTest2 = 0;

		var result = await range.ToChannel(10, true, cts.Token)
			.Split(10, data => data, out var unmatched1, cts.Token)
			.Pipe(5, x => { Interlocked.Increment(ref countTest1); return x; })
			.Merge(unmatched1, cts.Token)
			.Split(10, data => !data, out var unmatched2, cts.Token)
			.Pipe(5, x => { Interlocked.Increment(ref countTest2); return x; })
			.Merge(unmatched2, cts.Token)
			.ReadAll(cts.Token, x => { });

		Assert.Equal(5_000, countTest1);
		Assert.Equal(5_000, countTest2);
		Assert.Equal(10_000, result);
	}
}
