using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions;

/// <summary>
/// A ChannelReader that batches results.
/// Use the .Batch extension instead of constructing this directly.
/// </summary>
public class BatchingChannelReader<T> : BufferingChannelReader<T, List<T>>
{
	private readonly int _batchSize;
	private List<T>? _batch;

	/// <summary>
	/// Constructs a BatchingChannelReader.
	/// Use the .Batch extension instead of constructing this directly.
	/// </summary>
	public BatchingChannelReader(
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
	public BatchingChannelReader<T> WithTimeout(long millisecondsTimeout)
	{
		_timeout = millisecondsTimeout <= 0 ? Timeout.Infinite : millisecondsTimeout;

		if (Buffer is null || Buffer.Reader.Completion.IsCompleted)
			return this;

		if (_timeout == Timeout.Infinite)
		{
			Interlocked.Exchange(ref _timer, null)?.Dispose();
			return this;
		}

		LazyInitializer.EnsureInitialized(ref _timer,
			() => new Timer(obj => ForceBatch()));

		return this;
	}

	/// <param name="timeout">
	/// The timeout value where after a batch is forced.<br/>
	/// A value of zero or less cancels/clears any timeout.<br/>
	/// Note: Values are converted to milliseconds.
	/// </param>
	/// <inheritdoc cref="WithTimeout(long)"/>
	public BatchingChannelReader<T> WithTimeout(TimeSpan timeout)
		=> WithTimeout(TimeSpan.FromMilliseconds(timeout.TotalMilliseconds));

	/// <inheritdoc />
	protected override void OnBeforeFinalFlush()
		=> Interlocked.Exchange(ref _timer, null)?.Dispose();

	/// <inheritdoc />
	protected override bool TryPipeItems(bool flush)
	{
		if (Buffer is null || Buffer.Reader.Completion.IsCompleted)
			return false;

		lock (Buffer)
		{
			if (Buffer.Reader.Completion.IsCompleted) return false;

			var batched = false;
			var newBatch = false;
			List<T>? c = _batch;
			ChannelReader<T>? source = Source;
			if (source is null || source.Completion.IsCompleted)
			{
				// All finished, if necessary, release the last batch to the buffer.
				if (c is null) return false;
				goto flushBatch;
			}

			while (source.TryRead(out T? item))
			{
				if (c is not null) c.Add(item);
				else
				{
					newBatch = true; // a new batch could start but not be emmited.
					_batch = c = new List<T>(_batchSize) { item };
				}

				Debug.Assert(c.Count <= _batchSize);
				var full = c.Count == _batchSize;
				while (!full && source.TryRead(out item))
				{
					c.Add(item);
					full = c.Count == _batchSize;
				}

				if (!full) break;

				Emit(ref c);
			}

			if (!flush || c is null)
				goto finalizeTimer;

		flushBatch:

			c.TrimExcess();
			Emit(ref c);

		finalizeTimer:
			
			// Are we adding to the existing batch (active timeout) or did we create a new one?
			if (newBatch && _batch is not null)
			{
				var ok = _timer?.Change(_timeout, 0);
				Debug.Assert(ok ?? true);
			}

			return batched;

			void Emit(ref List<T>? c)
			{
				_batch = null;
				newBatch = false;
				if (!batched) _timer?.Change(Timeout.Infinite, 0); // Since we're emmitting one, let's ensure the timeout is cancelled.
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
		if (source is null) return await bufferWait.ConfigureAwait(false);

		Task<bool>? b = bufferWait.AsTask();
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken token = tokenSource.Token;

	start:

		if (b.IsCompleted) return await b.ConfigureAwait(false);

		ValueTask<bool> s = source.WaitToReadAsync(token);
		if (s.IsCompleted && !b.IsCompleted) TryPipeItems(false);

		if (b.IsCompleted)
		{
			tokenSource.Cancel();
			return await b.ConfigureAwait(false);
		}

		await Task.WhenAny(s.AsTask(), b).ConfigureAwait(false);
		if (b.IsCompleted)
		{
			tokenSource.Cancel();
			return await b.ConfigureAwait(false);
		}

		TryPipeItems(false);
		goto start;
	}
}
