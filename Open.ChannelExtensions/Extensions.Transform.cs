using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class TransformingChannelReader<T, TResult> : ChannelReader<TResult>
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
				if (_source.TryRead(out var e))
				{
					item = _transform(e);
					return true;
				}

				item = default!;
				return false;
			}

			public override async ValueTask<TResult> ReadAsync(CancellationToken cancellationToken = default)
				=> _transform(await _source.ReadAsync(cancellationToken));

			public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
				=> _source.WaitToReadAsync(cancellationToken);
		}

		/// <summary>
		/// Transforms the 
		/// </summary>
		/// <typeparam name="T">The output type of the provided source reader and input type of the transform.</typeparam>
		/// <typeparam name="TResult">The output type of the transform.</typeparam>
		/// <param name="source">The source channel reader.</param>
		/// <param name="transform">The transform function.</param>
		/// <returns>A channel reader representing the tranformed results.</returns>
		public static ChannelReader<TResult> Transform<T, TResult>(this ChannelReader<T> source, Func<T, TResult> transform)
			=> new TransformingChannelReader<T, TResult>(source, transform);
	}
}
