using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	private const string MustBeAtLeast1 = "Must be at least 1.";

	/// <summary>
	/// Creates an enumerable that will read from the channel until no more are available for read.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <returns>An enumerable that will read from the channel until no more are available for read</returns>
	public static IEnumerable<T> ReadAvailable<T>(this ChannelReader<T> reader)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
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
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (max < 1) throw new ArgumentOutOfRangeException(nameof(max), max, MustBeAtLeast1);
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
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static async ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Func<T, long, ValueTask> receiver,
		bool deferredExecution = false)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		if (deferredExecution)
			await Task.Yield();


		long index = 0;
		try
		{
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
		}
		catch (OperationCanceledException)
		{
			// In case WaitToReadAsync is cancelled.
		}

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
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Func<TRead, long, ValueTask> receiver,
		bool deferredExecution = false)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return channel.Reader.ReadUntilCancelledAsync(cancellationToken, receiver, deferredExecution);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver,
		bool deferredExecution = false)
		=> ReadUntilCancelledAsync(reader, cancellationToken, (e, i) => receiver(e), deferredExecution);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelledAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Func<TRead, ValueTask> receiver,
		bool deferredExecution = false)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return ReadUntilCancelledAsync(channel.Reader, cancellationToken, receiver, deferredExecution);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelled<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Action<T, long> receiver,
		bool deferredExecution = false)
		=> ReadUntilCancelledAsync(
			reader,
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
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Action<TRead, long> receiver,
		bool deferredExecution = false)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return channel.Reader.ReadUntilCancelled(cancellationToken, receiver, deferredExecution);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
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
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadUntilCancelled<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Action<TRead> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return channel.Reader.ReadUntilCancelled(cancellationToken, receiver, deferredExecution);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static async ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, long, ValueTask> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		var count = await ReadUntilCancelledAsync(reader, cancellationToken, receiver, deferredExecution).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		return count;
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, long, ValueTask> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAllAsync(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Func<T, long, ValueTask> receiver,
		bool deferredExecution = false)
		=> ReadAllAsync(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, long, ValueTask> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return ReadAllAsync(channel.Reader, receiver, deferredExecution, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, long, ValueTask> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Func<TRead, long, ValueTask> receiver,
		bool deferredExecution = false)
		=> ReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, long, Task> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> ReadAllAsync(channel, (e, i) => new ValueTask(receiver(e, i)), deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, long, Task> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> TaskReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Func<TRead, long, Task> receiver,
		bool deferredExecution = false)
		=> TaskReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, ValueTask> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> ReadAllAsync(reader, (e, i) => receiver(e), deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, ValueTask> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAllAsync(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver,
		bool deferredExecution = false)
		=> ReadAllAsync(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> TaskReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, Task> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> ReadAllAsync(reader, (e, i) => new ValueTask(receiver(e)), deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> TaskReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, Task> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> TaskReadAllAsync(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> TaskReadAllAsync<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Func<T, Task> receiver,
		bool deferredExecution = false)
		=> TaskReadAllAsync(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, Task> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> ReadAllAsync(channel, (e, i) => new ValueTask(receiver(e)), deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, Task> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> TaskReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> TaskReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Func<TRead, Task> receiver,
		bool deferredExecution = false)
		=> TaskReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, ValueTask> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		return ReadAllAsync(channel.Reader, receiver, deferredExecution, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Func<TRead, ValueTask> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAllAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Func<TRead, ValueTask> receiver,
		bool deferredExecution = false)
		=> ReadAllAsync(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writreadinging.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
		Action<T, long> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		return reader
			.ReadAllAsync((e, i) =>
			{
				receiver(e, i);
				return new ValueTask();
			},
			deferredExecution,
			cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writreadinging.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
		Action<T, long> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAll(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writreadinging.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Action<T, long> receiver,
		bool deferredExecution = false)
		=> ReadAll(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Action<TRead, long> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		return ReadAll(channel.Reader, receiver, deferredExecution, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">The optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Action<TRead, long> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAll(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">The optional cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Action<TRead, long> receiver,
		bool deferredExecution = false)
		=> ReadAll(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
		Action<T> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		return reader.ReadAllAsync(
			(e, i) =>
			{
				receiver(e);
				return new ValueTask();
			},
			deferredExecution,
			cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
		Action<T> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAll(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAll<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Action<T> receiver,
		bool deferredExecution = false)
		=> ReadAll(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Action<TRead> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return ReadAll(channel.Reader, receiver, deferredExecution, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">The optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		Action<TRead> receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAll(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="cancellationToken">The optional cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static ValueTask<long> ReadAll<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		CancellationToken cancellationToken,
		Action<TRead> receiver,
		bool deferredExecution = false)
		=> ReadAll(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and writes to the target writer.
	/// </summary>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsLines(this ChannelReader<string> reader,
		TextWriter receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> ReadAllAsync(reader, line => new ValueTask(receiver.WriteLineAsync(line)), deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and writes to the target writer.
	/// </summary>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsLines(this ChannelReader<string> reader,
		TextWriter receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAllAsLines(reader, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Reads items from the channel and writes to the target writer.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The TextWriter to recieve the lines.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsLines<T>(this Channel<T, string> channel,
		TextWriter receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return ReadAllAsLines(channel.Reader, receiver, deferredExecution, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and writes to the target writer.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="receiver">The TextWriter to recieve the lines.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsLines<T>(this Channel<T, string> channel,
		TextWriter receiver,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> ReadAllAsLines(channel, receiver, deferredExecution, cancellationToken);

	/// <summary>
	/// Adds all items in the reader to a list and returns when the channel completes.
	/// Note: this should only be used when the results of the channel are guaranteed to be limited.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <returns>A list containing all the items from the completed channel.</returns>
	public static async ValueTask<List<T>> ToListAsync<T>(this ChannelReader<T> reader)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		Contract.EndContractBlock();

		var list = new List<T>();
		await ReadAll(reader, list.Add).ConfigureAwait(false);
		return list;
	}

}
