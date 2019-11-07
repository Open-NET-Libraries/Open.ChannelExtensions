using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class JoiningChannelReader<TList, T> : BufferingChannelReader<TList, T>
			where TList : IEnumerable<T>
		{
			public JoiningChannelReader(ChannelReader<TList> source, bool singleReader) : base(source, singleReader)
			{
			}

			protected override bool TryPipeItems()
			{
				if (Source.Completion.IsCompleted)
					return false;

				lock (Buffer)
				{
					if (!Source.TryRead(out TList batch))
						return false;

					foreach (var i in batch)
					{
						// Assume this will always be true for our internal unbound channel.
						Buffer.Writer.TryWrite(i);
					}

					return true;
				}
			}
		}

		const int DefaultBufferSize = 100;

		static ChannelReader<T> JoinInternal<T, TEnumerable>(ChannelReader<TEnumerable> source, int bufferSize, bool singleReader)
			where TEnumerable : IEnumerable<T>
		{
			if (source == null) throw new ArgumentNullException(nameof(source));
			if (bufferSize < 1) throw new ArgumentOutOfRangeException(nameof(bufferSize), bufferSize, "Must be at least 1.");
			Contract.EndContractBlock();

			var buffer = CreateChannel<T>(bufferSize, singleReader);
			var writer = buffer.Writer;

			Task.Run(async () =>
				await source.ReadAllAsync(
					async (batch, i) =>
					{
						foreach (var e in batch)
						{
							if(!writer.TryWrite(e))
								await writer.WriteAsync(e).ConfigureAwait(false);
						}
					}))
				.ContinueWith(
					t => buffer.CompleteAsync(t.Exception), TaskContinuationOptions.ExecuteSynchronously);

			return buffer.Reader;
		}

		/// <summary>
		/// Joins collections of the same type into a single channel reader in the order provided.
		/// </summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="source">The source reader.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the joined results.</returns>
		public static ChannelReader<T> Join<T>(this ChannelReader<IEnumerable<T>> source, bool singleReader = false)
			=> new JoiningChannelReader<IEnumerable<T>, T>(source, singleReader);

		/// <summary>
		/// Joins collections of the same type into a single channel reader in the order provided.
		/// </summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="source">The source reader.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the joined results.</returns>
		public static ChannelReader<T> Join<T>(this ChannelReader<ICollection<T>> source, bool singleReader = false)
			=> new JoiningChannelReader<ICollection<T>, T>(source, singleReader);

		/// <summary>
		/// Joins collections of the same type into a single channel reader in the order provided.
		/// </summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="source">The source reader.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the joined results.</returns>
		public static ChannelReader<T> Join<T>(this ChannelReader<IList<T>> source, bool singleReader = false)
			=> new JoiningChannelReader<IList<T>, T>(source, singleReader);

		/// <summary>
		/// Joins collections of the same type into a single channel reader in the order provided.
		/// </summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="source">The source reader.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the joined results.</returns>
		public static ChannelReader<T> Join<T>(this ChannelReader<List<T>> source, bool singleReader = false)
			=> new JoiningChannelReader<List<T>, T>(source, singleReader);

		/// <summary>
		/// Joins collections of the same type into a single channel reader in the order provided.
		/// </summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="source">The source reader.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the joined results.</returns>
		public static ChannelReader<T> Join<T>(this ChannelReader<T[]> source, bool singleReader = false)
			=> new JoiningChannelReader<T[], T>(source, singleReader);

#if NETSTANDARD2_1
		/// <summary>
		/// Joins collections of the same type into a single channel reader in the order provided.
		/// </summary>
		/// <typeparam name="T">The result type.</typeparam>
		/// <param name="source">The source reader.</param>
		/// <param name="bufferSize">The capacity of the resultant channel.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <returns>A channel reader containing the joined results.</returns>
		public static ChannelReader<T> Join<T>(this ChannelReader<IAsyncEnumerable<T>> source, int bufferSize = DefaultBufferSize, bool singleReader = false)
		{
			var buffer = CreateChannel<T>(bufferSize, singleReader);

			Task.Run(async () =>
				await source.ReadAllAsync(
					async (batch, i) =>
					{
						await foreach (var e in batch)
							await buffer.Writer.WriteAsync(e).ConfigureAwait(false);
					}))
				.ContinueWith(
					t => buffer.CompleteAsync(t.Exception), TaskContinuationOptions.ExecuteSynchronously);

			return buffer.Reader;
		}
#endif
	}
}
