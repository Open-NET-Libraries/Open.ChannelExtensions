namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static Task<long> WriteAllConcurrentlyAsync<T>(
		this ChannelWriter<T> target,
		int maxConcurrency,
		IEnumerable<ValueTask<T>> source,
		bool complete = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		if (maxConcurrency < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Must be at least 1.");
		Contract.EndContractBlock();

		if (cancellationToken.IsCancellationRequested)
			return Task.FromCanceled<long>(cancellationToken);

		if (maxConcurrency == 1)
			return target.WriteAllAsync(source, complete, true, cancellationToken).AsTask();

		Task? shouldWait = target
			.WaitToWriteAndThrowIfClosedAsync("The target channel was closed before writing could begin.", cancellationToken)
			.AsTask(); // ValueTasks can only have a single await.

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA2000 // Dispose objects before losing scope
		var errorTokenSource = new CancellationTokenSource();
#pragma warning restore CA2000 // Dispose objects before losing scope
#pragma warning restore IDE0079 // Remove unnecessary suppression
		CancellationToken errorToken = errorTokenSource.Token;
		IEnumerator<ValueTask<T>>? enumerator = source.GetEnumerator();
		var writers = new Task<long>[maxConcurrency];
		for (int w = 0; w < maxConcurrency; w++)
			writers[w] = WriteAllAsyncCore();

		return Task
			.WhenAll(writers)
			.ContinueWith(t =>
				{
					errorTokenSource.Dispose();
					if (complete)
						target.Complete(t.Exception);

#pragma warning disable IDE0079 // Remove unnecessary suppression
#pragma warning disable CA1849 // Call async methods when in an async method
					return t.IsFaulted
						? Task.FromException<long>(t.Exception!)
						: t.IsCanceled
						? Task.FromCanceled<long>(cancellationToken)
						: Task.FromResult(t.Result.Sum());
#pragma warning restore CA1849 // Call async methods when in an async method
#pragma warning restore IDE0079 // Remove unnecessary suppression
				},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Current)
			.Unwrap();

		// returns false if there's no more (wasn't cancelled).
		async Task<long> WriteAllAsyncCore()
		{
			await Task.Yield();

			try
			{
				await shouldWait.ConfigureAwait(false);
				long count = 0;
				ValueTask next = default;
				bool potentiallyCancelled = true; // if it completed and actually returned false, no need to bubble the cancellation since it actually completed.
				while (!errorToken.IsCancellationRequested
					&& !cancellationToken.IsCancellationRequested
					&& (potentiallyCancelled = TryMoveNextSynchronized(enumerator, out ValueTask<T> e)))
				{
					T? value = await e.ConfigureAwait(false);
					await next.ConfigureAwait(false);
					count++;
					next = target.TryWrite(value) // do this to avoid unnecessary early cancel.
						? default
						: target.WriteAsync(value, cancellationToken);
				}
				await next.ConfigureAwait(false);
				if (potentiallyCancelled) cancellationToken.ThrowIfCancellationRequested();
				return count;
			}
			catch
			{
#if NET8_0_OR_GREATER
				await errorTokenSource.CancelAsync().ConfigureAwait(false);
#else
				errorTokenSource.Cancel();
#endif
				throw;
			}
		}
	}

	static bool TryMoveNextSynchronized<T>(
		IEnumerator<T> source,
		out T value)
	{
		lock (source)
		{
			if (source.MoveNext())
			{
				value = source.Current;
				return true;
			}
		}

		value = default!;
		return false;
	}

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static Task<long> WriteAllConcurrentlyAsync<T>(
		this ChannelWriter<T> target,
		int maxConcurrency,
		IEnumerable<Task<T>> source,
		bool complete = false,
		CancellationToken cancellationToken = default)
		=> WriteAllConcurrentlyAsync(
			target,
			maxConcurrency,
			(source ?? throw new ArgumentNullException(nameof(source))).WrapValueTask(),
			complete,
			cancellationToken);

	/// <summary>
	/// Asynchronously executes all entries and writes their results to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static Task<long> WriteAllConcurrentlyAsync<T>(
		this ChannelWriter<T> target,
		int maxConcurrency,
		IEnumerable<Func<T>> source,
		bool complete = false,
		CancellationToken cancellationToken = default)
		=> WriteAllConcurrentlyAsync(
			target,
			maxConcurrency,
			(source ?? throw new ArgumentNullException(nameof(source))).WrapValueTask(),
			complete, cancellationToken);
}
