using System.IO;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	private const string ChannelClosedMessage = "The target channel was closed before writing could begin.";

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static async ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IEnumerable<ValueTask<T>> source,
		bool complete = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		try
		{
			await target
				.WaitToWriteAndThrowIfClosedAsync(ChannelClosedMessage, deferredExecution, cancellationToken)
				.ConfigureAwait(false);

			long count = 0;
			ValueTask next = default;
			foreach (ValueTask<T> e in source)
			{
				T? value = await e.ConfigureAwait(false);
				await next.ConfigureAwait(false);
				count++;
				next = target.WriteAsync(value, cancellationToken);
			}
			await next.ConfigureAwait(false);
			return count;
		}
		catch (ChannelClosedException) { throw; }
		catch (Exception ex)
		{
			if (complete)
			{
				target.TryComplete(ex);
				complete = false;
			}
			throw;
		}
		finally
		{
			if (complete)
				target.TryComplete();
		}
	}

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IEnumerable<ValueTask<T>> source,
		bool complete,
		CancellationToken cancellationToken)
		=> WriteAllAsync(target, source, complete, false, cancellationToken);

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IEnumerable<Task<T>> source,
		bool complete = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		return WriteAllAsync(
			target,
			source.WrapValueTask(),
			complete,
			deferredExecution,
			cancellationToken);
	}

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IEnumerable<Task<T>> source,
		bool complete,
		CancellationToken cancellationToken)
		=> WriteAllAsync(target, source, complete, false, cancellationToken);

	/// <summary>
	/// Asynchronously executes all entries and writes their results to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IEnumerable<Func<T>> source,
		bool complete = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		return WriteAllAsync(
			target,
			source.WrapValueTask(),
			complete,
			deferredExecution,
			cancellationToken);
	}

	/// <summary>
	/// Asynchronously executes all entries and writes their results to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IEnumerable<Func<T>> source,
		bool complete,
		CancellationToken cancellationToken)
		=> WriteAllAsync(target, source, complete, false, cancellationToken);

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAll<T>(
		this ChannelWriter<T> target,
		IEnumerable<T> source,
		bool complete = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		return WriteAllAsync(
			target,
			source.WrapValueTask(),
			complete,
			deferredExecution,
			cancellationToken);
	}

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAll<T>(
		this ChannelWriter<T> target,
		IEnumerable<T> source,
		bool complete,
		CancellationToken cancellationToken)
		=> WriteAll(target, source, complete, false, cancellationToken);

	/// <summary>
	/// Consumes all lines from a TextReader and writes them to a channel.
	/// </summary>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The text reader to consume from.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	[SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly", Justification = "Is used correctly.")]
	public static async ValueTask<long> WriteAllLines(
		this ChannelWriter<string> target,
		TextReader source,
		bool complete = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		try
		{
			ValueTask next = target.WaitToWriteAndThrowIfClosedAsync(ChannelClosedMessage, deferredExecution, cancellationToken);
			await next.ConfigureAwait(false);

			long count = 0;
			bool more = false; // if it completed and actually returned false, no need to bubble the cancellation since it actually completed.
			while (!cancellationToken.IsCancellationRequested)
			{
#if NET8_0_OR_GREATER
				string? line = await source.ReadLineAsync(cancellationToken).ConfigureAwait(false);
#else
				string? line = await source.ReadLineAsync().ConfigureAwait(false);
#endif
				// the following is written this way to help null analysis.
				if (line is null)
				{
					more = false;
					break;
				}
				else
				{
					more = true;
				}

				await next.ConfigureAwait(false);
				count++;
				next = target.WriteAsync(line, cancellationToken);
			}
			await next.ConfigureAwait(false);
			if (more) cancellationToken.ThrowIfCancellationRequested();
			return count;
		}
		catch (ChannelClosedException) { throw; }
		catch (Exception ex)
		{
			if (complete)
			{
				target.TryComplete(ex);
				complete = false;
			}
			throw;
		}
		finally
		{
			if (complete)
				target.TryComplete();
		}
	}

	/// <summary>
	/// Consumes all lines from a TextReader and writes them to a channel.
	/// </summary>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The text reader to consume from.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllLines(
		this ChannelWriter<string> target,
		TextReader source,
		bool complete,
		CancellationToken cancellationToken)
		=> WriteAllLines(target, source, complete, false, cancellationToken);

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static async ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IAsyncEnumerable<T> source,
		bool complete = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		try
		{
			await target
				.WaitToWriteAndThrowIfClosedAsync(ChannelClosedMessage, deferredExecution, cancellationToken)
				.ConfigureAwait(false);

			long count = 0;
			ValueTask next = default;
			await foreach (T? value in source.ConfigureAwait(false))
			{
				await next.ConfigureAwait(false);
				count++;
				next = target.WriteAsync(value, cancellationToken);
			}
			await next.ConfigureAwait(false);
			return count;
		}
		catch (ChannelClosedException) { throw; }
		catch (Exception ex)
		{
			if (complete)
			{
				target.TryComplete(ex);
				complete = false;
			}
			throw;
		}
		finally
		{
			if (complete)
				target.TryComplete();
		}
	}

	/// <summary>
	/// Asynchronously writes all entries from the source to the channel.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is empty).</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>A task containing the count of items written that completes when all the data has been written to the channel writer.
	/// The count should be ignored if the number of iterations could exceed the max value of long.</returns>
	public static ValueTask<long> WriteAllAsync<T>(
		this ChannelWriter<T> target,
		IAsyncEnumerable<T> source,
		bool complete,
		CancellationToken cancellationToken)
		=> WriteAllAsync(target, source, complete, false, cancellationToken);
}
