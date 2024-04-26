namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Splits a channel based on a predicate. All items not matching the predicate are returned in the unmatchedChannelReader out parameter.
	/// </summary>
	/// <typeparam name="T">The input type of the channel.</typeparam>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="maxConcurrency">The maximum number of concurrent operations.  Greater than 1 may likely cause results to be out of order.</param>
	/// <param name="predicate">Predicate to test against</param>
	/// <param name="unmatchedChannelReader">Channel for the unmatched items</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns></returns>
	public static ChannelReader<T> Split<T>(this ChannelReader<T> source, int maxConcurrency, Func<T, bool> predicate, out ChannelReader<T> unmatchedChannelReader, CancellationToken cancellationToken)
	{
		Channel<T>? matchedChannel = Channel.CreateUnbounded<T>();
		ChannelWriter<T>? matchedWriter = matchedChannel.Writer;

		Channel<T>? unmatchedChannel = Channel.CreateUnbounded<T>();
		ChannelWriter<T>? unmatchedWriter = unmatchedChannel.Writer;

		source
			.ReadAllConcurrentlyAsync(maxConcurrency, e =>
			{
				return predicate(e)
					? matchedWriter.WriteAsync(e, cancellationToken)
					: unmatchedWriter.WriteAsync(e, cancellationToken);
			}, cancellationToken)
			.ContinueWith(
				t =>
				{
					unmatchedWriter.Complete(t.Exception);
					matchedWriter.Complete(t.Exception);
				},
				CancellationToken.None,
				TaskContinuationOptions.ExecuteSynchronously,
				TaskScheduler.Current);

		unmatchedChannelReader = unmatchedChannel.Reader;
		return matchedChannel.Reader;
	}
}
