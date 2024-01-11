namespace Open.ChannelExtensions;

/// <summary>
/// A ChannelReader that batches results.
/// Use the .Batch extension instead of constructing this directly.
/// </summary>
public abstract class BatchingChannelReader<T, TBatch> : BufferingChannelReader<T, TBatch>
	where TBatch : class
{
	private readonly int _batchSize;
	private TBatch? _batch;

	/// <summary>
	/// Constructs a BatchingChannelReader.
	/// Use the .Batch extension instead of constructing this directly.
	/// </summary>
	protected BatchingChannelReader(
		ChannelReader<T> source,
		int batchSize,
		bool singleReader,
		bool syncCont = false)
		: base(source, singleReader, syncCont)
	{
		if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least 1.");
		Contract.EndContractBlock();

		_batchSize = batchSize;
	}

	/// <summary>
	/// If no full batch is waiting, will force buffering any batch that has at least one item.
	/// Returns true if anything was added to the buffer.
	/// </summary>
	public bool ForceBatch() => TryPipeItems(true);

	void ForceBatch(object obj) => ForceBatch();

	long _timeout = -1;
	Timer? _timer;

	/// <summary>
	/// Specifies a timeout by which a batch will be emmited there is at least one item but has been waiting
	/// for longer than the timeout value.
	/// </summary>
	/// <param name="millisecondsTimeout">
	/// The timeout value where after a batch is forced.<br/>
	/// A value of zero or less cancels/clears any timeout.
	/// </param>
	/// <returns>The current reader.</returns>
	public BatchingChannelReader<T, TBatch> WithTimeout(long millisecondsTimeout)
	{
		_timeout = millisecondsTimeout <= 0 ? Timeout.Infinite : millisecondsTimeout;

		if (Buffer?.Reader.Completion.IsCompleted != false)
			return this;

		if (_timeout == Timeout.Infinite)
		{
			Interlocked.Exchange(ref _timer, null)?.Dispose();
			return this;
		}

		LazyInitializer.EnsureInitialized(ref _timer,
			() => new Timer(ForceBatch));

		if (_batch is null) return this;

		// Might be in the middle of a batch so we need to update the timeout.
		lock (Buffer)
		{
			if (_batch is not null) RefreshTimeout();
		}

		return this;
	}

	/// <summary>
	/// If one exists, updates the timer's timeout value.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected void RefreshTimeout() => TryUpdateTimer(_timeout);

	private void TryUpdateTimer(long timeout)
	{
		try
		{
			var ok = _timer?.Change(timeout, 0);
			Debug.Assert(ok ?? true);
		}
		catch (ObjectDisposedException)
		{
			// Rare instance where another thread has disposed the timer before .Change can be called.
		}
	}

	/// <param name="timeout">
	/// The timeout value where after a batch is forced.<br/>
	/// A value of zero or less cancels/clears any timeout.<br/>
	/// Note: Values are converted to milliseconds.
	/// </param>
	/// <inheritdoc cref="WithTimeout(long)"/>
	public BatchingChannelReader<T, TBatch> WithTimeout(TimeSpan timeout)
		=> WithTimeout((long)timeout.TotalMilliseconds);

	/// <inheritdoc />
	protected override void OnBeforeFinalFlush()
		=> Interlocked.Exchange(ref _timer, null)?.Dispose();

	/// <summary>
	/// Creates a batch for consumption.
	/// </summary>
	protected abstract TBatch CreateBatch(int capacity);

	/// <summary>
	/// Trims the excess capacity of a batch before releasing.
	/// </summary>
	protected abstract void TrimBatch(TBatch batch);

	/// <summary>
	/// Adds an item to the batch.
	/// </summary>
	protected abstract void AddBatchItem(TBatch batch, T item);

	/// <summary>
	/// Adds an item to the batch.
	/// </summary>
	protected abstract int GetBatchSize(TBatch batch);

	/// <inheritdoc />
	protected override bool TryPipeItems(bool flush)
	{
		if (Buffer?.Reader.Completion.IsCompleted != false)
			return false;

		lock (Buffer)
		{
			if (Buffer.Reader.Completion.IsCompleted) return false;

			var batched = false;
			var newBatch = false;
			TBatch? c = _batch;
			ChannelReader<T>? source = Source;
			if (source?.Completion.IsCompleted != false)
			{
				// All finished, if necessary, release the last batch to the buffer.
				if (c is null) return false;
				goto flushBatch;
			}

			while (source.TryRead(out T? item))
			{
				if (c is null)
				{
					newBatch = true; // a new batch could start but not be emmited.
					_batch = c = CreateBatch(_batchSize);
					AddBatchItem(c, item);
				}
				else
				{
					AddBatchItem(c, item);
				}

				Debug.Assert(GetBatchSize(c) <= _batchSize);
				var full = GetBatchSize(c) == _batchSize;
				while (!full && source.TryRead(out item))
				{
					AddBatchItem(c, item);
					full = GetBatchSize(c) == _batchSize;
				}

				if (!full) break;

				Emit(ref c);

				if (!flush)
					goto finalizeTimer;
			}

			if (!flush || c is null)
				goto finalizeTimer;

			flushBatch:

			TrimBatch(c);
			Emit(ref c);

		finalizeTimer:

			// Are we adding to the existing batch (active timeout) or did we create a new one?
			if (newBatch && _batch is not null) RefreshTimeout();

			return batched;

			void Emit(ref TBatch? c)
			{
				_batch = null;
				newBatch = false;
				if (!batched) TryUpdateTimer(Timeout.Infinite); // Since we're emmitting one, let's ensure the timeout is cancelled.
				batched = Buffer!.Writer.TryWrite(c!);
				Debug.Assert(batched);
				c = null;
			}
		}
	}

	/// <inheritdoc />
	protected override async ValueTask<bool> WaitToReadAsyncCore(
		ValueTask<bool> bufferWait,
		CancellationToken cancellationToken)
	{
		ChannelReader<T>? source = Source;

		if (source is null || bufferWait.IsCompleted)
			return await bufferWait.ConfigureAwait(false);

		var b = bufferWait.AsTask();
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken token = tokenSource.Token;

	start:

		ValueTask<bool> s = source.WaitToReadAsync(token);
		if (s.IsCompleted && !b.IsCompleted) TryPipeItems(false);

		if (b.IsCompleted)
		{
			tokenSource.Cancel();
			return await b.ConfigureAwait(false);
		}

		// WhenAny will not throw when a task is cancelled.
		await Task.WhenAny(s.AsTask(), b).ConfigureAwait(false);
		if (b.IsCompleted) // Assuming it was bufferWait that completed.
		{
			tokenSource.Cancel();
			return await b.ConfigureAwait(false);
		}

		TryPipeItems(false);

		if (b.IsCompleted || token.IsCancellationRequested)
			return await b.ConfigureAwait(false);

		goto start;
	}
}

/// <inheritdoc />
public class QueueBatchingChannelReader<T> : BatchingChannelReader<T, Queue<T>>
{
	/// <inheritdoc />
	public QueueBatchingChannelReader(ChannelReader<T> source, int batchSize, bool singleReader, bool syncCont = false)
		: base(source, batchSize, singleReader, syncCont) { }

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void AddBatchItem(Queue<T> batch, T item) => batch.Enqueue(item);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override Queue<T> CreateBatch(int capacity) => new(capacity);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override int GetBatchSize(Queue<T> batch) => batch.Count;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void TrimBatch(Queue<T> batch) => batch!.TrimExcess();
}

/// <inheritdoc />
public class BatchingChannelReader<T> : BatchingChannelReader<T, List<T>>
{
	/// <inheritdoc />
	public BatchingChannelReader(ChannelReader<T> source, int batchSize, bool singleReader, bool syncCont = false)
		: base(source, batchSize, singleReader, syncCont) { }

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void AddBatchItem(List<T> batch, T item) => batch.Add(item);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override List<T> CreateBatch(int capacity) => new(capacity);

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override int GetBatchSize(List<T> batch) => batch.Count;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void TrimBatch(List<T> batch) => batch!.TrimExcess();
}