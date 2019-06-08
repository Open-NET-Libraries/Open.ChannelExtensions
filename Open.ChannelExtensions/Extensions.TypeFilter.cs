using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class TypeFilteringChannelReader<TSource, T> : ChannelReader<T>
		{
			public TypeFilteringChannelReader(ChannelReader<TSource> source)
			{
				_source = source ?? throw new ArgumentNullException(nameof(source));
				Contract.EndContractBlock();
			}

			private readonly ChannelReader<TSource> _source;
			public override Task Completion => _source.Completion;

			public override bool TryRead(out T item)
			{
				while (_source.TryRead(out TSource s))
				{
					if(s is T i)
					{
						item = i;
						return true;
					}
				}

				item = default;
				return false;
			}

			public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
				=> _source.WaitToReadAsync(cancellationToken);
		}

		public static ChannelReader<T> OfType<TSource, T>(this ChannelReader<TSource> source)
			=> new TypeFilteringChannelReader<TSource, T>(source);
	}
}
