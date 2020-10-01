using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Channels;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
        /// <summary>
		/// Batches results into the batch size provided with a max capacity of batches.
		/// </summary>
		/// <typeparam name="T">The output type of the source channel.</typeparam>
		/// <param name="source">The channel to read from.</param>
		/// <param name="batchSize">The maximum size of each batch.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="allowSynchronousContinuations">True can reduce the amount of scheduling and markedly improve performance, but may produce unexpected or even undesirable behavior.</param>
		/// <returns>A channel reader containing the batches.</returns>
		public static BatchingChannelReader<T> Batch<T>(this ChannelReader<T> source, int batchSize, bool singleReader = false, bool allowSynchronousContinuations = false)
			=> new BatchingChannelReader<T>(source ?? throw new ArgumentNullException(nameof(source)), batchSize, singleReader, allowSynchronousContinuations);

        /// <summary>
        /// Batches results into the batch size provided with a max capacity of batches.
        /// Flushes incomplete batches automatically after the provided timeout.
        /// </summary>
        /// <typeparam name="T">The output type of the source channel.</typeparam>
        /// <param name="source">The channel to read from.</param>
        /// <param name="batchSize">The maximum size of each batch.</param>
        /// <param name="batchTimeout">The maximum wait time until another item is written.</param>
        /// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
        /// <param name="allowSynchronousContinuations">True can reduce the amount of scheduling and markedly improve performance, but may produce unexpected or even undesirable behavior.</param>
        /// <returns>A channel reader containing the batches.</returns>
        public static TimedBatchingChannelReader<T> Batch<T>(this ChannelReader<T> source, int batchSize, TimeSpan batchTimeout, bool singleReader = false, bool allowSynchronousContinuations = false)
            => new TimedBatchingChannelReader<T>(source ?? throw new ArgumentNullException(nameof(source)), batchSize, batchTimeout, singleReader, allowSynchronousContinuations);
	}
}
