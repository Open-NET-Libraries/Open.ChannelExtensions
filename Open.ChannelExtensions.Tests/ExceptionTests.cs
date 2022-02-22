using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests;

public static class ExceptionTests
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Roslynator", "RCS1194:Implement exception constructors.")]
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
}
