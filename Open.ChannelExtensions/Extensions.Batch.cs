using System.Buffers;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	// Note: the return types are normalized to ensure the same API surface area.

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
	public static BatchingChannelReader<T, List<T>> Batch<T>(
		this ChannelReader<T> source,
		int batchSize,
		bool singleReader = false,
		bool allowSynchronousContinuations = false)
		=> new BatchingChannelReader<T>(source ?? throw new ArgumentNullException(nameof(source)), batchSize, singleReader, allowSynchronousContinuations);

	/// <inheritdoc cref="Batch{T}(ChannelReader{T}, int, bool, bool)" />
	public static BatchingChannelReader<T, Queue<T>> BatchToQueues<T>(
		this ChannelReader<T> source,
		int batchSize,
		bool singleReader = false,
		bool allowSynchronousContinuations = false)
		=> new QueueBatchingChannelReader<T>(source ?? throw new ArgumentNullException(nameof(source)), batchSize, singleReader, allowSynchronousContinuations);

	/// <remarks>
	/// <list type="bullet">
	/// <item>The <see cref="Memory{T}"/> returned will always be less than or equal to the batch size.</item>
	/// <item>It is important to manage disposing of the resultant <see cref="IMemoryOwner{T}"/> and not access them after disposal.</item>
	/// </list>
	/// See <see cref="MemoryPool{T}"/> for more information. It may be more benefitial to use this for larger batch sizes.
	/// </remarks>
	/// <inheritdoc cref="Batch{T}(ChannelReader{T}, int, bool, bool)" />
	public static BatchingChannelReader<T, IMemoryOwner<T>> BatchAsMemory<T>(
		this ChannelReader<T> source,
		int batchSize,
		bool singleReader = false,
		bool allowSynchronousContinuations = false)
		=> new MemoryPooledBatchingChannelReader<T>(source ?? throw new ArgumentNullException(nameof(source)), batchSize, singleReader, allowSynchronousContinuations);
}
