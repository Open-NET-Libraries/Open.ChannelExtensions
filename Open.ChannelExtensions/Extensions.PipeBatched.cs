using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace Open.ChannelExtensions;
public static partial class Extensions
{
	/// <summary>
	/// Asynchronously processes items from a channel in batches using a provided batch processing function.
	/// The processed items are written to a new channel that is returned to the caller.
	/// </summary>
	/// <typeparam name="TIn">The type of the items in the input channel.</typeparam>
	/// <typeparam name="TOut">The type of the items in the output channel.</typeparam>
	/// <param name="reader">The input channel to read items from.</param>
	/// <param name="batchProcessor">A function that processes a batch of items and returns a task that completes with the processed items.</param>
	/// <param name="maxBatchSize">The maximum number of items to include in a batch. If not specified or less than 1, there is no upper limit on batch size.</param>
	/// <param name="minBatchSize">The minimum number of items to include in a batch. If not specified or less than 1, a batch is processed as soon as any items are available.</param>
	/// <param name="capacity">The maximum number of items that can be stored in the output channel. If not specified or less than 1, the channel is unbounded.</param>
	/// <param name="singleReader">Indicates whether the output channel allows multiple concurrent readers. If not specified, the default is false.</param>
	/// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
	/// <returns>A channel reader that can be used to read the processed items.</returns>
	public static ChannelReader<TOut> PipeBatchedAsync<TIn, TOut>
	(
		this ChannelReader<TIn> reader,
		Func<IEnumerable<TIn>, ValueTask<IEnumerable<TOut>>> batchProcessor,
		int maxBatchSize = -1,
		int minBatchSize = -1,
		int capacity = -1,
		bool singleReader = false,
		CancellationToken cancellationToken = default
	)
	{
		if (reader is null) throw new ArgumentNullException(nameof(reader));
		if (batchProcessor is null) throw new ArgumentNullException(nameof(batchProcessor));
		//if (maxBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(maxBatchSize), "Must be greater than 0");
		//if (minBatchSize <= 0) throw new ArgumentOutOfRangeException(nameof(minBatchSize), "Must be greater than 0");
		Contract.EndContractBlock();

		var channel = CreateChannel<TOut>(capacity, singleReader);


		_ = Task.Run(async () =>
		{
			var hasUpperLimit = maxBatchSize > 0;

			var items = new List<TIn>();
			do
			{
				while (reader.TryRead(out TIn? item))
				{
					items.Add(item);
					if (hasUpperLimit && items.Count >= maxBatchSize)
					{
						break;
					}

					if (cancellationToken.IsCancellationRequested)
					{
						break;
					}
				}

				var hasReachedLowerBounds = items.Count > 0 && items.Count >= minBatchSize;
				var hasReachedUpperBounds = hasUpperLimit && items.Count >= maxBatchSize;

				if (hasReachedLowerBounds || hasReachedUpperBounds)
				{
					await WriteToChannel(items).ConfigureAwait(false);

					items.Clear();
				}

				if (cancellationToken.IsCancellationRequested)
				{
					break;
				}

				if (!await reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
				{
					break;
				}
			}
			while (true);

			if (items.Any())
			{
				await WriteToChannel(items).ConfigureAwait(false);
			}

			channel.Writer.Complete();
		}, cancellationToken);

		return channel.Reader;

		async Task WriteToChannel(List<TIn> items)
		{
			var processedItems = await batchProcessor(items).ConfigureAwait(false);
			if (processedItems is null)
			{
				return;
			}

			foreach (var processedItem in processedItems)
			{
				await channel.Writer.WriteAsync(processedItem, cancellationToken).ConfigureAwait(false);
			}
		}
	}
}
