namespace Open.ChannelExtensions.Tests;

public static class ExceptionTests
{
	[SuppressMessage("Roslynator", "RCS1194:Implement exception constructors.")]
	class TestException : Exception { }

	[Fact]
	public static async Task ExceptionPropagation()
	{
		int count = 0;
		System.Collections.Generic.IEnumerable<int> range = Enumerable.Range(0, 1000);
		try
		{
			await range
				.ToChannel()
				.ReadAll(i =>
				{
					if (i == 500)
					{
						Interlocked.Increment(ref count);
						throw new TestException();
					}
				});
		}
		catch (Exception ex)
		{
			Assert.IsType<TestException>(ex);
		}

		Assert.Equal(1, count);
	}

	[Fact]
	public static async Task TransformExceptionPropagation()
	{
		int count = 0;
		System.Collections.Generic.IEnumerable<int> range = Enumerable.Range(0, 1000);
		try
		{
			await range
				.ToChannel()
				.Transform(i =>
				{
					if (i == 500)
					{
						Interlocked.Increment(ref count);
						throw new TestException();
					}

					return i.ToString();
				})
				.ReadAll(_ => { });
		}
		catch (Exception ex)
		{
			Assert.IsType<TestException>(ex);
		}

		Assert.Equal(1, count);
	}

	[Fact]
	public static async Task ExceptionPropagationConcurrent()
	{
		const int testSize = 100000000;
		int total = 0;
		int count = 0;
		System.Collections.Generic.IEnumerable<int> range = Enumerable.Range(0, testSize);
		await Assert.ThrowsAsync<AggregateException>(async () =>
		{
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
							throw new TestException();
						}
					});
			}
			catch (Exception ex)
			{
				Assert.IsType<AggregateException>(ex);
				Assert.IsType<TestException>(((AggregateException)ex).InnerException);
				throw;
			}
		});

		Assert.Equal(1, count);
		Assert.NotEqual(testSize, total);
	}

	[Fact]
	[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "Needs to happen synchronously")]
	public static void ChannelClosed()
	{
		var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1000)
		{
			SingleWriter = true,
			SingleReader = true,
		});
		channel.Writer.Complete();
		// Needs to throw immediately if true.
		Assert.Throws<ChannelClosedException>(() => channel.Writer.WaitToWriteAndThrowIfClosedAsync());
	}

	[Fact]
	public static async Task WriteAllThrowIfClosed()
	{
		var channel = Channel.CreateBounded<int>(new BoundedChannelOptions(1000)
		{
			SingleWriter = true,
			SingleReader = true,
		});
		var reader = channel.Source(Enumerable.Range(0, 10_000));
		await reader.ReadAll(_ => { });

		await Assert.ThrowsAsync<ChannelClosedException>(async () =>
		{
			channel.Source(Enumerable.Range(0, 10_000), out var completion);
			await completion;
		});
	}
}
