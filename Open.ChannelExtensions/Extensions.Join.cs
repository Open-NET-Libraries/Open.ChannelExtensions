using System.Buffers;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	private sealed class JoiningChannelReader<TList, T>(
		ChannelReader<TList> source,
		bool singleReader,
		Action<TList>? recycler = null)
		: BufferingChannelReader<TList, T>(source, singleReader)
		where TList : IEnumerable<T>
	{
		protected override bool TryPipeItems(bool _)
		{
			ChannelReader<TList>? source = Source;
			if (source?.Completion.IsCompleted != false || Buffer is null)
				return false;

			TList? batch;
			lock (SyncLock)
			{
				if (!source.TryRead(out batch))
					return false;

				foreach (T? i in batch)
				{
					// Assume this will always be true for our internal unbound channel.
					Buffer.Writer.TryWrite(i);
				}
			}

			recycler?.Invoke(batch);

			return true;
		}
	}

	private sealed class QueueJoiningChannelReader<T>(
		ChannelReader<Queue<T>> source,
		bool singleReader, Action<Queue<T>>? recycler)
		: BufferingChannelReader<Queue<T>, T>(source, singleReader)
	{
		protected override bool TryPipeItems(bool _)
		{
			ChannelReader<Queue<T>>? source = Source;
			if (source?.Completion.IsCompleted != false || Buffer is null)
				return false;

			Queue<T>? batch;
			lock (SyncLock)
			{
				if (!source.TryRead(out batch))
					return false;

				while (batch.TryDequeue(out T? i))
				{
					// Assume this will always be true for our internal unbound channel.
					Buffer.Writer.TryWrite(i);
				}
			}

			recycler?.Invoke(batch);

			return true;
		}
	}

	private sealed class MemoryJoiningChannelReader<T>(
		ChannelReader<IMemoryOwner<T>> source,
		bool singleReader)
		: BufferingChannelReader<IMemoryOwner<T>, T>(source, singleReader)
	{
		protected override bool TryPipeItems(bool _)
		{
			ChannelReader<IMemoryOwner<T>>? source = Source;
			if (source?.Completion.IsCompleted != false || Buffer is null)
				return false;

			IMemoryOwner<T>? batch = null;
			try
			{
				lock (SyncLock)
				{
					if (!source.TryRead(out batch))
						return false;
					Memory<T> mem = batch.Memory;
					int len = mem.Length;
					Span<T> span = mem.Span;
					for (int i = 0; i < len; i++)
					{
						// Assume this will always be true for our internal unbound channel.
						Buffer.Writer.TryWrite(span[i]);
					}
				}
			}
			finally
			{
				batch?.Dispose();
			}

			return true;
		}
	}

#if NETSTANDARD2_0
	internal static bool TryDequeue<T>(this Queue<T> queue, out T item)
	{
		if (queue.Count == 0)
		{
			item = default!;
			return false;
		}

		item = queue.Dequeue();
		return true;
	}
#endif

	/// <summary>
	/// Joins collections of the same type into a single channel reader in the order provided.
	/// </summary>
	/// <typeparam name="T">The result type.</typeparam>
	/// <typeparam name="TList">The collection type.</typeparam>
	/// <param name="source">The source reader.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="recycler">An optional delegate for reusing the already consumed batches.</param>
	/// <returns>A channel reader containing the joined results.</returns>
	public static ChannelReader<T> Join<T, TList>(
		this ChannelReader<TList> source,
		bool singleReader = false,
		Action<TList>? recycler = null)
		where TList : IEnumerable<T>
		=> new JoiningChannelReader<TList, T>(source, singleReader, recycler);

	/// <inheritdoc cref="Join{T, TList}(ChannelReader{TList}, bool, Action{TList}?)"/>
	public static ChannelReader<T> Join<T>(
		this ChannelReader<ICollection<T>> source,
		bool singleReader = false,
		Action<ICollection<T>>? recycler = null)
		=> new JoiningChannelReader<ICollection<T>, T>(source, singleReader, recycler);

	/// <inheritdoc cref="Join{T, TList}(ChannelReader{TList}, bool, Action{TList}?)"/>
	public static ChannelReader<T> Join<T>(
		this ChannelReader<IList<T>> source,
		bool singleReader = false,
		Action<IList<T>>? recycler = null)
		=> new JoiningChannelReader<IList<T>, T>(source, singleReader, recycler);

	/// <inheritdoc cref="Join{T, TList}(ChannelReader{TList}, bool, Action{TList}?)"/>
	public static ChannelReader<T> Join<T>(
		this ChannelReader<List<T>> source,
		bool singleReader = false,
		Action<List<T>>? recycler = null)
		=> new JoiningChannelReader<List<T>, T>(source, singleReader, recycler);

	/// <inheritdoc cref="Join{T, TList}(ChannelReader{TList}, bool, Action{TList}?)"/>
	public static ChannelReader<T> Join<T>(
		this ChannelReader<Queue<T>> source,
		bool singleReader = false,
		Action<Queue<T>>? recycler = null)
		=> new QueueJoiningChannelReader<T>(source, singleReader, recycler);

	/// <remarks>Automatically disposes of the <see cref="IMemoryOwner{T}"/>.</remarks>
	/// <inheritdoc cref="Join{T, TList}(ChannelReader{TList}, bool, Action{TList}?)"/>
	public static ChannelReader<T> Join<T>(
		this ChannelReader<IMemoryOwner<T>> source,
		bool singleReader = false)
		=> new MemoryJoiningChannelReader<T>(source, singleReader);

	/// <inheritdoc cref="Join{T, TList}(ChannelReader{TList}, bool, Action{TList}?)"/>
	public static ChannelReader<T> Join<T>(
		this ChannelReader<T[]> source,
		bool singleReader = false)
		=> new JoiningChannelReader<T[], T>(source, singleReader);

	/// <summary>
	/// Joins collections of the same type into a single channel reader in the order provided.
	/// </summary>
	/// <typeparam name="T">The result type.</typeparam>
	/// <param name="source">The source reader.</param>
	/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
	/// <param name="allowSynchronousContinuations">True can reduce the amount of scheduling and markedly improve performance, but may produce unexpected or even undesirable behavior.</param>
	/// <returns>A channel reader containing the joined results.</returns>
	[SuppressMessage("CodeQuality", "IDE0079:Remove unnecessary suppression", Justification = "For NET STandard 2.1")]
	[SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "Potential exception type is too far removed.")]
	public static ChannelReader<T> Join<T>(
		this ChannelReader<IAsyncEnumerable<T>> source,
		bool singleReader = false,
		bool allowSynchronousContinuations = false)
	{
		Channel<T>? buffer = CreateChannel<T>(1, singleReader, allowSynchronousContinuations);
		ChannelWriter<T>? writer = buffer.Writer;

		Task.Run(JoinCore);

		return buffer.Reader;

		async ValueTask JoinCore()
		{
			try
			{
				await source
					.ReadAllAsync(async (batch, _) =>
					{
						await foreach (T? e in batch.ConfigureAwait(false))
						{
							await writer
								.WriteAsync(e)
								.ConfigureAwait(false);
						}
					})
					.ConfigureAwait(false);

				writer.Complete();
			}
			catch (Exception ex)
			{
				writer.Complete(ex);
			}
		}
	}
}
