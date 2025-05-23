﻿namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Reads items from the channel and passes them to the receiver.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="taskCreationOptions">The task creation options to use.</param>
	/// <param name="scheduler">The task scheduler to use.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		TaskScheduler scheduler,
		TaskCreationOptions taskCreationOptions,
		Func<T, ValueTask> receiver,
		CancellationToken cancellationToken = default)
		=> Task.Factory
			.StartNew(
				() => ReadAllConcurrentlyAsync(reader, maxConcurrency, receiver, cancellationToken),
				cancellationToken,
				taskCreationOptions,
				scheduler)
			.Unwrap();

	/// <inheritdoc cref="ReadAllConcurrentlyAsync{T}(ChannelReader{T}, int, TaskScheduler, TaskCreationOptions, Func{T, ValueTask}, CancellationToken)"/>
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		TaskScheduler scheduler,
		Func<T, ValueTask> receiver,
		CancellationToken cancellationToken = default)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency, scheduler, TaskCreationOptions.None, receiver, cancellationToken);

	/// <inheritdoc cref="ReadAllConcurrentlyAsync{T}(ChannelReader{T}, int, TaskScheduler, TaskCreationOptions, Func{T, ValueTask}, CancellationToken)"/>
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

		// Leverage async to simplify this function and make it easy to dispose the token source.
		return ReadAllConcurrentlyAsyncCore(reader, maxConcurrency, receiver, cancellationToken);

		static async Task<long> ReadAllConcurrentlyAsyncCore(
			ChannelReader<T> reader, int maxConcurrency, Func<T, ValueTask> receiver, CancellationToken cancellationToken)
		{
			using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			CancellationToken token = tokenSource.Token;
			var readers = new Task<long>[maxConcurrency];
			var scheduler = TaskScheduler.Current;
			for (int r = 0; r < maxConcurrency; ++r)
				readers[r] = Task.Factory.StartNew(Read, cancellationToken, TaskCreationOptions.None, scheduler).Unwrap();

			// This produces the most accurate/reliable exception and cancellation results.
			return await Task
				.WhenAll(readers)
				.ContinueWith(
					t => t.IsFaulted
						? Task.FromException<long>(t.Exception!)
						: t.IsCanceled
						? Task.FromCanceled<long>(token)
						: SumAsync(t),
					CancellationToken.None,
					TaskContinuationOptions.ExecuteSynchronously,
					scheduler)
				.Unwrap()
				.ConfigureAwait(false);

			async Task<long> Read()
			{
				try
				{
					return await reader.ReadUntilCancelledAsync(token, (T item, long _) => receiver(item), true).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Debug.WriteLine(ex.ToString());
					await tokenSource.CancelAsync().ConfigureAwait(false);
					throw;
				}
			}

			static async Task<long> SumAsync(Task<long[]> counts)
				=> (await counts.ConfigureAwait(false)).Sum();
		}
	}

	/// <inheritdoc cref="ReadAllConcurrentlyAsync{T}(ChannelReader{T}, int, TaskScheduler, TaskCreationOptions, Func{T, ValueTask}, CancellationToken)"/>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency, receiver, cancellationToken);

	/// <inheritdoc cref="ReadAllConcurrentlyAsync{T}(ChannelReader{T}, int, TaskScheduler, TaskCreationOptions, Func{T, ValueTask}, CancellationToken)"/>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		TaskScheduler scheduler,
		TaskCreationOptions taskCreationOptions,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency, scheduler, taskCreationOptions, receiver, cancellationToken);

	/// <inheritdoc cref="ReadAllConcurrentlyAsync{T}(ChannelReader{T}, int, TaskScheduler, TaskCreationOptions, Func{T, ValueTask}, CancellationToken)"/>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
	public static Task<long> ReadAllConcurrentlyAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		TaskScheduler scheduler,
		CancellationToken cancellationToken,
		Func<T, ValueTask> receiver)
		=> ReadAllConcurrentlyAsync(reader, maxConcurrency, scheduler, TaskCreationOptions.None, receiver, cancellationToken);

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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
				return default;
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
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
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
	public static Task<long> ReadAllConcurrently<TWrite, TRead>(this Channel<TWrite, TRead> channel,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Action<TRead> receiver)
		=> ReadAllConcurrently(channel, maxConcurrency, receiver, cancellationToken);

	/// <summary>
	/// Partitions out potential reads to multiple threads as enumerables.
	/// </summary>
	/// <typeparam name="T">The item type.</typeparam>
	/// <param name="reader">The channel reader to read from.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="receiver">The async receiver function.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task that completes when no more reading is to be done.</returns>
	public static Task ReadAllConcurrentlyAsEnumerablesAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		Func<IEnumerable<T>, ValueTask> receiver,
		CancellationToken cancellationToken = default)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be at least 1.");
		Contract.EndContractBlock();

		if (cancellationToken.IsCancellationRequested)
			return Task.FromCanceled(cancellationToken);

		if (maxConcurrency == 1)
			return reader.ReadAllAsEnumerablesAsync(receiver, true, cancellationToken).AsTask();

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2000 // Dispose objects before losing scope
		var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
