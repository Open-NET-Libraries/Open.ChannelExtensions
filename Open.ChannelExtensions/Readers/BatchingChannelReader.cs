using System.Buffers;

namespace Open.ChannelExtensions;

/// <summary>
/// A ChannelReader that batches results.
/// Use the .Batch extension instead of constructing this directly.
/// </summary>
public abstract class BatchingChannelReader<T, TBatch>
	: BufferingChannelReader<T, TBatch>
	where TBatch : class
{
	private readonly int _batchSize;
	private TBatch? _batch;

	/// <summary>
	/// The delegate used to create new batches.
	/// </summary>
	protected Func<int, TBatch> BatchFactory { get; }

	/// <summary>
	/// Constructs a BatchingChannelReader.
	/// Use the .Batch extension instead of constructing this directly.
	/// </summary>
	protected BatchingChannelReader(
		Func<int, TBatch> batchFactory,
		ChannelReader<T> source,
		int batchSize,
		bool singleReader,
		bool syncCont = false)
		: base(source, singleReader, syncCont)
	{
		if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least 1.");
		BatchFactory = batchFactory ?? throw new ArgumentNullException(nameof(batchFactory));
		Contract.EndContractBlock();

		_batchSize = batchSize;
	}

	/// <summary>
	/// If no full batch is waiting, will force buffering any batch that has at least one item.
	/// Returns true if anything was added to the buffer.
	/// </summary>
	public bool ForceBatch() => TryPipeItems(true);

	private void ForceBatch(object? obj) => ForceBatch();

	private long _timeout = -1;
	private Timer? _timer;

	/// <summary>
	/// Specifies a timeout by which a batch will be emitted there is at least one item but has been waiting
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
		lock (SyncLock)
		{
			// Analyzer did not take into account thread safety.
#pragma warning disable CA1508 // Avoid dead conditional code
			if (_batch is not null) RefreshTimeout();
#pragma warning restore CA1508 // Avoid dead conditional code
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
		var t = _timer;;
		if (t is null) return;

		try
		{
			t.Change(timeout, 0);
			// bool? ok = t.Change(timeout, 0);
			// Debug.Assert(ok ?? true);
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
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected virtual TBatch CreateBatch(int capacity)
		=> BatchFactory(capacity);

	/// <summary>
	/// Trims the excess capacity of a batch before releasing.
	/// </summary>
	protected abstract void TrimBatch(ref TBatch batch, bool isVerifiedFull);

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

		lock (SyncLock)
		{
			if (Buffer.Reader.Completion.IsCompleted) return false;

			bool batched = false;
			bool newBatch = false;
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
					newBatch = true; // a new batch could start but not be emitted.
					_batch = c = CreateBatch(_batchSize);
					AddBatchItem(c, item);
				}
				else
				{
					AddBatchItem(c, item);
				}

				Debug.Assert(GetBatchSize(c) <= _batchSize);
				bool full = GetBatchSize(c) == _batchSize;
				while (!full && source.TryRead(out item))
				{
					AddBatchItem(c, item);
					full = GetBatchSize(c) == _batchSize;
				}

				if (!full) break;

				TrimBatch(ref c, full);
				Emit(ref c);

				if (!flush)
					goto finalizeTimer;
			}

			if (!flush || c is null)
				goto finalizeTimer;

			flushBatch:

			TrimBatch(ref c, false);
			Emit(ref c);

		finalizeTimer:

			// Are we adding to the existing batch (active timeout) or did we create a new one?
			if (newBatch && _batch is not null) RefreshTimeout();

			return batched;

			void Emit(ref TBatch? c)
			{
				_batch = null;
				newBatch = false;
				if (!batched) TryUpdateTimer(Timeout.Infinite); // Since we're emitting one, let's ensure the timeout is cancelled.
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

		Task<bool> b = bufferWait.AsTask();
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken token = tokenSource.Token;

	start:

		ValueTask<bool> s = source.WaitToReadAsync(token);
		if (s.IsCompleted && !b.IsCompleted) TryPipeItems(false);

		if (b.IsCompleted)
		{
			await tokenSource.CancelAsync().ConfigureAwait(false);
			return await b.ConfigureAwait(false);
		}

		// WhenAny will not throw when a task is cancelled.
		await Task.WhenAny(s.AsTask(), b).ConfigureAwait(false);
		if (b.IsCompleted) // Assuming it was bufferWait that completed.
		{
			await tokenSource.CancelAsync().ConfigureAwait(false);
			return await b.ConfigureAwait(false);
		}

		TryPipeItems(false);

		if (b.IsCompleted || token.IsCancellationRequested)
			return await b.ConfigureAwait(false);

		goto start;
	}
}

/// <inheritdoc />
public class QueueBatchingChannelReader<T>(
	ChannelReader<T> source,
	int batchSize,
	bool singleReader,
	bool syncCont = false,
	Func<int, Queue<T>>? batchFactory = null)
	: BatchingChannelReader<T, Queue<T>>(
		batchFactory ?? (static capacity => new Queue<T>(capacity)),
		source,
		batchSize,
		singleReader,
		syncCont)
{
	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void AddBatchItem(Queue<T> batch, T item)
	{
		Debug.Assert(batch is not null);
		batch.Enqueue(item);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override int GetBatchSize(Queue<T> batch)
	{
		Debug.Assert(batch is not null);
		return batch.Count;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void TrimBatch(ref Queue<T> batch, bool isVerifiedFull)
	{
		Debug.Assert(batch is not null);
		if (!isVerifiedFull) batch.TrimExcess();
	}
}

/// <inheritdoc />
public class BatchingChannelReader<T>(
	ChannelReader<T> source,
	int batchSize,
	bool singleReader,
	bool syncCont = false,
	Func<int, List<T>>? batchFactory = null)
	: BatchingChannelReader<T, List<T>>(
		batchFactory ?? (static capacity => new List<T>(capacity)),
		source,
		batchSize,
		singleReader,
		syncCont)
{
	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void AddBatchItem(List<T> batch, T item)
	{
		Debug.Assert(batch is not null);
		batch!.Add(item);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override int GetBatchSize(List<T> batch)
	{
		Debug.Assert(batch is not null);
		return batch.Count;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void TrimBatch(ref List<T> batch, bool isVerifiedFull)
	{
		Debug.Assert(batch is not null);
		if (!isVerifiedFull) batch!.TrimExcess();
	}
}

/// <inheritdoc />
public class MemoryPooledBatchingChannelReader<T>(
	ChannelReader<T> source,
	int batchSize,
	bool singleReader,
	bool syncCont = false)
	: BatchingChannelReader<T, IMemoryOwner<T>>(
		static _ => throw new InvalidOperationException("Should never occur. Method is overridden."),
		source,
		batchSize,
		singleReader,
		syncCont)
{
	private int _currentBatchLength;

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void AddBatchItem(IMemoryOwner<T> batch, T item)
	{
		Debug.Assert(batch is not null);
		Debug.Assert(batch.Memory.Length > _currentBatchLength);
		batch.Memory.Span[_currentBatchLength++] = item;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override IMemoryOwner<T> CreateBatch(int capacity)
	{
		_currentBatchLength = 0;
		return MemoryPool<T>.Shared.Rent(capacity);
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override int GetBatchSize(IMemoryOwner<T> batch)
	{
		Debug.Assert(batch is not null);
		// We can do this because the calling code is highly synchronized.
		return _currentBatchLength;
	}

	/// <inheritdoc />
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected override void TrimBatch(ref IMemoryOwner<T> batch, bool isVerifiedFull)
	{
		Debug.Assert(batch is not null);
		int len = batch.Memory.Length;
		Debug.Assert(len >= _currentBatchLength);
		//Debug.Assert(_currentBatchLength <= batchSize);
		if (len == _currentBatchLength) return;
		batch = new Batch(batch, batch.Memory.Slice(0, _currentBatchLength));
	}

	private readonly struct Batch(IMemoryOwner<T> owner, Memory<T> memory)
		: IMemoryOwner<T>
	{
		public Memory<T> Memory { get; } = memory;
		public void Dispose() => owner.Dispose();
	}
}