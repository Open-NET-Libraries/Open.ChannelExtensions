using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{

		/// <summary>
		/// Creates an enumerable that will read from the channel until no more are available for read.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <returns>An enumerable that will read from the channel until no more are available for read</returns>
		public static IEnumerable<T> ReadAvailable<T>(this ChannelReader<T> reader)
		{
			if (reader == null) throw new NullReferenceException();
			Contract.EndContractBlock();

			while (reader.TryRead(out var e))
				yield return e;
		}

		public static async ValueTask<List<T>> ReadBatchAsync<T>(this ChannelReader<T> reader, int max, CancellationToken cancellationToken = default)
		{
			if (reader == null) throw new NullReferenceException();
			if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), max, "Must be at least 1.");
			Contract.EndContractBlock();

			var results = new List<T>(max);

			do
			{
				while (
					results.Count < max
					&& !cancellationToken.IsCancellationRequested
					&& reader.TryRead(out var item))
				{
					results.Add(item);
				}

				if (results.Count == max)
					return results;

				cancellationToken.ThrowIfCancellationRequested();
			}
			while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));

			results.TrimExcess();
			return results;
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static async ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Func<T, long, ValueTask> receiver)
		{
			if (reader == null) throw new NullReferenceException();
			if (receiver == null) throw new ArgumentNullException(nameof(receiver));
			Contract.EndContractBlock();

			long index = 0;
			do
			{
				var next = new ValueTask();
				while (
					!cancellationToken.IsCancellationRequested
					&& reader.TryRead(out var item))
				{
					await next.ConfigureAwait(false);
					next = receiver(item, index++);
				}
				await next.ConfigureAwait(false);
			}
			while (
				!cancellationToken.IsCancellationRequested
				&& await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));

			return index;
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			CancellationToken cancellationToken,
			Func<TRead, long, ValueTask> receiver)
			=> channel.Reader.ReadUntilCancelledAsync(cancellationToken, receiver);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Action<T, long> receiver)
			=> reader
				.ReadUntilCancelledAsync(
					cancellationToken,
					(e, i) =>
					{
						receiver(e, i);
						return new ValueTask();
					});

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			CancellationToken cancellationToken,
			Action<TRead, long> receiver)
			=> channel.Reader.ReadUntilCancelled(cancellationToken, receiver);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Action<T> receiver)
			=> reader.ReadUntilCancelledAsync(
				cancellationToken,
				(e, i) =>
				{
					receiver(e);
					return new ValueTask();
				});

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static async ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, long, ValueTask> receiver,
			CancellationToken cancellationToken = default)
		{
			var count = await reader.ReadUntilCancelledAsync(cancellationToken, receiver).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			return count;
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, long, ValueTask> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync(receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, long, Task> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync((e, i) => new ValueTask(receiver(e, i)), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, Task> receiver,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllAsync((e, i) => new ValueTask(receiver(e)), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, Task> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync((e, i) => new ValueTask(receiver(e)), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
			Action<T, long> receiver,
			CancellationToken cancellationToken = default)
		{
			if (reader == null) throw new NullReferenceException();
			if (receiver == null) throw new ArgumentNullException(nameof(receiver));
			Contract.EndContractBlock();

			return reader
				.ReadAllAsync((e, i) =>
				{
					receiver(e, i);
					return new ValueTask();
				},
				cancellationToken);
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Action<TRead, long> receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAll(receiver, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
			Action<T> receiver,
			CancellationToken cancellationToken = default)
		{
			if (reader == null) throw new NullReferenceException();
			if (receiver == null) throw new ArgumentNullException(nameof(receiver));
			Contract.EndContractBlock();

			return reader.ReadAllAsync(
				(e, i) =>
				{
					receiver(e);
					return new ValueTask();
				},
				cancellationToken);
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsLines(this ChannelReader<string> reader,
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
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsLines<T>(this Channel<T, string> channel,
			TextWriter receiver,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsLines(receiver, cancellationToken);

	}
}
