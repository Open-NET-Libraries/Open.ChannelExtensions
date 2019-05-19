using System;
using System.Linq;
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
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static async ValueTask ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
            CancellationToken cancellationToken,
            Func<T, int, ValueTask> receiver)
        {
            var localCount = 0;
            do
            {
                while (
                    !cancellationToken.IsCancellationRequested
                    && reader.TryRead(out var item))
                {
                    var result = receiver(item, localCount++);
                    if (!result.IsCompletedSuccessfully)
                        await result.ConfigureAwait(false);
                }
            }
            while (
                !cancellationToken.IsCancellationRequested
                && await reader.WaitToReadAsync()
                    .ConfigureAwait(false));
        }

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelledAsync<T>(this Channel<T> channel,
            CancellationToken cancellationToken,
            Func<T, int, ValueTask> receiver)
            => channel.Reader.ReadUntilCancelledAsync(cancellationToken, receiver);

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="reader">The channel reader to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
            CancellationToken cancellationToken,
            Func<T, ValueTask> receiver)
            => reader.ReadUntilCancelledAsync(cancellationToken, (e, i) => receiver(e));

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelledAsync<T>(this Channel<T> channel,
            CancellationToken cancellationToken,
            Func<T, ValueTask> receiver)
            => channel.Reader.ReadUntilCancelledAsync(cancellationToken, receiver);

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="reader">The channel reader to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelled<T>(this ChannelReader<T> reader,
            CancellationToken cancellationToken,
            Action<T, int> receiver)
            => reader
                .ReadUntilCancelledAsync(
                    cancellationToken,
                    (e, i) =>
                    {
                        receiver(e, i);
                        return new ValueTask(Task.CompletedTask);
                    });

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelled<T>(this Channel<T> channel,
            CancellationToken cancellationToken,
            Action<T, int> receiver)
            => channel.Reader.ReadUntilCancelled(cancellationToken, receiver);

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="reader">The channel reader to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelled<T>(this ChannelReader<T> reader,
            CancellationToken cancellationToken,
            Action<T> receiver)
            => reader.ReadUntilCancelledAsync(
                cancellationToken,
                (e, i) =>
                {
                    receiver(e);
                    return new ValueTask(Task.CompletedTask);
                });

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelled<T>(this Channel<T> channel,
            Action<T> receiver,
            CancellationToken cancellationToken)
            => channel.Reader.ReadUntilCancelled(cancellationToken, receiver);

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="reader">The channel reader to read from.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static async ValueTask ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, int, ValueTask> receiver,
			CancellationToken cancellationToken = default)
		{
            await reader.ReadUntilCancelledAsync(cancellationToken, receiver);
            cancellationToken.ThrowIfCancellationRequested();
		}

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadAllAsync<T>(this Channel<T> channel,
			Func<T, int, ValueTask> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync(receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static ValueTask ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, ValueTask> receiver,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllAsync((e, i) => receiver(e), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static ValueTask ReadAllAsync<T>(this Channel<T> channel,
			Func<T, ValueTask> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync(receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static ValueTask ReadAll<T>(this ChannelReader<T> reader,
			Action<T, int> receiver,
			CancellationToken cancellationToken = default)
			=> reader
                .ReadAllAsync((e, i) =>
				{
					receiver(e, i);
					return new ValueTask(Task.CompletedTask);
				},
				cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static ValueTask ReadAll<T>(this Channel<T> channel,
			Action<T, int> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAll(receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static ValueTask ReadAll<T>(this ChannelReader<T> reader,
			Action<T> receiver,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllAsync((e, i) =>
				{
					receiver(e);
					return new ValueTask(Task.CompletedTask);
				},
				cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static ValueTask ReadAll<T>(this Channel<T> channel,
			Action<T> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAll(receiver, cancellationToken);
	}
}
