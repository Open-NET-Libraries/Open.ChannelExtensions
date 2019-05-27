using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Asynchronously writes all entries from the source to the channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static async ValueTask WriteAllAsync<T>(this ChannelWriter<T> target,
			IEnumerable<ValueTask<T>> source, bool complete = false, CancellationToken cancellationToken = default)
		{
			await target.WaitToWriteAndThrowIfClosedAsync(
				"The target channel was closed before writing could begin.",
				cancellationToken);

			var next = new ValueTask();
			foreach (var e in source)
			{
				var value = await e.ConfigureAwait(false);
				await next.ConfigureAwait(false);
				next = target.WriteAsync(value, cancellationToken);
			}
			await next.ConfigureAwait(false);

			if (complete)
				target.Complete();
		}

		/// <summary>
		/// Asynchronously writes all entries from the source to the channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static ValueTask WriteAllAsync<T>(this ChannelWriter<T> target,
			IEnumerable<Task<T>> source, bool complete = false, CancellationToken cancellationToken = default)
			=> WriteAllAsync(target, source.Select(e => new ValueTask<T>(e)), complete, cancellationToken);

		/// <summary>
		/// Asynchronously executes all entries and writes their results to the channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The asynchronous source data to use.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static ValueTask WriteAllAsync<T>(this ChannelWriter<T> target,
			IEnumerable<Func<T>> source, bool complete = false, CancellationToken cancellationToken = default)
			=> WriteAllAsync(target, source.Select(e => new ValueTask<T>(e())), complete, cancellationToken);

		/// <summary>
		/// Asynchronously writes all entries from the source to the channel.
		/// </summary>
		/// <typeparam name="T">The input type of the channel.</typeparam>
		/// <param name="target">The channel to write to.</param>
		/// <param name="source">The source data to use.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static ValueTask WriteAll<T>(this ChannelWriter<T> target,
			IEnumerable<T> source, bool complete = false, CancellationToken cancellationToken = default)
			=> WriteAllAsync(target, source.Select(e => new ValueTask<T>(e)), complete, cancellationToken);

		/// <summary>
		/// Consumes all lines from a TextReader and writes them to a channel.
		/// </summary>
		/// <param name="source">The text reader to consume from.</param>
		/// <param name="complete">If true, will call .Complete() if all the results have successfully been written (or the source is emtpy).</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task which completes when all the data has been written to the channel writer.</returns>
		public static async ValueTask WriteAllLines(this ChannelWriter<string> target,
			TextReader source, bool complete = false, CancellationToken cancellationToken = default)
		{
			var next = target.WaitToWriteAndThrowIfClosedAsync(
				"The target channel was closed before writing could begin.",
				cancellationToken);

			await next.ConfigureAwait(false);
			var more = false; // if it completed and actually returned false, no need to bubble the cancellation since it actually completed.
			while (!cancellationToken.IsCancellationRequested)
			{
				var line = await source.ReadLineAsync().ConfigureAwait(false);
				if (line == null)
				{
					more = false;
					break;
				}
				else
				{
					more = true;
				}

				await next.ConfigureAwait(false);
				next = target.WriteAsync(line, cancellationToken);
			}
			await next.ConfigureAwait(false);

			if(more)
				cancellationToken.ThrowIfCancellationRequested();

			if (complete)
				target.Complete();
		}
	}
}
