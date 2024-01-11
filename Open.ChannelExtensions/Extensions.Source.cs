using System.IO;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Executes all entries from the source and passes their result to the channel.  Calls complete when finished.
	/// </summary>
	/// <typeparam name="TWrite">The input type of the channel.</typeparam>
	/// <typeparam name="TRead">The output type of the channel.</typeparam>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="completion">The underlying ValueTask used to pass the data from the source to the channel.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <remarks>Calling this method does not throw if the channel is already closed.</remarks>
	/// <returns>The channel reader.</returns>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<Func<TWrite>> source,
		out ValueTask<long> completion,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		completion = target.Writer
			.WriteAllAsync(source, true, deferredExecution, cancellationToken);

		return target.Reader;
	}

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Func{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<Func<TWrite>> source,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> SourceAsync(target, source, out _, deferredExecution, cancellationToken);

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Func{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<Func<TWrite>> source,
		CancellationToken cancellationToken)
		=> SourceAsync(target, source, out _, false, cancellationToken);

	/// <summary>
	/// Awaits all entries from the source and passes their result to the channel.  Calls complete when finished.
	/// </summary>
	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Func{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<ValueTask<TWrite>> source,
		out ValueTask<long> completion,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		completion = target.Writer
			.WriteAllAsync(source, true, deferredExecution, cancellationToken);

		return target.Reader;
	}

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{ValueTask{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<ValueTask<TWrite>> source,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> SourceAsync(target, source, out _, deferredExecution, cancellationToken);

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{ValueTask{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<ValueTask<TWrite>> source,
		CancellationToken cancellationToken)
		=> SourceAsync(target, source, out _, false, cancellationToken);

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{ValueTask{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<Task<TWrite>> source,
		out ValueTask<long> completion,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		completion = target.Writer
			.WriteAllAsync(source, true, deferredExecution, cancellationToken);

		return target.Reader;
	}

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Task{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<Task<TWrite>> source,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> SourceAsync(target, source, out _, deferredExecution, cancellationToken);

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Task{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<Task<TWrite>> source,
		CancellationToken cancellationToken)
		=> SourceAsync(target, source, out _, false, cancellationToken);

	/// <summary>
	/// Writes all entries from the source to the channel.  Calls complete when finished.
	/// </summary>
	/// <param name="target">The channel to write to.</param>
	/// <param name="source">The source data to use.</param>
	/// <param name="completion">The underlying ValueTask used to pass the data from the source to the channel.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Func{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> Source<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<TWrite> source,
		out ValueTask<long> completion,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		if (source is null) throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		completion = target.Writer
			.WriteAll(source, true, deferredExecution, cancellationToken);

		return target.Reader;
	}

	/// <inheritdoc cref="Source{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{TWrite}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> Source<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<TWrite> source,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> Source(target, source, out _, deferredExecution, cancellationToken);

	/// <inheritdoc cref="Source{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{TWrite}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> Source<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IEnumerable<TWrite> source,
		CancellationToken cancellationToken)
		=> Source(target, source, out _, false, cancellationToken);

	/// <param name="target">The channel to write to.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{Func{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		int maxConcurrency,
		IEnumerable<Func<TWrite>> source,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		if (maxConcurrency == 1)
			return SourceAsync(target, source, true, cancellationToken);

		target.Writer.WriteAllConcurrentlyAsync(maxConcurrency, source, true, cancellationToken).ConfigureAwait(false);
		return target.Reader;
	}

	/// <summary>
	/// Awaits all entries from the source and passes their result to the channel.  Calls complete when finished.
	/// </summary>
	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, int, IEnumerable{Func{TWrite}}, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		int maxConcurrency,
		IEnumerable<ValueTask<TWrite>> source,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		if (maxConcurrency == 1)
			return SourceAsync(target, source, true, cancellationToken);

		target.Writer
			.WriteAllConcurrentlyAsync(maxConcurrency, source, true, cancellationToken)
			.ConfigureAwait(false);

		return target.Reader;
	}

	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, int, IEnumerable{ValueTask{TWrite}}, CancellationToken)"/>
	public static ChannelReader<TRead> SourceAsync<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		int maxConcurrency,
		IEnumerable<Task<TWrite>> source,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		if (maxConcurrency == 1)
			return target.SourceAsync(source, true, cancellationToken);

		target.Writer
			.WriteAllConcurrentlyAsync(maxConcurrency, source, true, cancellationToken)
			.ConfigureAwait(false);

		return target.Reader;
	}

	/// <typeparam name="T">The output type of the channel.</typeparam>
	/// <inheritdoc cref="Source{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{TWrite}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<T> Source<T>(
		this Channel<string, T> target,
		TextReader source,
		out ValueTask<long> completion,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		completion = target.Writer
			.WriteAllLines(source, true, deferredExecution, cancellationToken);

		return target.Reader;
	}

	/// <inheritdoc cref="Source{T}(Channel{string, T}, TextReader, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<T> Source<T>(
		this Channel<string, T> target,
		TextReader source,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> Source(target, source, out _, deferredExecution, cancellationToken);

	/// <inheritdoc cref="Source{T}(Channel{string, T}, TextReader, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<T> Source<T>(
		this Channel<string, T> target,
		TextReader source,
		CancellationToken cancellationToken)
		=> Source(target, source, out _, false, cancellationToken);

#if NETSTANDARD2_1
	/// <inheritdoc cref="SourceAsync{TWrite, TRead}(Channel{TWrite, TRead}, IEnumerable{ValueTask{TWrite}}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> Source<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IAsyncEnumerable<TWrite> source,
		out ValueTask<long> completion,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
	{
		if (target is null) throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		completion = target.Writer
			.WriteAllAsync(source, true, deferredExecution, cancellationToken);

		return target.Reader;
	}

	/// <inheritdoc cref="Source{TWrite, TRead}(Channel{TWrite, TRead}, IAsyncEnumerable{TWrite}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> Source<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IAsyncEnumerable<TWrite> source,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> Source(target, source, out _, deferredExecution, cancellationToken);

	/// <inheritdoc cref="Source{TWrite, TRead}(Channel{TWrite, TRead}, IAsyncEnumerable{TWrite}, out ValueTask{long}, bool, CancellationToken)"/>
	public static ChannelReader<TRead> Source<TWrite, TRead>(
		this Channel<TWrite, TRead> target,
		IAsyncEnumerable<TWrite> source,
		CancellationToken cancellationToken)
		=> Source(target, source, false, cancellationToken);
#endif
}
