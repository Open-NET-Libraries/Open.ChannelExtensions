using System;
using System.Collections.Generic;
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
	public BatchingChannelReader(ChannelReader<T> source, int batchSize, bool singleReader, bool syncCont = false) : base(source, singleReader, syncCont)
	{
		if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least 1.");
		Contract.EndContractBlock();

		_batchSize = batchSize;
	}

	/// <summary>
	/// If no full batch is waiting, will force buffering any batch that has at least one item.
	/// Returns true if anything was added to the buffer.
	/// </summary>
	public bool ForceBatch()
	{
		if (Buffer is null || Buffer.Reader.Completion.IsCompleted) return false;
		if (TryPipeItems()) return true;
		if (_batch is null) return false;

		lock (Buffer)
		{
			if (Buffer.Reader.Completion.IsCompleted) return false;
			if (TryPipeItems()) return true;
			List<T>? c = _batch;
			if (c is null || Buffer.Reader.Completion.IsCompleted)
				return false;
			c.TrimExcess();
			_batch = null;
			return Buffer.Writer.TryWrite(c); // Should always be true at this point.
		}
	}

	/// <inheritdoc />
	protected override bool TryPipeItems()
	{
		if (Buffer is null || Buffer.Reader.Completion.IsCompleted)
			return false;

		lock (Buffer)
		{
			if (Buffer.Reader.Completion.IsCompleted) return false;

			List<T>? c = _batch;
			ChannelReader<T>? source = Source;
			if (source is null || source.Completion.IsCompleted)
			{
				// All finished, release the last batch to the buffer.
				if (c is null) return false;

				c.TrimExcess();
				_batch = null;

				Buffer.Writer.TryWrite(c);
				return true;
			}

			while (source.TryRead(out T? item))
			{
				if (c is null) _batch = c = new List<T>(_batchSize) { item };
				else c.Add(item);

				if (c.Count == _batchSize)
				{
					_batch = null;
					Buffer.Writer.TryWrite(c);
					return true;
				}
			}

			return false;
		}
	}

	/// <inheritdoc />
	protected override async ValueTask<bool> WaitToReadAsyncCore(ValueTask<bool> bufferWait, CancellationToken cancellationToken)
	{

		ChannelReader<T>? source = Source;
		if (source is null) return await bufferWait.ConfigureAwait(false);

		Task<bool>? b = bufferWait.AsTask();
		using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
		CancellationToken token = tokenSource.Token;

	start:

		if (b.IsCompleted) return await b.ConfigureAwait(false);

		ValueTask<bool> s = source.WaitToReadAsync(token);
		if (s.IsCompleted && !b.IsCompleted) TryPipeItems();

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

		TryPipeItems();
		goto start;
	}
}
