using System;
using System.Linq;
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
		public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
			int maxConcurrency,
			Func<T, ValueTask> receiver,
			CancellationToken cancellationToken = default)
		{
			if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be at least 1.");
			Contract.EndContractBlock();

			if (cancellationToken.IsCancellationRequested)
				return Task.FromCanceled<long>(cancellationToken);

			if (maxConcurrency == 1)
				return reader.ReadAllAsync(receiver, cancellationToken, true).AsTask();

			var readers = new Task<long>[maxConcurrency];
			for (var r = 0; r < maxConcurrency; ++r)
				readers[r] = reader
					.ReadUntilCancelledAsync(cancellationToken, ParallelReceiver, true).AsTask();

			return Task
				.WhenAll(readers)
				.ContinueWith(
					t => t.Result.Sum(),
					TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);

			ValueTask ParallelReceiver(T item, long i) => receiver(item);
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task<long> TaskReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
			int maxConcurrency,
			Func<T, Task> receiver,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllConcurrentlyAsync(maxConcurrency, item => new ValueTask(receiver(item)), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task<long> ReadAllConcurrentlyAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			int maxConcurrency,
			Func<TRead, ValueTask> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllConcurrentlyAsync(maxConcurrency, receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task<long> TaskReadAllConcurrentlyAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			int maxConcurrency,
			Func<TRead, Task> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllConcurrentlyAsync(maxConcurrency, item => new ValueTask(receiver(item)), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task<long> ReadAllConcurrently<T>(this ChannelReader<T> reader,
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
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task<long> ReadAllConcurrently<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			int maxConcurrency,
			Action<TRead> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllConcurrently(maxConcurrency, receiver, cancellationToken);
	}
}
