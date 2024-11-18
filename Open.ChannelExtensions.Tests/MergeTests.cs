namespace Open.ChannelExtensions.Tests;

public static class MergeTests
{
	const int Total = 3000000;
	const int Bounds = 100;
	const int Count = 5;

	private static Channel<int>[] GetChannels()
		=> Enumerable.Range(0, Count).Select(_ => Channel.CreateBounded<int>(Bounds)).ToArray();

	private static async Task BasicMergeTestCore(ChannelWriter<int>[] writers, ValueTask<List<int>> merging)
	{
		// Act
		await Parallel.ForAsync(0, Total,
			(i, token) => writers[i % Count].WriteAsync(i, token));

		foreach (ChannelWriter<int> writer in writers)
			writer.Complete();

		List<int> merged = await merging;
		merged.Sort();

		// Assert
		Assert.Equal(Total, merged.Count);
		Assert.True(Enumerable.Range(0, Total).SequenceEqual(merged));
	}

	[Fact()]
	public static async Task BasicMergeTest()
	{
		// 3 channels
		Channel<int>[] c = GetChannels();

		// 3 writers
		ChannelWriter<int>[] writers = c.Select(e => e.Writer).ToArray();

		// 3 readers
		ValueTask<List<int>> merging = c.Select(e => e.Reader).Merge().ToListAsync(Total);

		await BasicMergeTestCore(writers, merging);
	}

	[Fact()]
	public static async Task MergeChainTest()
	{
		// 3 channels
		Channel<int>[] c = GetChannels();

		// 3 writers
		ChannelWriter<int>[] writers = c.Select(e => e.Writer).ToArray();

		ChannelReader<int> reader = c[0].Reader;
		for (int i = 1; i < c.Length; i++)
			reader = reader.Merge(c[i].Reader);

		// 3 readers
		ValueTask<List<int>> merging = reader.ToListAsync(Total);

		await BasicMergeTestCore(writers, merging);
	}

	[Fact()]
	public static async Task MergeChainTest2()
	{
		// 3 channels
		Channel<int>[] c = GetChannels();

		// 3 writers
		ChannelWriter<int>[] writers = c.Select(e => e.Writer).ToArray();

		MergingChannelReader<int> reader = c[0].Reader.Merge(c[1].Reader, c.Skip(2).Select(e => e.Reader).ToArray());
		for (int i = 1; i < c.Length; i++)
			reader = reader.Merge(c[i].Reader);

		// 3 readers
		ValueTask<List<int>> merging = reader.ToListAsync(Total);

		await BasicMergeTestCore(writers, merging);
	}

	[Fact()]
	public static async Task ExceptionPropagationTest()
	{
		// 3 channels
		Channel<int>[] c = GetChannels();

		// 3 writers
		ChannelWriter<int>[] writers = c.Select(e => e.Writer).ToArray();

		// 3 readers
		MergingChannelReader<int> merging = c.Select(e => e.Reader).Merge();
		ValueTask<List<int>> list = merging.ToListAsync(Total);

		// Act
		await Assert.ThrowsAsync<ChannelClosedException>(() => Parallel.ForAsync(0, Total,
			async (i, token) =>
			{
				ChannelWriter<int> w = writers[i % 3];
				if (i == Total / 2)
					w.Complete(new Exception("Test"));
				else
					await w.WriteAsync(i, token).ConfigureAwait(false);
			}));

		// Assert
		await Assert.ThrowsAsync<Exception>(list.AsTask);
		await Assert.ThrowsAsync<Exception>(() => merging.Completion);
	}
}