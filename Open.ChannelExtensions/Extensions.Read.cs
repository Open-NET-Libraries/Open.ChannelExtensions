using System;
using System.IO;
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
                var next = new ValueTask();
                while (
                    !cancellationToken.IsCancellationRequested
                    && reader.TryRead(out var item))
                {
                    if (!next.IsCompletedSuccessfully) await next.ConfigureAwait(false);
                    next = receiver(item, localCount++);
                }
                if (!next.IsCompletedSuccessfully) await next.ConfigureAwait(false);
            }
            while (
                !cancellationToken.IsCancellationRequested
                && await reader.WaitToReadAsync().ConfigureAwait(false));
        }

        /// <summary>
        /// Reads items from the channel and passes them to the receiver.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="receiver">The async receiver function.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            CancellationToken cancellationToken,
            Func<TRead, int, ValueTask> receiver)
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
        public static ValueTask ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            CancellationToken cancellationToken,
            Func<TRead, ValueTask> receiver)
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
        public static ValueTask ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            CancellationToken cancellationToken,
            Action<TRead, int> receiver)
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
        public static ValueTask ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            Action<TRead> receiver,
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
            await reader.ReadUntilCancelledAsync(cancellationToken, receiver).ConfigureAwait(false);
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
        public static ValueTask ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            Func<TRead, int, ValueTask> receiver,
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
        public static ValueTask ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            Func<TRead, ValueTask> receiver,
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
        public static ValueTask ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            Action<TRead, int> receiver,
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
        public static ValueTask ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
            Action<TRead> receiver,
            CancellationToken cancellationToken = default)
            => channel.Reader.ReadAll(receiver, cancellationToken);

        /// <summary>
        /// Reads items from the channel and writes to the target writer.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="reader">The channel reader to read from.</param>
        /// <param name="receiver">The receiver function.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadAllAsLines(this ChannelReader<string> reader,
            TextWriter receiver,
            CancellationToken cancellationToken = default)
            => reader.ReadAllAsync(line => new ValueTask(receiver.WriteLineAsync(line)), cancellationToken);

        /// <summary>
        /// Reads items from the channel and writes to the target writer.
        /// </summary>
        /// <typeparam name="T">The item type.</typeparam>
        /// <param name="channel">The channel to read from.</param>
        /// <param name="receiver">The TextWriter to recieve the lines.</param>
        /// <param name="cancellationToken">An optional cancellation token.</param>
        /// <returns>A task that completes when no more reading is to be done.</returns>
        public static ValueTask ReadAllAsLines<T>(this Channel<T, string> channel,
            TextWriter receiver,
            CancellationToken cancellationToken = default)
            => channel.Reader.ReadAllAsLines(receiver, cancellationToken);

    }
}
