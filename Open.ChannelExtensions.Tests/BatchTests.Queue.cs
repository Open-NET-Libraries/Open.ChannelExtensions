using System.Runtime.CompilerServices;

namespace Open.ChannelExtensions.Tests;

public static class BatchTestsOfQueue
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
			.BatchToQueues(2)
			.ReadAllAsync(async (batch, i) =>
			{
				int a = batch.Dequeue();
				int b = batch.Dequeue();
				switch (i)
				{
					case 0:
						Assert.Equal(1, a);
						Assert.Equal(2, b);
						break;

					case 1:
						Assert.Equal(3, a);
						Assert.Equal(4, b);
						break;

					case 2:
						Assert.Equal(5, a);
						Assert.Equal(6, b);
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
			.BatchToQueues(2)
			.ReadAllAsync(async (batch, i) =>
			{
				int a = batch.Dequeue();
				int b = batch.Dequeue();

				switch (i)
				{
					case 0:
						Assert.Equal(1, a);
						Assert.Equal(2, b);
						break;
					case 1:
						Assert.Equal(3, a);
						Assert.Equal(4, b);
						_ = Task.Run(async () =>
						{
							await Task.Delay(60000, token);
							if (!token.IsCancellationRequested) c.Writer.TryComplete(new Exception("Should have completed successfully."));
						});
						break;
					case 2:
						Assert.Equal(5, a);
						Assert.Equal(6, b);
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
		BatchingChannelReader<int, Queue<int>> reader = c.Reader.BatchToQueues(3);
		Assert.Equal(2, await reader.ReadAllAsync(tokenSource.Token, async (batch, i) =>
		{
			switch (i)
			{
				case 0:
				{
					Assert.Equal(3, batch.Count);

					int e1 = batch.Dequeue();
					int e2 = batch.Dequeue();
					int e3 = batch.Dequeue();

					Assert.Equal(1, e1);
					Assert.Equal(2, e2);
					Assert.Equal(3, e3);
					await Task.Delay(500);
					reader.ForceBatch();
					break;
				}

				case 1:
				{
					Assert.Equal(2, batch.Count);

					int e1 = batch.Dequeue();
					int e2 = batch.Dequeue();

					Assert.Equal(4, e1);
					Assert.Equal(5, e2);
					c.Writer.Complete();
					break;
				}

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
		BatchingChannelReader<int, Queue<int>> reader = c.Reader.BatchToQueues(3);
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
		Assert.Equal(2, await reader.ReadAllAsync(tokenSource.Token, async (batch, i) =>
		{
			switch (i)
			{
				case 0:
				{
					Assert.Equal(3, batch.Count);
					int e1 = batch.Dequeue();
					int e2 = batch.Dequeue();
					int e3 = batch.Dequeue();
					Assert.Equal(1, e1);
					Assert.Equal(2, e2);
					Assert.Equal(3, e3);
					Debug.WriteLine("First batch received.");
					break;
				}

				case 1:
				{
					Assert.Equal(2, batch.Count);
					int e1 = batch.Dequeue();
					int e2 = batch.Dequeue();
					Assert.Equal(4, e1);
					Assert.Equal(5, e2);
					Debug.WriteLine("Second batch received.");
					c.Writer.Complete();
					break;
				}
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
		BatchingChannelReader<int, Queue<int>> reader = c.Reader.BatchToQueues(10).WithTimeout(500);
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
		Assert.Equal(1, await reader.ReadAllAsync(tokenSource.Token, async (batch, i) =>
		{
			switch (i)
			{
				case 0:
					Assert.Equal(5, batch.Count);
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
		BatchingChannelReader<int, Queue<int>> reader = c.Reader.BatchToQueues(10).WithTimeout(500);
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
		Assert.Equal(4, await reader.ReadAllAsync(tokenSource.Token, async (batch, i) =>
		{
			switch (i)
			{
				case 0:
				case 2:
					Assert.Equal(10, batch.Count);
					break;
				case 1:
				case 3:
					Assert.Equal(5, batch.Count);
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
		BatchingChannelReader<int, Queue<int>> reader = c.Reader.BatchToQueues(10);

		var queue = new Queue<int>(Enumerable.Range(0, 100));
		int e;
		while (queue.TryDequeue(out e) && c.Writer.TryWrite(e))
			await Task.Yield();

		Assert.True(69 <= queue.Count);
		await reader.WaitToReadAsync();
		// At this point, a batch is prepared and there is room in the channel.
		await Dequeue();

		Assert.True(59 <= queue.Count);
		Assert.True(reader.TryRead(out Queue<int> batch));
		Assert.Equal(10, batch.Count);
		// At this point nothing is waiting so either a wait must occur or a read to trigger a new batch.

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.True(49 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.True(39 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.True(29 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.True(19 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.True(09 <= queue.Count);
		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);
		await Dequeue();

		Assert.Empty(queue);
		c.Writer.Complete();

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);

		Assert.True(reader.TryRead(out batch));
		Assert.Equal(10, batch.Count);

		Assert.False(reader.TryRead(out _));

		async ValueTask Dequeue()
		{
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
		await foreach (Queue<int> batch in c.Reader.ReadBatchQueueEnumerableAsyncBakedIn(2, TimeSpan.FromMilliseconds(500), CancellationToken.None))
		{
			switch (i)
			{
				case 0:
				{
					int a = batch.Dequeue();
					int b = batch.Dequeue();
					Assert.Equal(1, a);
					Assert.Equal(2, b);
					Debug.WriteLine("First batch received: " + string.Join(',', batch.Select(item => item)));
					break;
				}

				case 1:
					Assert.Single(batch);
					Assert.Equal(3, batch.Dequeue());
					Debug.WriteLine("Second batch received: " + string.Join(',', batch.Select(item => item)));
					break;

				case 2:
					Assert.Equal(4, batch.Dequeue());
					Assert.Equal(5, batch.Dequeue());
					Debug.WriteLine("Third batch received: " + string.Join(',', batch.Select(item => item)));
					break;

				default:
					throw new Exception("Shouldn't arrive here. Got batch: " + string.Join(',', batch.Select(item => item)));
			}
			i++;
		}
		Assert.Equal(3, i);
		await c.Reader.Completion; // Propagate possible failure
	}

	public static async IAsyncEnumerable<Queue<T>> ReadBatchQueueEnumerableAsyncBakedIn<T>(
		this ChannelReader<T> channelReader,
		int batchSize,
		TimeSpan timeout,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		BatchingChannelReader<T, Queue<T>> reader = channelReader.BatchToQueues(batchSize);
		reader = reader.WithTimeout(timeout); // stack overflow here
		while (true)
		{
			Queue<T> item;
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

			if (item?.Count > 0) yield return item;
		}
	}
}
