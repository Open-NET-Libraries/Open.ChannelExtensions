namespace Open.ChannelExtensions;

/// <summary>
/// A channel wrapper that takes the provided channel and transforms them on demand when being read.
/// </summary>
/// <typeparam name="TWrite">Specifies the type of data that may be written to the channel.</typeparam>
/// <typeparam name="TRead">Specifies the type of data read from the source channel.</typeparam>
/// <typeparam name="TResult">Specifies the type of data that may be read from the channel.</typeparam>
public class TransformChannel<TWrite, TRead, TResult> : Channel<TWrite, TResult>
{
	/// <summary>
	/// Creates a channel wrapper that takes the provided channel and transforms them on demand when being read.
	/// </summary>
	/// <param name="source">The channel containing the source data.</param>
	/// <param name="transform">The transform function to be applied to the results when being read.</param>
	public TransformChannel(Channel<TWrite, TRead> source, Func<TRead, TResult> transform)
	{
		if (source is null) throw new ArgumentNullException(nameof(source));
		if (transform is null) throw new ArgumentNullException(nameof(transform));
		Contract.EndContractBlock();

		Writer = source.Writer;
		Reader = source.Reader.Transform(transform);
	}
}

/// <summary>
/// A channel wrapper that takes the provided channel and transforms them on demand when being read.
/// </summary>
/// <typeparam name="T">Specifies the type of data that may be written to the channel.</typeparam>
/// <typeparam name="TResult">Specifies the type of data that may be read from the channel.</typeparam>
public class TransformChannel<T, TResult> : TransformChannel<T, T, TResult>
{
	/// <summary>
	/// Creates a channel wrapper that takes the provided channel and transforms them on demand when being read.
	/// </summary>
	/// <param name="source">The channel containing the source data.</param>
	/// <param name="transform">The transform function to be applied to the results when being read.</param>
	public TransformChannel(Channel<T, T> source, Func<T, TResult> transform)
		: base(source, transform)
	{
	}
}
