using System;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1849:Call async methods when in an async method", Justification = "Task is complete.")]
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Called when all tasks complete.")]
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		Func<T, ValueTask> receiver,
		CancellationToken cancellationToken = default)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be at least 1.");
		Contract.EndContractBlock();

		if (cancellationToken.IsCancellationRequested)
			return Task.FromCanceled<long>(cancellationToken);

		if (maxConcurrency == 1)
			return reader.ReadAllAsync(receiver, cancellationToken, true).AsTask();

		var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken token = tokenSource.Token;
		var readers = new Task<long>[maxConcurrency];
		for (int r = 0; r < maxConcurrency; ++r)
			readers[r] = Read();

		return Task
			.WhenAll(readers)
			.ContinueWith(t =>
				{
					tokenSource.Dispose();
					return t.IsFaulted
						? Task.FromException<long>(t.Exception)
						: t.IsCanceled
						? Task.FromCanceled<long>(token)
						: Task.FromResult(t.Result.Sum());
				},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Current)
			.Unwrap();

		async Task<long> Read()
		{
			try
			{
				return await reader.ReadUntilCancelledAsync(token, ParallelReceiver, true).ConfigureAwait(false);
			}
			catch
			{
				tokenSource.Cancel();
				throw;
			}
		}

		ValueTask ParallelReceiver(T item, long i) => receiver(item);

	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency, receiver, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task<long> TaskReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		Func<T, Task> receiver,
		CancellationToken cancellationToken = default)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency, item => new ValueTask(receiver(item)), cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task<long> ReadAllConcurrentlyAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		int maxConcurrency,
		Func<TRead, ValueTask> receiver,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return ReadAllConcurrentlyAsync(channel.Reader, maxConcurrency, receiver, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static Task<long> ReadAllConcurrentlyAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Func<TRead, ValueTask> receiver)
		=> ReadAllConcurrentlyAsync(channel, maxConcurrency, receiver, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task<long> TaskReadAllConcurrentlyAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		int maxConcurrency,
		Func<TRead, Task> receiver,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return ReadAllConcurrentlyAsync(channel.Reader, maxConcurrency, item => new ValueTask(receiver(item)), cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task<long> ReadAllConcurrently<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		Action<T> receiver,
		CancellationToken cancellationToken = default)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency,
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
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static Task<long> ReadAllConcurrently<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Action<T> receiver)
		=> ReadAllConcurrently(reader, maxConcurrency, receiver, cancellationToken);

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task<long> ReadAllConcurrently<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		int maxConcurrency,
		Action<TRead> receiver,
		CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return channel.Reader.ReadAllConcurrently(maxConcurrency, receiver, cancellationToken);
	}

	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="TWrite">The item type of the writer.</typeparam>
	/// <typeparam name="TRead">The item type of the reader.</typeparam>
	/// <param name="channel">The channel to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="receiver">The receiver function.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convience.")]
	public static Task<long> ReadAllConcurrently<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Action<TRead> receiver)
		=> ReadAllConcurrently(channel, maxConcurrency, receiver, cancellationToken);
}
