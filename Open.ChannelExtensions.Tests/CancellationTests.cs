using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests;

public static class CancellationTests
{

	[Fact]
	public static async Task OperationCancellationPropagation()
	{
		int count = 0;
		System.Collections.Generic.IEnumerable<int> range = Enumerable.Range(0, 1000);
		using var tokenSource = new CancellationTokenSource();
		CancellationToken token = tokenSource.Token;
		try
		{
			await range
				.ToChannel()
				.ReadAll(i =>
				{
					if (i == 500)
					{
						Interlocked.Increment(ref count);
						tokenSource.Cancel();
					}
					token.ThrowIfCancellationRequested();
				});
		}
		catch (Exception ex)
		{
			Assert.IsType<OperationCanceledException>(ex);
		}

		Assert.Equal(1, count);
	}

	[Fact]
	public static async Task TaskCancellationPropagationConcurrent()
	{
		const int testSize = 1000000;
		int total = 0;
		int count = 0;
		System.Collections.Generic.IEnumerable<int> range = Enumerable.Range(0, testSize);
		using var tokenSource = new CancellationTokenSource();
		CancellationToken token = tokenSource.Token;
		try
		{
			await range
				.ToChannel()
				.ReadAllConcurrently(8, i =>
				{
					Interlocked.Increment(ref total);
					if (i == 500)
					{
						Interlocked.Increment(ref count);
						tokenSource.Cancel();
					}
					token.ThrowIfCancellationRequested();
				});
		}
		catch (Exception ex)
		{
			Assert.IsType<OperationCanceledException>(ex);
		}

		Assert.Equal(1, count);
		Assert.NotEqual(testSize, total);
	}


	[Fact]
	public static async Task CancellationPropagationConcurrent()
	{
		const int testSize = 1000000;
		int total = 0;
		int count = 0;
		System.Collections.Generic.IEnumerable<int> range = Enumerable.Range(0, testSize);
		using var tokenSource = new CancellationTokenSource();
		CancellationToken token = tokenSource.Token;
		try
		{
			await range
				.ToChannel()
				.ReadAllConcurrently(8, token, i =>
				{
					Interlocked.Increment(ref total);
					if (i == 500)
					{
						Interlocked.Increment(ref count);
						tokenSource.Cancel();
					}
				});
		}
		catch (Exception ex)
		{
			Assert.IsType<OperationCanceledException>(ex);
		}

		Assert.Equal(1, count);
		Assert.NotEqual(testSize, total);

	}

}
