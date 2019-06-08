using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		class JoiningChannelReader<TList, T> : ChannelReader<T>
			where TList : IEnumerable<T>
		{
			readonly Channel<T> _buffer = Channel.CreateUnbounded<T>(new UnboundedChannelOptions { AllowSynchronousContinuations = true });
			public JoiningChannelReader(ChannelReader<TList> source)
			{
				_source = source ?? throw new ArgumentNullException(nameof(source));
				Contract.EndContractBlock();

				_source.Completion.ContinueWith(t =>
				{
					// Need to be sure writing is done before we continue...
					lock (_buffer)
					{
						_buffer.Writer.Complete(t.Exception);
					}
				});
			}

			private readonly ChannelReader<TList> _source;
			public override Task Completion => _buffer.Reader.Completion;

			bool TryPipeItems()
			{
				if (_source.Completion.IsCompleted)
					return false;

				lock (_buffer)
				{
					if (!_source.TryRead(out TList batch))
						return false;

					foreach (var i in batch)
					{
						// Assume this will always be true for our internal unbound channel.
						_buffer.Writer.TryWrite(i);
					}

					return true;
				}
			}

			public override bool TryRead(out T item)
			{
				do
				{
					if (_buffer.Reader.TryRead(out item))
						return true;
				}
				while (TryPipeItems());

				item = default;
				return false;
			}

			public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
			{
				if (cancellationToken.IsCancellationRequested)
					return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));

				var b = _buffer.Reader.WaitToReadAsync(cancellationToken);
				if (b.IsCompletedSuccessfully || _source.Completion.IsCompleted)
					return b;

				var s = _source.WaitToReadAsync(cancellationToken);
				if (s.IsCompletedSuccessfully)
					return s.Result ? new ValueTask<bool>(true) : b;

				return WaitCore();

				async ValueTask<bool> WaitCore()
				{
					cancellationToken.ThrowIfCancellationRequested();

					// Not sure if there's a better way to 'WhenAny' with a ValueTask yet.
					var bt = b.AsTask();
					var st = s.AsTask();
					var first = await Task.WhenAny(bt, st);
					// Either one? Ok go.
					if (first.Result) return true;
					// Buffer returned false? We're done.
					if (first == bt) return false;
					// Second return false? Wait for buffer.
					return await bt;
				}
			}
		}

		public static ChannelReader<T> Join<T>(this ChannelReader<IEnumerable<T>> source)
			=> new JoiningChannelReader<IEnumerable<T>, T>(source);

		public static ChannelReader<T> Join<T>(this ChannelReader<ICollection<T>> source)
			=> new JoiningChannelReader<ICollection<T>, T>(source);

		public static ChannelReader<T> Join<T>(this ChannelReader<IList<T>> source)
			=> new JoiningChannelReader<IList<T>, T>(source);

		public static ChannelReader<T> Join<T>(this ChannelReader<List<T>> source)
			=> new JoiningChannelReader<List<T>, T>(source);

		public static ChannelReader<T> Join<T>(this ChannelReader<T[]> source)
			=> new JoiningChannelReader<T[], T>(source);

	}
}
