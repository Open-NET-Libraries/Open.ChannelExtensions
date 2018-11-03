using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Reads all entries and applies the values to the provided transform function before buffering the results into another channel for consumption.
		/// </summary>
		/// <typeparam name="TIn">The type contained by the source channel.</typeparam>
		/// <typeparam name="TOut">The outgoing type from the resultant channel.</typeparam>
		/// <param name="source">The source channel.</param>
		/// <param name="transform">The transform function to apply the source entries before passing on to the output.</param>
		/// <param name="boundedSize">The width of the pipe: how many entries to buffer while waiting to be read from.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the output.</returns>
		public static ChannelReader<TOut> PipeAsync<TIn, TOut>(this ChannelReader<TIn> source,
			Func<TIn, ValueTask<TOut>> transform, int boundedSize = -1,
			CancellationToken cancellationToken = default)
		{
			var channel = boundedSize > 0
				? Channel.CreateBounded<TOut>(new BoundedChannelOptions(boundedSize)
				{
					SingleWriter = true,
					SingleReader = true,
					AllowSynchronousContinuations = true,
					FullMode = BoundedChannelFullMode.Wait
				})
				: Channel.CreateUnbounded<TOut>(new UnboundedChannelOptions
				{
					SingleWriter = true,
					SingleReader = true,
					AllowSynchronousContinuations = true
				});

			var writer = channel.Writer;
			source
				.ReadAllAsync(e =>
				{
					if (cancellationToken.IsCancellationRequested)
						return new ValueTask(Task.FromCanceled(cancellationToken));
					var result = transform(e);
					return result.IsCompletedSuccessfully
						? writer.WriteAsync(result.Result, cancellationToken)
						: ValueNotReady(result); // Result is not ready, so we need to wait for it.
				}, cancellationToken)
				.ContinueWith(t =>
				{
					writer.Complete(t.Exception);
				});

			return channel.Reader;

			async ValueTask ValueNotReady(ValueTask<TOut> value)
			{
				var r = await value;
				await writer.WriteAsync(r, cancellationToken);
			}
		}

		/// <summary>
		/// Reads all entries and applies the values to the provided transform function before buffering the results into another channel for consumption.
		/// </summary>
		/// <typeparam name="TIn">The type contained by the source channel.</typeparam>
		/// <typeparam name="TOut">The outgoing type from the resultant channel.</typeparam>
		/// <param name="source">The source channel.</param>
		/// <param name="transform">The transform function to apply the source entries before passing on to the output.</param>
		/// <param name="boundedSize">The width of the pipe: how many entries to buffer while waiting to be read from.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the output.</returns>
		public static ChannelReader<TOut> PipeAsync<TIn, TOut>(this Channel<TIn> source,
			Func<TIn, ValueTask<TOut>> transform, int boundedSize = -1,
			CancellationToken cancellationToken = default)
			=> source.Reader.PipeAsync(transform, boundedSize, cancellationToken);

		/// <summary>
		/// Reads all entries and applies the values to the provided transform function before buffering the results into another channel for consumption.
		/// </summary>
		/// <typeparam name="TIn">The type contained by the source channel.</typeparam>
		/// <typeparam name="TOut">The outgoing type from the resultant channel.</typeparam>
		/// <param name="source">The source channel.</param>
		/// <param name="transform">The transform function to apply the source entries before passing on to the output.</param>
		/// <param name="boundedSize">The width of the pipe: how many entries to buffer while waiting to be read from.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the output.</returns>
		public static ChannelReader<TOut> PipeAsync<TIn, TOut>(this ChannelReader<TIn> source,
			Func<TIn, Task<TOut>> transform, int boundedSize = -1,
			CancellationToken cancellationToken = default)
			=> source.PipeAsync(e => new ValueTask<TOut>(transform(e)), boundedSize, cancellationToken);

		/// <summary>
		/// Reads all entries and applies the values to the provided transform function before buffering the results into another channel for consumption.
		/// </summary>
		/// <typeparam name="TIn">The type contained by the source channel.</typeparam>
		/// <typeparam name="TOut">The outgoing type from the resultant channel.</typeparam>
		/// <param name="source">The source channel.</param>
		/// <param name="transform">The transform function to apply the source entries before passing on to the output.</param>
		/// <param name="boundedSize">The width of the pipe: how many entries to buffer while waiting to be read from.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the output.</returns>
		public static ChannelReader<TOut> PipeAsync<TIn, TOut>(this Channel<TIn> source,
			Func<TIn, Task<TOut>> transform, int boundedSize = -1,
			CancellationToken cancellationToken = default)
			=> source.Reader.PipeAsync(transform, boundedSize, cancellationToken);

		/// <summary>
		/// Reads all entries and applies the values to the provided transform function before buffering the results into another channel for consumption.
		/// </summary>
		/// <typeparam name="TIn">The type contained by the source channel.</typeparam>
		/// <typeparam name="TOut">The outgoing type from the resultant channel.</typeparam>
		/// <param name="source">The source channel.</param>
		/// <param name="transform">The transform function to apply the source entries before passing on to the output.</param>
		/// <param name="boundedSize">The width of the pipe: how many entries to buffer while waiting to be read from.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the output.</returns>
		public static ChannelReader<TOut> Pipe<TIn, TOut>(this ChannelReader<TIn> source,
			Func<TIn, TOut> transform, int boundedSize = -1,
			CancellationToken cancellationToken = default)
			=> source.PipeAsync(e => new ValueTask<TOut>(transform(e)), boundedSize, cancellationToken);

		/// <summary>
		/// Reads all entries and applies the values to the provided transform function before buffering the results into another channel for consumption.
		/// </summary>
		/// <typeparam name="TIn">The type contained by the source channel.</typeparam>
		/// <typeparam name="TOut">The outgoing type from the resultant channel.</typeparam>
		/// <param name="source">The source channel.</param>
		/// <param name="transform">The transform function to apply the source entries before passing on to the output.</param>
		/// <param name="boundedSize">The width of the pipe: how many entries to buffer while waiting to be read from.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the output.</returns>
		public static ChannelReader<TOut> Pipe<TIn, TOut>(this Channel<TIn> source,
			Func<TIn, TOut> transform, int boundedSize = -1,
			CancellationToken cancellationToken = default)
			=> source.Reader.Pipe(transform, boundedSize, cancellationToken);
	}
}
