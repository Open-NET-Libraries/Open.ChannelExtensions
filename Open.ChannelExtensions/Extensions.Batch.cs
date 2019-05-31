using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		public static ChannelReader<List<T>> Batch<T>(this ChannelReader<T> source, int batchSize, int maxBatches = 3)
		{
			var buffer = maxBatches < 1
				? Channel.CreateUnbounded<List<T>>(new UnboundedChannelOptions() { SingleWriter = true })
				: Channel.CreateBounded<List<T>>(new BoundedChannelOptions(maxBatches) { SingleWriter = true });

			Task.Run(async () =>
			{
				List<T> list;
				while ((list = await source.ReadBatchAsync(batchSize)).Count != 0)
					await buffer.Writer.WriteAsync(list);
				await source.Completion;
				await buffer.CompleteAsync();
			});

			return buffer.Reader;
		}
	}
}
