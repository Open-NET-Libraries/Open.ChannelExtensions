using System.IO;

[assembly: CLSCompliant(true)]
namespace Open.ChannelExtensions;

/// <summary>
/// Extensions for operating with System.Threading.Channels.
/// </summary>
[SuppressMessage("RCS1047FadeOut",
	"RCS1047FadeOut: Async Naming",
	Justification = "Some methods use the Async suffix to distinguish between the contents of their parameters.")]
public static partial class Extensions
{
	internal static Channel<T> CreateChannel<T>(ChannelOptions channelOptions)
		=> channelOptions is BoundedChannelOptions bco
		? Channel.CreateBounded<T>(bco)
		: channelOptions is UnboundedChannelOptions ubco
			? Channel.CreateUnbounded<T>(ubco)
			: throw new ArgumentException("Unsupported channel option type.", nameof(channelOptions));

	internal static Channel<T> CreateChannel<T>(int capacity = -1, bool singleReader = false, bool syncCont = false, bool singleWriter = true)
		=> capacity > 0
			? Channel.CreateBounded<T>(new BoundedChannelOptions(capacity)
			{
				SingleWriter = singleWriter,
				SingleReader = singleReader,
				AllowSynchronousContinuations = syncCont,
				FullMode = BoundedChannelFullMode.Wait
			})
			: Channel.CreateUnbounded<T>(new UnboundedChannelOptions
			{
				SingleWriter = singleWriter,
				SingleReader = singleReader,
				AllowSynchronousContinuations = syncCont
			});

	static async ValueTask ThrowChannelClosedExceptionIfFalse(ValueTask<bool> write, string? message = null)
	{
		if (!await write.ConfigureAwait(false))
		{
			if (string.IsNullOrWhiteSpace(message)) throw new ChannelClosedException();
			throw new ChannelClosedException(message);
		}
	}

#if NET8_0_OR_GREATER
#else
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static ValueTask CancelAsync(this CancellationTokenSource source)
	{
		source.Cancel();
		return new ValueTask();
	}
#endif

	// Avoid potential lambda allocation.
	internal static IEnumerable<ValueTask<T>> WrapValueTask<T>(this IEnumerable<T> source)
	{
		foreach (var e in source)
			yield return new ValueTask<T>(e);
	}

	internal static IEnumerable<ValueTask<T>> WrapValueTask<T>(this IEnumerable<Task<T>> source)
	{
		foreach (var e in source)
			yield return new ValueTask<T>(e);
	}

	internal static IEnumerable<ValueTask<T>> WrapValueTask<T>(this IEnumerable<Func<T>> source)
	{
		foreach (var e in source)
			yield return new ValueTask<T>(e());
	}

	/// <summary>
	/// Will propagate the completion of the <paramref name="source"/> channel reader to the <paramref name="target"/> channel writer.
	/// </summary>
	/// <remarks>If the <paramref name="cancellationToken"/> is cancelled, the propagation will not occur.</remarks>
	/// <returns>The source reader.</returns>
	public static ChannelReader<TSource> PropagateCompletion<TSource, TTarget>(
		this ChannelReader<TSource> source, ChannelWriter<TTarget> target, CancellationToken cancellationToken = default)
	{
		if (source is null) throw new ArgumentNullException(nameof(source));
		if (target is null) throw new ArgumentNullException(nameof(target));
		Contract.EndContractBlock();

		if (cancellationToken.IsCancellationRequested)
			return source;

		source.Completion.ContinueWith(t => {
			if (t.IsFaulted)
				target.TryComplete(t.Exception);
			else
				target.TryComplete();
		}, cancellationToken);

		return source;
	}

	/// <inheritdoc cref="PropagateCompletion{TSource, TTarget}(ChannelReader{TSource}, ChannelWriter{TTarget}, CancellationToken)"/>
	[ExcludeFromCodeCoverage]
	public static ChannelReader<TSource> PropagateCompletion<TSource, TTargetIn, TTargetOut>(
		this ChannelReader<TSource> source, Channel<TTargetIn, TTargetOut> target, CancellationToken cancellationToken = default)
		=> PropagateCompletion(source, target.Writer, cancellationToken);

