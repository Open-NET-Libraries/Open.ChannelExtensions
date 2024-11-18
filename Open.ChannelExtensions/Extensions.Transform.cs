namespace Open.ChannelExtensions;

public static partial class Extensions
{
	private sealed class TransformingChannelReader<T, TResult> : ChannelReader<TResult>
	{
		public TransformingChannelReader(ChannelReader<T> source, Func<T, TResult> transform)
		{
			_source = source ?? throw new ArgumentNullException(nameof(source));
			_transform = transform ?? throw new ArgumentNullException(nameof(transform));
			Contract.EndContractBlock();
		}

		private readonly ChannelReader<T> _source;
		private readonly Func<T, TResult> _transform;
		public override Task Completion => _source.Completion;

		public override bool TryRead(out TResult item)
		{
			if (_source.TryRead(out T? e))
			{
				item = _transform(e);
				return true;
			}

			item = default!;
			return false;
		}

		public override async ValueTask<TResult> ReadAsync(CancellationToken cancellationToken = default)
			=> _transform(await _source.ReadAsync(cancellationToken).ConfigureAwait(false));

		public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
			=> _source.WaitToReadAsync(cancellationToken);
	}

	/// <summary>
	/// Transforms the underlying values as they are being read.
	/// </summary>
	/// <typeparam name="T">The output type of the provided source reader and input type of the transform.</typeparam>
	/// <typeparam name="TResult">The output type of the transform.</typeparam>
	/// <param name="source">The source channel reader.</param>
	/// <param name="transform">The transform function.</param>
	/// <returns>A channel reader representing the transformed results.</returns>
	public static ChannelReader<TResult> Transform<T, TResult>(this ChannelReader<T> source, Func<T, TResult> transform)
		=> new TransformingChannelReader<T, TResult>(source, transform);

	/// <summary>
	/// Transforms the underlying values as they are being read.
	/// </summary>
	/// <typeparam name="TWrite">Specifies the type of data that may be written to the channel.</typeparam>
	/// <typeparam name="TRead">Specifies the type of data read from the source channel.</typeparam>
	/// <typeparam name="TResult">Specifies the type of data that may be read from the channel.</typeparam>
	/// <param name="source">The source channel reader.</param>
	/// <param name="transform">The transform function.</param>
	/// <returns>A channel reader representing the transformed results.</returns>
	public static TransformChannel<TWrite, TRead, TResult> Transform<TWrite, TRead, TResult>(this Channel<TWrite, TRead> source, Func<TRead, TResult> transform)
		=> new(source, transform);

	/// <summary>
	/// Transforms the underlying values as they are being read.
	/// </summary>
	/// <typeparam name="T">Specifies the type of data that may be written to the channel.</typeparam>
	/// <typeparam name="TResult">Specifies the type of data that may be read from the channel.</typeparam>
	/// <param name="source">The source channel reader.</param>
	/// <param name="transform">The transform function.</param>
	/// <returns>A channel reader representing the transformed results.</returns>
	public static TransformChannel<T, TResult> Transform<T, TResult>(this Channel<T> source, Func<T, TResult> transform)
		=> new(source, transform);
}
