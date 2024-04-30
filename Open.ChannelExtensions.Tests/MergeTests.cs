namespace Open.ChannelExtensions.Tests;

public static class MergeTests
{
	[Fact()]
	public static async Task BasicMergeTest()
	{
		// Arrange
		const int total = 3000000;
		const int bound = 100;

		// 3 channels
		var c1 = Channel.CreateBounded<int>(bound);
		var c2 = Channel.CreateBounded<int>(bound);
		var c3 = Channel.CreateBounded<int>(bound);
		var writers = new[] { c1.Writer, c2.Writer, c3.Writer };

		// 3 readers
		var merging = new[] { c1.Reader, c2.Reader, c3.Reader }.Merge().ToListAsync(total);

		// Act
		await Parallel.ForAsync(0, total,
			(i, token) => writers[i % 3].WriteAsync(i, token));

		foreach (var writer in writers)
			writer.Complete();

		var merged = await merging;
		merged.Sort();

		// Assert
		Assert.Equal(total, merged.Count);
		Assert.True(Enumerable.Range(0, total).SequenceEqual(merged));
	}

	[Fact()]
	public static async Task ExceptionPropagationTest()
	{
		// Arrange
		const int total = 3000000;
		const int bound = 100;

		// 3 channels
		var c1 = Channel.CreateBounded<int>(bound);
		var c2 = Channel.CreateBounded<int>(bound);
		var c3 = Channel.CreateBounded<int>(bound);
		var writers = new[] { c1.Writer, c2.Writer, c3.Writer };

		// 3 readers
		var merging = new[] { c1.Reader, c2.Reader, c3.Reader }.Merge();
		var list = merging.ToListAsync(total);

		// Act
		await Assert.ThrowsAsync<ChannelClosedException>(() => Parallel.ForAsync(0, total,
			async (i, token) =>
			{
				var w = writers[i % 3];
				if (i == total / 2)
					w.Complete(new Exception("Test"));
				else
					await w.WriteAsync(i, token).ConfigureAwait(false);
			}));

		// Assert
		await Assert.ThrowsAsync<Exception>(list.AsTask);
		await Assert.ThrowsAsync<Exception>(() => merging.Completion);
	}
}