	/// <inheritdoc cref="PropagateCompletion{TSource, TTarget}(ChannelReader{TSource}, ChannelWriter{TTarget}, CancellationToken)"/>
	[ExcludeFromCodeCoverage]
	public static Channel<TSourceIn, TSourceOut> PropagateCompletion<TSourceIn, TSourceOut, TTarget>(
		this Channel<TSourceIn, TSourceOut> source, ChannelWriter<TTarget> target, CancellationToken cancellationToken = default)
	{
		_ = source.Reader.PropagateCompletion(target, cancellationToken);
		return source;
	}

	/// <inheritdoc cref="PropagateCompletion{TSource, TTarget}(ChannelReader{TSource}, ChannelWriter{TTarget}, CancellationToken)"/>/>
	[ExcludeFromCodeCoverage]
	public static Channel<TSourceIn, TSourceOut> PropagateCompletion<TSourceIn, TSourceOut, TTargetIn, TTargetOut>(
		this Channel<TSourceIn, TSourceOut> source, Channel<TTargetIn, TTargetOut> target, CancellationToken cancellationToken = default)
		=> PropagateCompletion(source, target.Writer, cancellationToken);

	/// <summary>
	/// Uses <see cref="ChannelWriter{T}.WaitToWriteAsync(CancellationToken)"/> to peek and see if the channel can still be written to.
	/// </summary>
	/// <typeparam name="T">The type being written to the channel</typeparam>
	/// <param name="writer">The channel writer.</param>
	/// <param name="ifClosedMessage">The message to include with the ChannelClosedException if thrown.</param>
	/// <exception cref="ChannelClosedException">If the channel writer will no longer accept messages.</exception>
	public static void ThrowIfClosed<T>(this ChannelWriter<T> writer, string? ifClosedMessage = null)
	{
		if (writer is null) throw new ArgumentNullException(nameof(writer));
		ValueTask<bool> waitForWrite = writer.WaitToWriteAsync();
		if (!waitForWrite.IsCompletedSuccessfully || waitForWrite.Result)
			return;

		if (string.IsNullOrWhiteSpace(ifClosedMessage))
			throw new ChannelClosedException();

		throw new ChannelClosedException(ifClosedMessage);
	}

	/// <summary>
	/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.
	/// </summary>
	/// <typeparam name="T">The type being written to the channel</typeparam>
	/// <param name="writer">The channel writer.</param>
	/// <param name="ifClosedMessage">The message to include with the ChannelClosedException if thrown.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	public static ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, string? ifClosedMessage = null, CancellationToken cancellationToken = default)
	{
		if (writer is null) throw new ArgumentNullException(nameof(writer));
		Contract.EndContractBlock();

		if (cancellationToken.IsCancellationRequested)
			return new ValueTask(Task.FromCanceled(cancellationToken));

		ValueTask<bool> waitForWrite = writer.WaitToWriteAsync(cancellationToken);
		if (!waitForWrite.IsCompletedSuccessfully)
			return ThrowChannelClosedExceptionIfFalse(waitForWrite, ifClosedMessage);

		if (waitForWrite.Result)
			return new ValueTask();

		if (string.IsNullOrWhiteSpace(ifClosedMessage)) throw new ChannelClosedException();
		throw new ChannelClosedException(ifClosedMessage);
	}

	/// <summary>
	/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.
	/// </summary>
	/// <typeparam name="T">The type being written to the channel</typeparam>
	/// <param name="writer">The channel writer.</param>
	/// <param name="ifClosedMessage">The message to include with the ChannelClosedException if thrown.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before continuing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	public static async ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, string ifClosedMessage, bool deferredExecution, CancellationToken cancellationToken = default)
	{
		ValueTask wait = writer.WaitToWriteAndThrowIfClosedAsync(ifClosedMessage, cancellationToken);

		if (deferredExecution)
		{
			await Task.Yield();
			if (wait.IsCompletedSuccessfully)
				wait = writer.WaitToWriteAndThrowIfClosedAsync(ifClosedMessage, cancellationToken);
		}

		await wait.ConfigureAwait(false);
	}

	/// <summary>
	/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.
	/// </summary>
	/// <typeparam name="T">The type being written to the channel</typeparam>
	/// <param name="writer">The channel writer.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	public static ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, CancellationToken cancellationToken)
		=> WaitToWriteAndThrowIfClosedAsync(writer, null, cancellationToken);

