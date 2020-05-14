using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Channels;

namespace Open.ChannelExtensions
{
	/// <summary>
	/// A ChannelReader that batches results.
	/// Use the .Batch extension instead of constructing this directly.
	/// </summary>
	public class BatchingChannelReader<T> : BufferingChannelReader<T, List<T>>
	{
		private readonly int _batchSize;
		private List<T>? _current;

		/// <summary>
		/// Constructs a BatchingChannelReader.
		/// Use the .Batch extension instead of constructing this directly.
		/// </summary>
		public BatchingChannelReader(ChannelReader<T> source, int batchSize, bool singleReader, bool syncCont = false) : base(source, singleReader, syncCont)
		{
			if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least 1.");
			Contract.EndContractBlock();

			_batchSize = batchSize;
			_current = source.Completion.IsCompleted ? null : new List<T>(batchSize);
		}

		/// <summary>
		/// If no full batch is waiting, will force buffering any batch that has at least one item.
		/// Returns true if anything was added to the buffer.
		/// </summary>
		public bool ForceBatch()
		{
			if (Buffer == null || Buffer.Reader.Completion.IsCompleted) return false;
			if (TryPipeItems()) return true;

			lock (Buffer)
			{
				if (Buffer.Reader.Completion.IsCompleted) return false;
				if (TryPipeItems()) return true;
				var c = _current;
				if (c == null || c.Count == 0 || Buffer.Reader.Completion.IsCompleted)
					return false;
				c.TrimExcess();
				_current = new List<T>(_batchSize);
				Buffer.Writer.TryWrite(c);
			}

			return true;
		}

		/// <inheritdoc />
		protected override bool TryPipeItems()
		{
			if (_current == null || Buffer == null || Buffer.Reader.Completion.IsCompleted)
				return false;

			lock (Buffer)
			{
				var c = _current;
				if (c == null || Buffer.Reader.Completion.IsCompleted)
					return false;

				var source = Source;
				if (source == null || source.Completion.IsCompleted)
				{
					// All finished, release the last batch to the buffer.
					c.TrimExcess();
					_current = null;
					if (c.Count == 0)
						return false;

					Buffer.Writer.TryWrite(c);
					return true;
				}

				while (source.TryRead(out T item))
				{
					if (c.Count == _batchSize)
					{
						_current = new List<T>(_batchSize) { item };
						Buffer.Writer.TryWrite(c);
						return true;
					}

					c.Add(item);
				}

				return false;
			}
		}
	}
}
