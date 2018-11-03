using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		public static ChannelReader<TOut> Pipe<TIn, TOut>(this ChannelReader<TIn> source,
			Func<TIn, Task<TOut>> transform, int width = -1,
			CancellationToken cancellationToken = default)
		{
			var channel = width>0
				? Channel.CreateBounded<TOut>(width)
				: Channel.CreateUnbounded<TOut>();

			var writer = channel.Writer;
			source
				.ReadAllAsync(async e =>
				{
					var next = await transform(e).ConfigureAwait(false);
					await writer.WriteAsync(next, cancellationToken).ConfigureAwait(false);
				}, cancellationToken)
				.ContinueWith(t =>
				{
					writer.Complete(t.Exception);
				});

			return channel.Reader;
		}

		public static ChannelReader<TOut> Pipe<TIn, TOut>(this Channel<TIn> source,
			Func<TIn, Task<TOut>> transform, int width = -1,
			CancellationToken cancellationToken = default)
			=> source.Reader.Pipe(transform, width, cancellationToken);
	}
}
