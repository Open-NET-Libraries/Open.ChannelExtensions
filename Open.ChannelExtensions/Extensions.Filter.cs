using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class FilteringChannelReader<T> : ChannelReader<T>
		{
			public FilteringChannelReader(ChannelReader<T> source, Func<T, bool> predicate)
			{
				_source = source ?? throw new ArgumentNullException(nameof(source));
				_predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
				Contract.EndContractBlock();
			}

			private readonly ChannelReader<T> _source;
			private readonly Func<T, bool> _predicate;
			public override Task Completion => _source.Completion;

			public override bool TryRead(out T item)
			{
				while (_source.TryRead(out item))
				{
					if (_predicate(item))
						return true;
				}

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
				item = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
				return false;
			}

			public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
				=> _source.WaitToReadAsync(cancellationToken);
		}

		/// <summary>
		/// Produces a reader that only contains results that pass the predicate condition.  Ones that fail the predicate are discarded.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="source">The source channel reader.</param>
		/// <param name="predicate">The predicate function.</param>
		/// <returns>A channel reader representing the filtered results.</returns>
		public static ChannelReader<T> Filter<T>(this ChannelReader<T> source, Func<T, bool> predicate)
			=> new FilteringChannelReader<T>(source, predicate);
	}
}
