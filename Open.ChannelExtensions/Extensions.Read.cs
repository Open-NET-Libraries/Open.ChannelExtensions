using System.IO;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	private const string MustBeAtLeast1 = "Must be at least 1.";

	/// <summary>
	/// Waits for read to complete or the cancellation token to be cancelled.
	/// </summary>
	public static async ValueTask<bool> WaitToReadOrCancelAsync<T>(this ChannelReader<T> reader, CancellationToken cancellationToken)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		Contract.EndContractBlock();

		try
		{
			return await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			return false;
		}
	}

	/// <summary>
	/// Creates an enumerable that will read from the channel until no more are available for read.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>An enumerable that will read from the channel until no more are available for read</returns>
	public static IEnumerable<T> ReadAvailable<T>(this ChannelReader<T> reader, CancellationToken cancellationToken = default)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		Contract.EndContractBlock();

		return ReadAvailableCore(reader, cancellationToken);

		static IEnumerable<T> ReadAvailableCore(ChannelReader<T> reader, CancellationToken token)
		{
			if (token.CanBeCanceled)
			{
				while (!token.IsCancellationRequested && reader.TryRead(out T? e))
					yield return e;
			}
			else
			{
				while (reader.TryRead(out T? e))
					yield return e;
			}
		}
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

		if (cancellationToken.CanBeCanceled)
		{
			do
			{
				while (
					results.Count < max
					&& !cancellationToken.IsCancellationRequested
					&& reader.TryRead(out T? item))
				{
					results.Add(item);
				}

				if (results.Count == max)
					return results;

				cancellationToken.ThrowIfCancellationRequested();
			}
			while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));
		}
		else
		{
			do
			{
				while (
					results.Count < max
					&& reader.TryRead(out T? item))
				{
					results.Add(item);
				}

				if (results.Count == max)
					return results;
			}
			while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));
		}

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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
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

		long count = 0;

		// Note: if the channel has complete with an OperationCanceledException, this will throw when waiting to read.
		if (cancellationToken.CanBeCanceled)
		{
			do
			{
				while (
					!cancellationToken.IsCancellationRequested
					&& reader.TryRead(out T? item))
				{
					await receiver(item, count++).ConfigureAwait(false);
				}
			}
			while (
				!cancellationToken.IsCancellationRequested
				&& await reader.WaitToReadOrCancelAsync(cancellationToken).ConfigureAwait(false));
		}
		else
		{
			do
			{
				while (reader.TryRead(out T? item))
				{
					await receiver(item, count++).ConfigureAwait(false);
				}
			}
			while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));
		}

		return count;
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelledAsync<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver,
		bool deferredExecution = false)
		=> ReadUntilCancelledAsync(reader, cancellationToken, (e, _) => receiver(e), deferredExecution);

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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
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
				return default;
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "This method is designed around a cancellation token.")]
	public static ValueTask<long> ReadUntilCancelled<T>(this ChannelReader<T> reader,
		CancellationToken cancellationToken,
		Action<T> receiver,
		bool deferredExecution = false)
		=> reader.ReadUntilCancelledAsync(
			cancellationToken,
			(e, _) =>
			{
				receiver(e);
				return default;
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
		long count = await ReadUntilCancelledAsync(reader, cancellationToken, receiver, deferredExecution).ConfigureAwait(false);
		cancellationToken.ThrowIfCancellationRequested();
		return count;
	}

	/// <inheritdoc cref="ReadAllAsEnumerablesAsync{T}(ChannelReader{T}, Func{IEnumerable{T}, ValueTask}, bool, CancellationToken)"/>
	public static ValueTask ReadAllAsEnumerables<T>(this ChannelReader<T> reader,
		Action<IEnumerable<T>> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		return reader.ReadAllAsEnumerablesAsync(
			e =>
			{
				receiver(e);
				return default;
			},
			deferredExecution,
			cancellationToken);
	}

	/// <summary>
	/// Provides an enumerable to a receiver function.<br/>
	/// The enumerable will yield items while they are available
	/// and complete when none are available.<br/>
	/// </summary>
	/// <remarks>See <seealso cref="ReadAvailable{T}(ChannelReader{T}, CancellationToken)"/>.</remarks>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	public static async ValueTask ReadAllAsEnumerablesAsync<T>(this ChannelReader<T> reader,
		Func<IEnumerable<T>, ValueTask> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		if (deferredExecution)
			await Task.Yield();

		if (cancellationToken.CanBeCanceled)
		{
			while (
				!cancellationToken.IsCancellationRequested
				&& await reader.WaitToReadOrCancelAsync(cancellationToken).ConfigureAwait(false))
			{
				await receiver(reader.ReadAvailable(cancellationToken)).ConfigureAwait(false);
			}

			return;
		}

		while (await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
		{
			await receiver(reader.ReadAvailable(cancellationToken)).ConfigureAwait(false);
		}
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> ReadAllAsync<T>(this ChannelReader<T> reader,
		Func<T, ValueTask> receiver,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> ReadAllAsync(reader, (e, _) => receiver(e), deferredExecution, cancellationToken);

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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
		=> ReadAllAsync(reader, (e, _) => new ValueTask(receiver(e)), deferredExecution, cancellationToken);

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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
		=> ReadAllAsync(channel, (e, _) => new ValueTask(receiver(e)), deferredExecution, cancellationToken);

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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
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
				return default;
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
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
			(e, _) =>
			{
				receiver(e);
				return default;
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
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <returns>The count of items read after the reader has completed.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	/// <param name="receiver">The TextWriter to receive the lines.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before reading.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
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
	/// <param name="receiver">The TextWriter to receive the lines.</param>
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
	/// <param name="initialCapacity">An optional capacity to initialze the list with.</param>
	/// <returns>A list containing all the items from the completed channel.</returns>
	public static async ValueTask<List<T>> ToListAsync<T>(this ChannelReader<T> reader, int initialCapacity = -1)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		Contract.EndContractBlock();

		List<T> list = initialCapacity < 0 ? new() : new(initialCapacity);
		await ReadAll(reader, list.Add).ConfigureAwait(false);
		return list;
	}
}
