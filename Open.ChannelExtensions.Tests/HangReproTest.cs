using System.Text.Json;

namespace Open.ChannelExtensions.Tests;

public static class HangReproTest
{
	/// <summary>
	/// Reproduction test for the hanging issue described in GitHub.
	/// This test should fail if the bug exists (by timing out).
	/// </summary>
	[Fact]
	public static async Task MultipleBatchReadersDoNotHang()
	{
		var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30)); // 30 second timeout
		var token = cts.Token;

		var counts = new int[5]; // Using 5 tasks instead of 10 for faster testing
		var tasks = Enumerable.Range(0, 5)
			.Select(x => Task.Run(async () =>
			{
				for (int i = 0; i < 50; i++) // Run 50 iterations instead of infinite
				{
					if (token.IsCancellationRequested)
						break;

					await GetSource()
						.ToChannel(singleReader: true, cancellationToken: token)
						.Batch(Random.Shared.Next(15, 30), singleReader: true)
						.ReadAllAsync(async batch =>
						{
							await Task.Delay(Random.Shared.Next(2, 15), token);
						}, token);

					counts[x] += 1;
				}
			}, token)).ToArray();

		// Wait for all tasks to complete or timeout
		await Task.WhenAll(tasks);

		// Verify all tasks completed at least some iterations
		Assert.All(counts, count => Assert.True(count >= 40, $"Count was {count}, expected at least 40"));
	}

	private static async IAsyncEnumerable<int> GetSource([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		foreach (var value in Enumerable.Range(0, Random.Shared.Next(80, 120)))
		{
			if (cancellationToken.IsCancellationRequested)
				yield break;

			yield return value;

			if (value % Random.Shared.Next(15, 25) == 0)
				await Task.Delay(Random.Shared.Next(2, 15), cancellationToken);
		}
	}
}
