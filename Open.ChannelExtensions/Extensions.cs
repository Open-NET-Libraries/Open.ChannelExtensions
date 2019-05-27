using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Attempts to write to a channel and will asynchronously return false if the channel is closed/complete.
		/// First attempt is synchronous and it may return immediately.
		/// Subsequent attempts will continue until the channel is closed or value is accepted by the writer.
		/// </summary>
		/// <typeparam name="T">The type accepted by the channel writer.</typeparam>
		/// <param name="writer">The channel writer to write to.</param>
		/// <param name="value">The value to attempt to write.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A ValueTask containing true if successfully added and false if the channel is closed/complete.</returns>
		public static ValueTask<bool> TryWriteAsync<T>(this ChannelWriter<T> writer,
			T value, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (writer.TryWrite(value))
				return new ValueTask<bool>(true);

			return TryWriteAsyncCore(writer, value, cancellationToken);
		}

		static async ValueTask<bool> TryWriteAsyncCore<T>(ChannelWriter<T> writer,
			T value, CancellationToken cancellationToken = default)
		{
			ValueTask<bool> tryAgain;
			do
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (writer.TryWrite(value))
					return true;

				tryAgain = writer.WaitToWriteAsync(cancellationToken);
			}
			while (tryAgain.IsCompletedSuccessfully ? tryAgain.Result : await tryAgain.ConfigureAwait(false));

			return false;
		}

		/// <summary>
		/// Attempts to write to a channel and throws if the channel is closed/complete.
		/// First attempt is synchronous and it may return immediately.
		/// Subsequent attempts will continue until the channel is closed or value is accepted by the writer.
		/// </summary>
		/// <typeparam name="T">The type accepted by the channel writer.</typeparam>
		/// <param name="writer">The channel writer to write to.</param>
		/// <param name="value">The value to attempt to write.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <exception cref="ChannelClosedException">If the channel is closed.</exception>
		public static ValueTask WriteIfOpenAsync<T>(this ChannelWriter<T> writer,
			T value, CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (writer.TryWrite(value))
				return new ValueTask();

			return ThrowChannelClosedExceptionIfFalse(
				TryWriteAsyncCore(writer, value, cancellationToken));
		}

		static async ValueTask ThrowChannelClosedExceptionIfFalse(ValueTask<bool> write)
		{
			var ok = write.IsCompletedSuccessfully ? write.Result : await write.ConfigureAwait(false);
			if (!ok) throw new ChannelClosedException();
		}

		/// <summary>
		/// Calls complete on the writer and returns the completion from the reader.
		/// </summary>
		/// <typeparam name="TWrite">The type being received by the writer.</typeparam>
		/// <typeparam name="TRead">The type being read from the reader.</typeparam>
		/// <returns>The reader's completion task.</returns>
		public static Task CompleteAsync<TWrite, TRead>(this Channel<TWrite, TRead> channel)
		{
			channel.Writer.Complete();
			return channel.Reader.Completion;
		}

		/// <summary>
		/// Writes all lines from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The source data to use.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<string> ToChannel(this TextReader source,
			int capacity = -1, bool singleReader = false,
			CancellationToken cancellationToken = default)
			=> CreateChannel<string>(capacity, singleReader)
				.Source(source, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The source data to use.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannel<T>(this IEnumerable<T> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.Source(maxConcurrency, source, cancellationToken);

		/// <summary>
		/// Asynchronously executes all entries and writes their results to a channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Func<T>> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.SourceAsync(maxConcurrency, source, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<ValueTask<T>> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.SourceAsync(maxConcurrency, source, cancellationToken);

		/// <summary>
		/// Writes all entries from the source to a channel and calls complete when finished.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="singleReader">True will cause the resultant reader to optimize for the assumption that no concurrent read operations will occur.</param>
		/// <param name="maxConcurrency">The maximum number of concurrent operations.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>The channel reader containing the results.</returns>
		public static ChannelReader<T> ToChannelAsync<T>(this IEnumerable<Task<T>> source,
			int capacity = -1, bool singleReader = false, int maxConcurrency = 1,
			CancellationToken cancellationToken = default)
			=> CreateChannel<T>(capacity, singleReader)
				.SourceAsync(maxConcurrency, source, cancellationToken);

	}
}
