using System;
using System.Threading.Channels;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Batches results into the batch size provided with a max capacity of batches.
	/// </summary>
	/// <typeparam name="T">The output type of the source channel.</typeparam>
	/// <param name="source">The channel to read from.</param>
	/// <param name="batchSize">
	/// The maximum size of each batch.
	/// Note: setting this value sets the capacity of each batch (reserves memory).
	/// </param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="allowSynchronousContinuations">True can reduce the amount of scheduling and markedly improve performance, but may produce unexpected or even undesirable behavior.</param>
	/// <returns>A channel reader containing the batches.</returns>
	public static BatchingChannelReader<T> Batch<T>(
		this ChannelReader<T> source,
		int batchSize,
		bool singleReader = false,
		bool allowSynchronousContinuations = false)
		=> new(source ?? throw new ArgumentNullException(nameof(source)), batchSize, singleReader, allowSynchronousContinuations);
}