	/// <summary>
	/// Waits for opportunity to write to a channel and throws a ChannelClosedException if the channel is closed.
	/// </summary>
	/// <typeparam name="T">The type being written to the channel</typeparam>
	/// <param name="writer">The channel writer.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before continuing.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	public static async ValueTask WaitToWriteAndThrowIfClosedAsync<T>(this ChannelWriter<T> writer, bool deferredExecution, CancellationToken cancellationToken = default)
	{
		ValueTask wait = writer.WaitToWriteAndThrowIfClosedAsync(null, cancellationToken);

		if (deferredExecution)
		{
			await Task.Yield();
			if (wait.IsCompletedSuccessfully)
				wait = writer.WaitToWriteAndThrowIfClosedAsync(null, cancellationToken);
		}

		await wait.ConfigureAwait(false);
	}

	/// <summary>
	/// Calls complete on the writer and returns the completion from the reader.
	/// </summary>
	/// <typeparam name="TWrite">The type being received by the writer.</typeparam>
	/// <typeparam name="TRead">The type being read from the reader.</typeparam>
	/// <param name="channel">The channel to complete asynchronously.</param>
	/// <param name="exception">The optional exception to include with completion.</param>
	/// <returns>The reader's completion task.</returns>
	public static Task CompleteAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel, Exception? exception = null)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		channel.Writer.Complete(exception);
		return channel.Reader.Completion;
	}

	/// <summary>
	/// Writes all lines from the source to a channel and calls complete when finished.
	/// </summary>
	/// <param name="source">The source data to use.</param>
	/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The channel reader containing the results.</returns>
	public static ChannelReader<string> ToChannel(this TextReader source,
		int capacity = -1, bool singleReader = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> CreateChannel<string>(capacity, singleReader)
			.Source(source, deferredExecution, cancellationToken);

	/// <summary>
	/// Writes all lines from the source to a channel and calls complete when finished.
	/// </summary>
	/// <param name="source">The source data to use.</param>
	/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <returns>The channel reader containing the results.</returns>
	public static ChannelReader<string> ToChannel(this TextReader source,
		int capacity, bool singleReader,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> CreateChannel<string>(capacity, singleReader)
			.Source(source, deferredExecution, cancellationToken);

	/// <summary>
	/// Writes all entries from the source to a channel and calls complete when finished.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="source">The source data to use.</param>
	/// <param name="channelOptions">The options for configuring the new channel.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The channel reader containing the results.</returns>
	public static ChannelReader<T> ToChannel<T>(this IEnumerable<T> source,
		ChannelOptions channelOptions,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(channelOptions)
			.Source(source, deferredExecution, cancellationToken);

	/// <summary>
	/// Writes all entries from the source to a channel and calls complete when finished.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="source">The source data to use.</param>
	/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The channel reader containing the results.</returns>
	public static ChannelReader<T> ToChannel<T>(this IEnumerable<T> source,
		int capacity = -1, bool singleReader = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(capacity, singleReader)
			.Source(source, deferredExecution, cancellationToken);

	/// <summary>
	/// Writes all entries from the source to a channel and calls complete when finished.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="source">The source data to use.</param>
	/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <returns>The channel reader containing the results.</returns>
	public static ChannelReader<T> ToChannel<T>(this IEnumerable<T> source,
		int capacity, bool singleReader,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> CreateChannel<T>(capacity, singleReader)
			.Source(source, deferredExecution, cancellationToken);

#if NETSTANDARD2_0
#else
	/// <summary>
	/// Writes all entries from the source to a channel and calls complete when finished.
	/// </summary>
	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, int, bool, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannel<T>(this IAsyncEnumerable<T> source,
		int capacity = -1, bool singleReader = false,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(capacity, singleReader)
			.Source(source, deferredExecution, cancellationToken);

	/// <summary>
	/// Writes all entries from the source to a channel and calls complete when finished.
	/// </summary>
	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, ChannelOptions, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannel<T>(this IAsyncEnumerable<T> source,
		ChannelOptions channelOptions,
		bool deferredExecution = false,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(channelOptions)
			.Source(source, deferredExecution, cancellationToken);

	/// <param name="source">The source data to use.</param>
	/// <param name="capacity">The optional bounded capacity of the channel.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <param name="deferredExecution">If true, calls await Task.Yield() before writing to the channel.</param>
	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, int, bool, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannel<T>(this IAsyncEnumerable<T> source,
		int capacity, bool singleReader,
		CancellationToken cancellationToken,
		bool deferredExecution = false)
		=> CreateChannel<T>(capacity, singleReader)
			.Source(source, deferredExecution, cancellationToken);

	/// <summary>
	/// Iterates over the results in a ChannelReader.
	/// Provided as an alternative to .ReadAllAsync() which at the time of publishing this, only exists in .NET Core 3.0 and not .NET Standard 2.1
	/// </summary>
	/// <typeparam name="T">The output type of the channel.</typeparam>
	/// <param name="reader">The reader to read from.</param>
	/// <param name="cancellationToken">An optional cancellation token that will break out of the iteration.</param>
	/// <returns>An IAsyncEnumerable for iterating the channel.</returns>
	public static IAsyncEnumerable<T> AsAsyncEnumerable<T>(this ChannelReader<T> reader, CancellationToken cancellationToken = default)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		Contract.EndContractBlock();

		return AsAsyncEnumerableCore(reader, cancellationToken);

		static async IAsyncEnumerable<T> AsAsyncEnumerableCore(ChannelReader<T> reader, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			do
			{
				while (!cancellationToken.IsCancellationRequested && reader.TryRead(out T? item))
					yield return item;
			}
			while (
				!cancellationToken.IsCancellationRequested
				&& await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));
		}
	}

	/// <summary>
	/// Iterates over the results in a Channel.
	/// Provided as an alternative to .ReadAllAsync() which at the time of publishing this, only exists in .NET Core 3.0 and not .NET Standard 2.1
	/// </summary>
	/// <typeparam name="TIn">The type received by the source channel.</typeparam>
	/// <typeparam name="TOut">The outgoing type from the source channel.</typeparam>
	/// <param name="channel">The reader to read from.</param>
	/// <param name="cancellationToken">An optional cancellation token that will break out of the iteration.</param>
	/// <returns>An IAsyncEnumerable for iterating the channel.</returns>
	public static IAsyncEnumerable<TOut> AsAsyncEnumerable<TIn, TOut>(this Channel<TIn, TOut> channel, CancellationToken cancellationToken = default)
	{
		if (channel is null) throw new ArgumentNullException(nameof(channel));
		Contract.EndContractBlock();

		return AsAsyncEnumerableCore(channel, cancellationToken);

		static async IAsyncEnumerable<TOut> AsAsyncEnumerableCore(Channel<TIn, TOut> channel, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			ChannelReader<TOut>? reader = channel.Reader;
			do
			{
				while (!cancellationToken.IsCancellationRequested && reader.TryRead(out TOut? item))
					yield return item;
			}
			while (
				!cancellationToken.IsCancellationRequested
				&& await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false));
		}
	}
#endif

	/// <summary>
	/// Asynchronously executes all entries and writes their results to a channel.
	/// </summary>
	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, ChannelOptions, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Func<T>> source,
		ChannelOptions channelOptions, int maxConcurrency = 1,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(channelOptions)
			.SourceAsync(maxConcurrency, source, cancellationToken);

	/// <summary>
	/// Asynchronously executes all entries and writes their results to a channel.
	/// </summary>
	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, int, bool, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Func<T>> source,
		int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(capacity, singleReader)
			.SourceAsync(maxConcurrency, source, cancellationToken);

	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="channelOptions">The options for configuring the new channel.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, int, bool, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<ValueTask<T>> source,
		ChannelOptions channelOptions, int maxConcurrency = 1,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(channelOptions)
			.SourceAsync(maxConcurrency, source, cancellationToken);

	/// <summary>
	/// Writes all entries from the source to a channel and calls complete when finished.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="capacity">The optional bounded capacity of the channel. Default is unbound.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="cancellationToken">An optional cancellation token.</param>
	/// <returns>The channel reader containing the results.</returns>
	public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<ValueTask<T>> source,
		int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(capacity, singleReader)
			.SourceAsync(maxConcurrency, source, cancellationToken);

	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, ChannelOptions, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Task<T>> source,
		ChannelOptions channelOptions, int maxConcurrency = 1,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(channelOptions)
			.SourceAsync(maxConcurrency, source, cancellationToken);

	/// <inheritdoc cref="ToChannelAsync{T}(IEnumerable{ValueTask{T}}, int, bool, int, CancellationToken)"/>
	public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Task<T>> source,
		int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
		CancellationToken cancellationToken = default)
		=> CreateChannel<T>(capacity, singleReader)
			.SourceAsync(maxConcurrency, source, cancellationToken);
}
