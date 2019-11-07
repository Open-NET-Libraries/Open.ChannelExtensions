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

		/// <summary>
		/// Reads from the channel up to the max count.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="max">The max size of the batch.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The batch requested.</returns>
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
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static async ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Func<T, long, ValueTask> receiver,
			bool deferredExecution = false)
		{
			if (reader == null) throw new NullReferenceException();
			if (receiver == null) throw new ArgumentNullException(nameof(receiver));
			Contract.EndContractBlock();

			if (deferredExecution)
				await Task.Yield();

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
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			CancellationToken cancellationToken,
			Func<TRead, long, ValueTask> receiver,
			bool deferredExecution = false)
			=> channel.Reader.ReadUntilCancelledAsync(cancellationToken, receiver, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Func<T, ValueTask> receiver,
			bool deferredExecution = false)
			=> reader.ReadUntilCancelledAsync(cancellationToken, (e, i) => receiver(e), deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			CancellationToken cancellationToken,
			Func<TRead, ValueTask> receiver,
			bool deferredExecution = false)
			=> channel.Reader.ReadUntilCancelledAsync(cancellationToken, receiver, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Action<T, long> receiver,
			bool deferredExecution = false)
			=> reader
				.ReadUntilCancelledAsync(
					cancellationToken,
					(e, i) =>
					{
						receiver(e, i);
						return new ValueTask();
					},
					deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			CancellationToken cancellationToken,
			Action<TRead, long> receiver,
			bool deferredExecution = false)
			=> channel.Reader.ReadUntilCancelled(cancellationToken, receiver, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<T>(this ChannelReader<T> reader,
			CancellationToken cancellationToken,
			Action<T> receiver,
			bool deferredExecution = false)
			=> reader.ReadUntilCancelledAsync(
				cancellationToken,
				(e, i) =>
				{
					receiver(e);
					return new ValueTask();
				},
				deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Action<TRead> receiver,
			CancellationToken cancellationToken,
			bool deferredExecution = false)
			=> channel.Reader.ReadUntilCancelled(cancellationToken, receiver, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static async ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, long, ValueTask> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
		{
			var count = await reader.ReadUntilCancelledAsync(cancellationToken, receiver, deferredExecution).ConfigureAwait(false);
			cancellationToken.ThrowIfCancellationRequested();
			return count;
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, long, ValueTask> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAllAsync(receiver, cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, long, Task> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAllAsync((e, i) => new ValueTask(receiver(e, i)), cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, ValueTask> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> reader.ReadAllAsync((e, i) => receiver(e), cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> TaskReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, Task> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> reader.ReadAllAsync((e, i) => new ValueTask(receiver(e)), cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, Task> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAllAsync((e, i) => new ValueTask(receiver(e)), cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The async receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Func<TRead, ValueTask> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAllAsync(receiver, cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before writreadinging.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
			Action<T, long> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
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
				cancellationToken,
				deferredExecution);
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Action<TRead, long> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAll(receiver, cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
			Action<T> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
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
				cancellationToken,
				deferredExecution);
		}

		/// <summary>
		/// Reads items from the channel and passes them to the receiver.
		/// </summary>
		/// <typeparam name="TWrite">The item type of the writer.</typeparam>
		/// <typeparam name="TRead">The item type of the reader.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
			Action<TRead> receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAll(receiver, cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and writes to the target writer.
		/// </summary>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="receiver">The receiver function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsLines(this ChannelReader<string> reader,
			TextWriter receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> reader.ReadAllAsync(line => new ValueTask(receiver.WriteLineAsync(line)), cancellationToken, deferredExecution);

		/// <summary>
		/// Reads items from the channel and writes to the target writer.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="receiver">The TextWriter to recieve the lines.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
		/// <returns>A task containing the count of items read that completes when no more reading is to be done.
		/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
		public static ValueTask<long> ReadAllAsLines<T>(this Channel<T, string> channel,
			TextWriter receiver,
			CancellationToken cancellationToken = default,
			bool deferredExecution = false)
			=> channel.Reader.ReadAllAsLines(receiver, cancellationToken, deferredExecution);

	}
}