#pragma warning restore CA2000 // Dispose objects before losing scope
#pragma warning restore IDE0079 // Remove unnecessary suppression
		CancellationToken token = tokenSource.Token;
		var readers = new Task[maxConcurrency];
		for (int r = 0; r < maxConcurrency; ++r)
			readers[r] = Read();

		return Task
			.WhenAll(readers)
			.ContinueWith(t =>
				{
					tokenSource.Dispose();
					return t.IsFaulted
						? Task.FromException(t.Exception!)
						: t.IsCanceled
						? Task.FromCanceled(token)
						: Task.CompletedTask;
				},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Current)
			.Unwrap();

		async Task Read()
		{
			try
			{
				await reader.ReadAllAsEnumerablesAsync(receiver, true, token).ConfigureAwait(false);
			}
			catch
			{
				await tokenSource.CancelAsync().ConfigureAwait(false);
				throw;
			}
		}
	}

	/// <inheritdoc cref="ReadAllAsEnumerablesAsync{T}(ChannelReader{T}, Func{IEnumerable{T}, ValueTask}, bool, CancellationToken)"/>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
	public static Task ReadAllConcurrentlyAsEnumerablesAsync<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Func<IEnumerable<T>, ValueTask> receiver)
		=> ReadAllConcurrentlyAsEnumerablesAsync(reader, maxConcurrency, receiver, cancellationToken);

	/// <inheritdoc cref="ReadAllAsEnumerablesAsync{T}(ChannelReader{T}, Func{IEnumerable{T}, ValueTask}, bool, CancellationToken)"/>
	public static Task ReadAllConcurrentlyAsEnumerables<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		Action<IEnumerable<T>> receiver,
		CancellationToken cancellationToken = default)
	{
		if (receiver is null) throw new ArgumentNullException(nameof(receiver));
		Contract.EndContractBlock();

		return reader.ReadAllConcurrentlyAsEnumerablesAsync(
			maxConcurrency,
			e =>
			{
				receiver(e);
				return default;
			},
			cancellationToken);
	}

	/// <inheritdoc cref="ReadAllAsEnumerablesAsync{T}(ChannelReader{T}, Func{IEnumerable{T}, ValueTask}, bool, CancellationToken)"/>
	[SuppressMessage("Design", "CA1068:CancellationToken parameters must come last", Justification = "Provided for aesthetic convenience.")]
	public static Task ReadAllConcurrentlyAsEnumerables<T>(this ChannelReader<T> reader,
		int maxConcurrency,
		CancellationToken cancellationToken,
		Action<IEnumerable<T>> receiver)
		=> ReadAllConcurrentlyAsEnumerables(reader, maxConcurrency, receiver, cancellationToken);
}
