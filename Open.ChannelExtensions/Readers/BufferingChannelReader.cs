namespace Open.ChannelExtensions;

/// <summary>
/// Base class for buffering results of a source ChannelReader.
/// </summary>
/// <typeparam name="TIn">The input type of the buffer.</typeparam>
/// <typeparam name="TOut">The output type of the buffer.</typeparam>
public abstract class BufferingChannelReader<TIn, TOut> : ChannelReader<TOut>
{
	/// <summary>
	/// The source of the buffer.
	/// </summary>
	protected ChannelReader<TIn>? Source { get; set; }

	/// <summary>
	/// The internal channel used for buffering.
	/// </summary>
	protected Channel<TOut>? Buffer { get; }

	/// <summary>
	/// Base constructor for a BufferingChannelReader.
	/// </summary>
	protected BufferingChannelReader(ChannelReader<TIn> source, bool singleReader, bool syncCont = false)
	{
		Source = source ?? throw new ArgumentNullException(nameof(source));
		Contract.EndContractBlock();

		if (source.Completion.IsCompleted)
		{
			Buffer = null;
			_completion = source.Completion;
		}
		else
		{
			Buffer = Extensions.CreateChannel<TOut>(-1, singleReader, syncCont);
			_completion = Buffer.Reader.Completion;

			source.Completion.ContinueWith(t =>
			{
				OnBeforeFinalFlush();
				// Need to be sure writing is done before we continue...
				lock (Buffer)
				{
					/* When the source is complete,
					 * we dump all remaining into the buffer 
					 * in order to propagate the completion and any exception. */
					TryPipeItems(true);
					Buffer.Writer.Complete(t.Exception);
				}

				Source = null;
			}, TaskScheduler.Current);
		}
	}

	/// <summary>
	/// Called before the last items are flushed to the buffer.
	/// </summary>
	protected virtual void OnBeforeFinalFlush()
	{ }

	private readonly Task _completion;
	/// <inheritdoc />
	public override Task Completion => _completion;

	/// <summary>
	/// The method that triggers adding entries to the buffer.
	/// </summary>
	/// <param name="flush">Signals that all items should be piped.</param>
	/// <returns>True if items were transferred.</returns>
	protected abstract bool TryPipeItems(bool flush);

	/// <inheritdoc />
	public override bool TryRead(out TOut item)
	{
		if (Buffer is not null)
		{
			do
			{
				if (Buffer.Reader.TryRead(out TOut? i))
				{
					item = i;
					return true;
				}
			}
			while (TryPipeItems(false));
		}

		item = default!;
		return false;
	}

	/// <inheritdoc />
	public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
	{
		Channel<TOut>? buffer = Buffer;
		Task completion = buffer is null ? _completion : buffer.Reader.Completion;
		if (buffer is null || completion.IsCompleted)
		{
			return completion.IsFaulted
				? new ValueTask<bool>(Task.FromException<bool>(completion.Exception!))
				: new ValueTask<bool>(false);
		}

		if (cancellationToken.IsCancellationRequested)
			return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));

		ValueTask<bool> b = buffer.Reader.WaitToReadAsync(cancellationToken);
		return b.IsCompleted ? b : WaitToReadAsyncCore(b, cancellationToken);
	}

	/// <summary>
	/// Implementation for waiting.
	/// Can be overridden.
	/// </summary>
	protected virtual async ValueTask<bool> WaitToReadAsyncCore(ValueTask<bool> bufferWait, CancellationToken cancellationToken)
	{
		ChannelReader<TIn>? source = Source;
		if (source is null || bufferWait.IsCompleted)
			return await bufferWait.ConfigureAwait(false);

		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken token = tokenSource.Token;

	start:

		ValueTask<bool> s = source.WaitToReadAsync(token);
		if (s.IsCompleted && !bufferWait.IsCompleted) TryPipeItems(false);

		if (bufferWait.IsCompleted)
		{
			await tokenSource.CancelAsync().ConfigureAwait(false);
			return await bufferWait.ConfigureAwait(false);
		}

		await s.ConfigureAwait(false);
		if (bufferWait.IsCompleted) return await bufferWait.ConfigureAwait(false);
		TryPipeItems(false);
		if (bufferWait.IsCompleted) return await bufferWait.ConfigureAwait(false);

		goto start;
	}
}
