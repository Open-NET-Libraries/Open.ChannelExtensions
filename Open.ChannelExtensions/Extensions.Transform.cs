using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class TransformingChannelReader<TIn, TOut> : ChannelReader<TOut>
		{
			public TransformingChannelReader(ChannelReader<TIn> source, Func<TIn, TOut> transform)
			{
				_source = source ?? throw new ArgumentNullException(nameof(source));
				_transform = transform ?? throw new ArgumentNullException(nameof(transform));
				Contract.EndContractBlock();
			}

			private readonly ChannelReader<TIn> _source;
			private readonly Func<TIn, TOut> _transform;
			public override Task Completion => _source.Completion;

			public override bool TryRead(out TOut item)
			{
				if (_source.TryRead(out var e))
				{
					item = _transform(e);
					return true;
				}

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
				item = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
				return false;
			}

			public override async ValueTask<TOut> ReadAsync(CancellationToken cancellationToken = default)
				=> _transform(await _source.ReadAsync(cancellationToken));

			public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
				=> _source.WaitToReadAsync(cancellationToken);
		}

		/// <summary>
		/// Transforms the 
		/// </summary>
		/// <typeparam name="TIn">The output type of the provided source reader and input type of the transform.</typeparam>
		/// <typeparam name="TOut">The output type of the transform.</typeparam>
		/// <param name="source">The source channel reader.</param>
		/// <param name="transform">The transform function.</param>
		/// <returns>A channel reader representing the tranformed results.</returns>
		public static ChannelReader<TOut> Transform<TIn, TOut>(this ChannelReader<TIn> source, Func<TIn, TOut> transform)
			=> new TransformingChannelReader<TIn, TOut>(source, transform);
	}
}
