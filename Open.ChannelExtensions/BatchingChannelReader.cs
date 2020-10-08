using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
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
			if (Buffer == null || Buffer.Reader.Completion.IsCompleted) return false;
			if (TryPipeItems()) return true;
			if (_batch == null) return false;

			lock (Buffer)
			{
				if (Buffer.Reader.Completion.IsCompleted) return false;
				if (TryPipeItems()) return true;
				var c = _batch;
				if (c == null || Buffer.Reader.Completion.IsCompleted)
					return false;
				c.TrimExcess();
				_batch = null;
				return Buffer.Writer.TryWrite(c); // Should always be true at this point.
			}
		}

		/// <inheritdoc />
		protected override bool TryPipeItems()
		{
			if (Buffer == null || Buffer.Reader.Completion.IsCompleted)
				return false;

			lock (Buffer)
			{
				if (Buffer.Reader.Completion.IsCompleted) return false;

				var c = _batch;
				var source = Source;
				if (source == null || source.Completion.IsCompleted)
				{
					// All finished, release the last batch to the buffer.
					if (c == null) return false;

					c.TrimExcess();
					_batch = null;

					Buffer.Writer.TryWrite(c);
					return true;
				}

				while (source.TryRead(out T item))
				{
					if (c == null) _batch = c = new List<T>(_batchSize) { item };
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

			var source = Source;
			if (source == null) return await bufferWait.ConfigureAwait(false);

			var b = bufferWait.AsTask();
			using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			var token = tokenSource.Token;

		start:

			if (b.IsCompleted) return await b.ConfigureAwait(false);

			var s = source.WaitToReadAsync(token);
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
}
