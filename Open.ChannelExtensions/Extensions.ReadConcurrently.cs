using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
			int maxConcurrency,
			Func<T, ValueTask> receiver,
			CancellationToken cancellationToken = default)
		{
			if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be at least 1.");
			Contract.EndContractBlock();

			if (cancellationToken.IsCancellationRequested)
				return Task.FromCanceled(cancellationToken);

			if (maxConcurrency == 1)
				return reader.ReadAllAsync(receiver, cancellationToken).AsTask();

			var readers = new Task[maxConcurrency];
			for (var r = 0; r < maxConcurrency; ++r)
				readers[r] = reader
					.ReadUntilCancelledAsync(cancellationToken, ParallelReceiver).AsTask();

			return Task
				.WhenAll(readers)
				.ContinueWith(
					t => (!t.IsFaulted && !t.IsCanceled && cancellationToken.IsCancellationRequested) ? Task.FromCanceled(cancellationToken) : t,
					TaskContinuationOptions.ExecuteSynchronously)
				.Unwrap();

			ValueTask ParallelReceiver(T item, int i) => receiver(item);
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllConcurrentlyAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			int maxConcurrency,
			Func<TRead, ValueTask> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllConcurrentlyAsync(maxConcurrency, receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllConcurrently<T>(this ChannelReader<T> reader,
			int maxConcurrency,
			Action<T> receiver,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllConcurrentlyAsync(maxConcurrency,
				e =>
				{
					receiver(e);
					return new ValueTask();
				},
				cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllConcurrently<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			int maxConcurrency,
			Action<TRead> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllConcurrently(maxConcurrency, receiver, cancellationToken);
	}
}
