using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Xunit;

namespace Open.ChannelExtensions.Tests
{
	public static class BatchTests
	{
        [Fact]
		public static async Task SimpleBatch2Test()
		{
            var c = Channel.CreateUnbounded<int>(new UnboundedChannelOptions { SingleReader = false, SingleWriter = false });
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
			Task.Run(async () =>
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
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed


			await c.Reader
                .Batch(2)
                .ReadAllAsync(async (batch, i) =>
                {
                    switch(i)
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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                c.Writer.TryWrite(1);
                c.Writer.TryWrite(2);
                c.Writer.TryWrite(3);
                c.Writer.TryWrite(4);
                c.Writer.TryWrite(5);
                c.Writer.TryWrite(6);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            using var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
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
                            _ = Task.Run(async () => {
                                await Task.Delay(60000, token);
                                if(!token.IsCancellationRequested) c.Writer.TryComplete(new Exception("Should have completed successfuly."));
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
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                c.Writer.TryWrite(1);
                c.Writer.TryWrite(2);
                c.Writer.TryWrite(3);
                c.Writer.TryWrite(4);
                c.Writer.TryWrite(5);
            });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

            using var tokenSource = new CancellationTokenSource();
            var token = tokenSource.Token;
            var reader = c.Reader.Batch(3);
            Assert.Equal(2, await reader.ReadAllAsync(async (batch, i) =>
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
    }
}
