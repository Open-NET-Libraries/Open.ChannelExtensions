using System.Buffers;
using System.Runtime.CompilerServices;

namespace Open.ChannelExtensions.Tests;

public static class BatchTestsOfMemoryOwner
{
	[Fact]
	public static async Task SimpleBatch2Test()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		_ = Task.Run(async () =>
		{
			await Task.Delay(1000);
			c.Writer.TryWrite(1);
			c.Writer.TryWrite(2);
			c.Writer.TryWrite(3);
			c.Writer.TryWrite(4);
			c.Writer.TryWrite(5);
			c.Writer.TryWrite(6);
			c.Writer.Complete();
		});

		await c.Reader
			.BatchAsMemory(2)
			.ReadAllAsync(async (owner, i) =>
			{
				using IMemoryOwner<int> _ = owner;
				Memory<int> batch = owner.Memory;
				switch (i)
				{
					case 0:
						Assert.Equal(1, batch.Span[0]);
						Assert.Equal(2, batch.Span[1]);
						break;

					case 1:
						Assert.Equal(3, batch.Span[0]);
						Assert.Equal(4, batch.Span[1]);
						break;

					case 2:
						Assert.Equal(5, batch.Span[0]);
						Assert.Equal(6, batch.Span[1]);
						break;

					default:
						throw new Exception("Shouldn't arrive here.");
				}
				await Task.Delay(500);
			});
	}

	[Fact]
	public static async Task Batch2TestWithDelay()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		_ = Task.Run(async () =>
		{
			await Task.Delay(1000);
			c.Writer.TryWrite(1);
			c.Writer.TryWrite(2);
			c.Writer.TryWrite(3);
			c.Writer.TryWrite(4);
			c.Writer.TryWrite(5);
			c.Writer.TryWrite(6);
		});

		using var tokenSource = new CancellationTokenSource();
		CancellationToken token = tokenSource.Token;
		await c.Reader
			.BatchAsMemory(2)
			.ReadAllAsync(async (owner, i) =>
			{
				using IMemoryOwner<int> __ = owner;
				Memory<int> batch = owner.Memory;

				switch (i)
				{
					case 0:
						Assert.Equal(1, batch.Span[0]);
						Assert.Equal(2, batch.Span[1]);
						break;
					case 1:
						Assert.Equal(3, batch.Span[0]);
						Assert.Equal(4, batch.Span[1]);
						_ = Task.Run(async () =>
						{
							await Task.Delay(60000, token);
							if (!token.IsCancellationRequested) c.Writer.TryComplete(new Exception("Should have completed successfully."));
						});
						break;
					case 2:
						Assert.Equal(5, batch.Span[0]);
						Assert.Equal(6, batch.Span[1]);
						tokenSource.Cancel();
						c.Writer.Complete();
						break;
					default:
						throw new Exception("Shouldn't arrive here.");
				}
				await Task.Delay(500);
			});
	}

	[Fact]
	public static async Task ForceBatchTest()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		_ = Task.Run(async () =>
		{
			await Task.Delay(1000);
			c.Writer.TryWrite(1);
			c.Writer.TryWrite(2);
			c.Writer.TryWrite(3);
			c.Writer.TryWrite(4);
			c.Writer.TryWrite(5);
		});

		using var tokenSource = new CancellationTokenSource(10000);
		BatchingChannelReader<int, IMemoryOwner<int>> reader = c.Reader.BatchAsMemory(3);
		Assert.Equal(2, await reader.ReadAllAsync(tokenSource.Token, async (owner, i) =>
		{
			using IMemoryOwner<int> _ = owner;
			Memory<int> batch = owner.Memory;

			switch (i)
			{
				case 0:
					Assert.Equal(1, batch.Span[0]);
					Assert.Equal(2, batch.Span[1]);
					Assert.Equal(3, batch.Span[2]);
					await Task.Delay(500);
					reader.ForceBatch();
					break;
				case 1:
					Assert.Equal(2, batch.Length);
					Assert.Equal(4, batch.Span[0]);
					Assert.Equal(5, batch.Span[1]);
					c.Writer.Complete();
					break;
				default:
					throw new Exception("Shouldn't arrive here.");
			}
			await Task.Delay(500);
		}));
	}

	[Fact]
	public static async Task ForceBatchTest2()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		BatchingChannelReader<int, IMemoryOwner<int>> reader = c.Reader.BatchAsMemory(3);
		_ = Task.Run(async () =>
		{
			await Task.Delay(1000);
			c.Writer.TryWrite(1);
			c.Writer.TryWrite(2);
			c.Writer.TryWrite(3);
			c.Writer.TryWrite(4);
			c.Writer.TryWrite(5);
			Debug.WriteLine("Writing Complete.");

			await Task.Delay(1000);
			Assert.True(reader.ForceBatch());
			Debug.WriteLine("Batch Forced.");
		});

		using var tokenSource = new CancellationTokenSource(6000);
		Assert.Equal(2, await reader.ReadAllAsync(tokenSource.Token, async (owner, i) =>
		{
			using IMemoryOwner<int> _ = owner;
			Memory<int> batch = owner.Memory;

			switch (i)
			{
				case 0:
					Assert.Equal(1, batch.Span[0]);
					Assert.Equal(2, batch.Span[1]);
					Assert.Equal(3, batch.Span[2]);
					Debug.WriteLine("First batch received.");
					break;
				case 1:
					Assert.Equal(2, batch.Length);
					Assert.Equal(4, batch.Span[0]);
					Assert.Equal(5, batch.Span[1]);
					Debug.WriteLine("Second batch received.");
					c.Writer.Complete();
					break;
				default:
					throw new Exception("Shouldn't arrive here.");
			}
			await Task.Delay(500);
		}));
	}

	[Fact]
	public static async Task TimeoutTest0()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		BatchingChannelReader<int, IMemoryOwner<int>> reader = c.Reader.BatchAsMemory(10).WithTimeout(500);
		bool complete = false;
		_ = Task.Run(async () =>
		{
			for (int i = 0; i < 5; i++)
			{
				c.Writer.TryWrite(i);
			}

			await Task.Delay(1000);
			complete = true;
			c.Writer.Complete();
		});

		using var tokenSource = new CancellationTokenSource(6000);
		Assert.Equal(1, await reader.ReadAllAsync(tokenSource.Token, async (owner, i) =>
		{
			using IMemoryOwner<int> _ = owner;
			Memory<int> batch = owner.Memory;
			switch (i)
			{
				case 0:
					Assert.Equal(5, batch.Length);
					Assert.False(complete);
					break;

				default:
					throw new Exception("Shouldn't arrive here.");
			}
			await Task.Delay(100);
		}));
	}

	[Fact]
	public static async Task TimeoutTest1()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		BatchingChannelReader<int, IMemoryOwner<int>> reader = c.Reader.BatchAsMemory(10).WithTimeout(500);
		_ = Task.Run(async () =>
		{
			for (int i = 0; i < 15; i++)
			{
				c.Writer.TryWrite(i);
			}

			await Task.Delay(1000);

			for (int i = 0; i < 15; i++)
			{
				c.Writer.TryWrite(i);
			}
			c.Writer.Complete();
		});

		using var tokenSource = new CancellationTokenSource(6000);
		Assert.Equal(4, await reader.ReadAllAsync(tokenSource.Token, async (owner, i) =>
		{
			using IMemoryOwner<int> _ = owner;
			Memory<int> batch = owner.Memory;

			switch (i)
			{
				case 0:
				case 2:
					Assert.Equal(10, batch.Length);
					break;
				case 1:
				case 3:
					Assert.Equal(5, batch.Length);
					break;

				default:
					throw new Exception("Shouldn't arrive here.");
			}
			await Task.Delay(100);
		}));
	}

	[Fact]
	public static async Task BatchReadBehavior()
	{
		var c = Channel.CreateBounded<int>(new BoundedChannelOptions(20) { SingleReader = false, SingleWriter = false });
		BatchingChannelReader<int, IMemoryOwner<int>> reader = c.Reader.BatchAsMemory(10);

		var queue = new Queue<int>(Enumerable.Range(0, 100));
		int e;
		while (queue.TryDequeue(out e) && c.Writer.TryWrite(e))
			await Task.Yield();

		IMemoryOwner<int> batch = null;
		Assert.True(69 <= queue.Count);
		await reader.WaitToReadAsync();
		// At this point, a batch is prepared and there is room in the channel.
		await Dequeue();

		Assert.True(59 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		// At this point nothing is waiting so either a wait must occur or a read to trigger a new batch.

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.True(49 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.True(39 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.True(29 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.True(19 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.True(09 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		await Dequeue();

		Assert.Empty(queue);
		c.Writer.Complete();

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		batch.Dispose();

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Memory.Length);
		batch?.Dispose();

		Assert.False(reader.TryRead(out batch));
		batch?.Dispose();

		async ValueTask Dequeue()
		{
			batch?.Dispose();
			if (!c.Writer.TryWrite(e)) return;
			while (queue.TryDequeue(out e) && c.Writer.TryWrite(e))
				await Task.Yield();
		}
	}

	[Fact]
	public static async Task ReadBatchWithTimeoutEnumerableBakedIn()
	{
		var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
		_ = Task.Run(async () =>
		{
			//await Task.Delay(1000);
			c.Writer.TryWrite(1);
			c.Writer.TryWrite(2);

			c.Writer.TryWrite(3);
			await Task.Delay(600);

			c.Writer.TryWrite(4);
			c.Writer.TryWrite(5);
			Debug.WriteLine("Writing Complete.");
			c.Writer.Complete();
		});
		int i = 0;
		await foreach (IMemoryOwner<int> owner in c.Reader.ReadBatchMemoryEnumerableAsyncBakedIn(2, TimeSpan.FromMilliseconds(500), CancellationToken.None))
		{
			using IMemoryOwner<int> _ = owner;
			Memory<int> batch = owner.Memory;

			switch (i)
			{
				case 0:
					Assert.Equal(1, batch.Span[0]);
					Assert.Equal(2, batch.Span[1]);
					break;
				case 1:
					Assert.Equal(1, batch.Length);
					Assert.Equal(3, batch.Span[0]);
					break;
				case 2:
					Assert.Equal(4, batch.Span[0]);
					Assert.Equal(5, batch.Span[1]);
					break;
				default:
					throw new Exception("Shouldn't arrive here.");
			}
			i++;
		}
		Assert.Equal(3, i);
		await c.Reader.Completion; // Propagate possible failure
	}

	public static async IAsyncEnumerable<IMemoryOwner<T>> ReadBatchMemoryEnumerableAsyncBakedIn<T>(
		this ChannelReader<T> channelReader,
		int batchSize,
		TimeSpan timeout,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		BatchingChannelReader<T, IMemoryOwner<T>> reader = channelReader.BatchAsMemory(batchSize);
		reader = reader.WithTimeout(timeout); // stack overflow here
		while (true)
		{
			IMemoryOwner<T> item;
			try
			{
				item = await reader.ReadAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				cancellationToken.ThrowIfCancellationRequested();
				yield break;
			}
			catch (ChannelClosedException)
			{
				yield break;
			}

			if (item?.Memory.Length > 0) yield return item;
		}
	}
}
