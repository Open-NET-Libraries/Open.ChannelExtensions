using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests
{
	public static class TimedBatchTests
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
				.Batch(2, TimeSpan.FromMilliseconds(100))
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
			var token = tokenSource.Token;
			await c.Reader
				.Batch(2,TimeSpan.FromMilliseconds(100))
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
    }
}
