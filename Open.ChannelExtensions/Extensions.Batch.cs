using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Channels;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class BatchingChannelReader<T> : BufferingChannelReader<T, List<T>>
		{
			private readonly int _batchSize;
			private List<T>? _current;

			public BatchingChannelReader(ChannelReader<T> source, int batchSize, bool singleReader) : base(source, singleReader)
			{
				if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least 1.");
				Contract.EndContractBlock();

				_batchSize = batchSize;
				_current = source.Completion.IsCompleted ? null : new List<T>(batchSize);
			}


			protected override bool TryPipeItems()
			{
				if (_current == null)
					return false;

				if (Buffer.Reader.Completion.IsCompleted)
					return false;

				lock (Buffer)
				{
					var c = _current;
					if (c == null)
						return false;

					if (Buffer.Reader.Completion.IsCompleted)
						return false;

					if (Source.Completion.IsCompleted)
					{
						c.TrimExcess();
						_current = null;
						if (c.Count == 0)
							return false;

						Buffer.Writer.TryWrite(c);
						return true;
					}

					while (Source.TryRead(out T item))
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

		/// <summary>
		/// Batches results into the batch size provided with a max capacity of batches.
		/// </summary>
		/// <typeparam name="T">The output type of the source channel.</typeparam>
		/// <param name="source">The channel to read from.</param>
		/// <param name="batchSize">The maximum size of each batch.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the batches.</returns>
		public static ChannelReader<List<T>> Batch<T>(this ChannelReader<T> source, int batchSize, bool singleReader = false)
			=> new BatchingChannelReader<T>(source, batchSize, singleReader);
	}
}
