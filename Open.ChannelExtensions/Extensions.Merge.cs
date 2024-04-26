namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Merges the unmatchedChannelReader parameter into the source channel.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	/// <param name="source">The asynchronous source data to use.</param>
	/// <param name="unmatchedChannelReader">Channel for the unmatched items to merged into the source channel</param>
	/// <param name="cancellationToken">A cancellation token.</param>
	/// <returns></returns>
	public static ChannelReader<T> Merge<T>(this ChannelReader<T> source, ChannelReader<T> unmatchedChannelReader, CancellationToken cancellationToken)
	{
		Channel<T> mergeChannel = Channel.CreateUnbounded<T>();
		ChannelWriter<T> mergeWriter = mergeChannel.Writer;

		Task.Run(async () =>
		{
			try
			{
				if (source != null)
				{
					await source
					  .ReadAllAsync(cancellationToken,
					  async (e) =>
					  {
						  await mergeWriter
						  .WriteAsync(e, cancellationToken)
						  .ConfigureAwait(false);
					  })
					  .ConfigureAwait(false);
				}
				if (unmatchedChannelReader != null)
				{
					await unmatchedChannelReader
					   .ReadAllAsync(cancellationToken,
					   async (e) =>
					   {
						   await mergeWriter
						   .WriteAsync(e, cancellationToken)
						   .ConfigureAwait(false);
					   })
					   .ConfigureAwait(false);
				}
			}
			finally
			{
				mergeWriter.Complete();
			}
		}, cancellationToken);

		return mergeChannel.Reader;
	}
}
