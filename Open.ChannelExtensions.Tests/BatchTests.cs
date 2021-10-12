using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests;

public static class BatchTests
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
			.Batch(2)
			.ReadAllAsync(async (batch, i) =>
			{
				switch (i)
				{
					case 0:
						Assert.Equal(1, batch[0]);
						Assert.Equal(2, batch[1]);
						break;

					case 1:
						Assert.Equal(3, batch[0]);
						Assert.Equal(4, batch[1]);
						break;

					case 2:
						Assert.Equal(5, batch[0]);
						Assert.Equal(6, batch[1]);
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
			.Batch(2)
			.ReadAllAsync(async (batch, i) =>
			{
				switch (i)
				{
					case 0:
						Assert.Equal(1, batch[0]);
						Assert.Equal(2, batch[1]);
						break;
					case 1:
						Assert.Equal(3, batch[0]);
						Assert.Equal(4, batch[1]);
						_ = Task.Run(async () =>
						{
							await Task.Delay(60000, token);
							if (!token.IsCancellationRequested) c.Writer.TryComplete(new Exception("Should have completed successfuly."));
						});
						break;
					case 2:
						Assert.Equal(5, batch[0]);
						Assert.Equal(6, batch[1]);
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
		BatchingChannelReader<int> reader = c.Reader.Batch(3);
		Assert.Equal(2, await reader.ReadAllAsync(tokenSource.Token, async (batch, i) =>
			{
				switch (i)
				{
					case 0:
						Assert.Equal(1, batch[0]);
						Assert.Equal(2, batch[1]);
						Assert.Equal(3, batch[2]);
						await Task.Delay(500);
						reader.ForceBatch();
						break;
					case 1:
						Assert.Equal(2, batch.Count);
						Assert.Equal(4, batch[0]);
						Assert.Equal(5, batch[1]);
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
		BatchingChannelReader<int> reader = c.Reader.Batch(3);
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
					Assert.Equal(1, batch[0]);
					Assert.Equal(2, batch[1]);
					Assert.Equal(3, batch[2]);
					Debug.WriteLine("First batch received.");
					break;
				case 1:
					Assert.Equal(2, batch.Count);
					Assert.Equal(4, batch[0]);
					Assert.Equal(5, batch[1]);
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
		BatchingChannelReader<int> reader = c.Reader.Batch(10).WithTimeout(500);
		var complete = false;
		_ = Task.Run(async () =>
		{
			for (var i = 0; i < 5; i++)
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
		BatchingChannelReader<int> reader = c.Reader.Batch(10).WithTimeout(500);
		_ = Task.Run(async () =>
		{
			for(var i = 0;i<15;i++)
			{
				c.Writer.TryWrite(i);
			}

			await Task.Delay(1000);

			for (var i = 0; i < 15; i++)
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
		BatchingChannelReader<int> reader = c.Reader.Batch(10);
		
		var queue = new Queue<int>(Enumerable.Range(0, 100));
		int e;
		while(queue.TryDequeue(out e) && c.Writer.TryWrite(e))
			await Task.Yield();

		Assert.True(69 <= queue.Count);
		await reader.WaitToReadAsync();
		// At this point, a batch is prepared and there is room in the channel.
		await Dequeue();

		Assert.True(59 <= queue.Count);
		Assert.True(reader.TryRead(out var batch));
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
		_ = Task.Run(async () => {
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
		var i = 0;
		await foreach (var batch in c.Reader.ReadBatchEnumerableAsyncBakedIn(2, TimeSpan.FromMilliseconds(500), CancellationToken.None))
		{
			switch (i)
			{
				case 0:
					Assert.Equal(1, batch[0]);
					Assert.Equal(2, batch[1]);
					Debug.WriteLine("First batch received: " + string.Join(',', batch.Select(item => item)));
					break;
				case 1:
					Assert.Equal(1, batch.Count);
					Assert.Equal(3, batch[0]);
					Debug.WriteLine("Second batch received: " + string.Join(',', batch.Select(item => item)));
					break;
				case 2:
					Assert.Equal(4, batch[0]);
					Assert.Equal(5, batch[1]);
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
	public static async IAsyncEnumerable<IList<T>> ReadBatchEnumerableAsyncBakedIn<T>(
		this ChannelReader<T> channelReader,
		int batchSize,
		TimeSpan timeout,
		[EnumeratorCancellation] CancellationToken cancellationToken = default)
	{
		var reader = channelReader.Batch(batchSize);
		reader = reader.WithTimeout(timeout); // stack overflow here
		while (true)
		{
			List<T> item;
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

