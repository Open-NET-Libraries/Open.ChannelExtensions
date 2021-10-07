using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions;

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
			var source = Source;
			if (source == null || source.Completion.IsCompleted || Buffer == null)
				return false;

			lock (Buffer)
			{
				if (!source.TryRead(out var batch))
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
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="allowSynchronousContinuations">True can reduce the amount of scheduling and markedly improve performance, but may produce unexpected or even undesirable behavior.</param>
	/// <returns>A channel reader containing the joined results.</returns>
	public static ChannelReader<T> Join<T>(this ChannelReader<IAsyncEnumerable<T>> source, bool singleReader = false, bool allowSynchronousContinuations = false)
	{
		var buffer = CreateChannel<T>(1, singleReader, allowSynchronousContinuations);
		var writer = buffer.Writer;

		_ = JoinCore();

		return buffer.Reader;

		async ValueTask JoinCore()
		{
			try
			{
				await source
					.ReadAllAsync(
						async (batch, i) =>
						{
							await foreach (var e in batch)
								await writer.WriteAsync(e).ConfigureAwait(false);
						}).ConfigureAwait(false);
				writer.Complete();
			}
			catch (Exception ex)
			{
				writer.Complete(ex);
			}

		}
	}
#endif
}
