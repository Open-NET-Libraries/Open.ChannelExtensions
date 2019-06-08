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
			}

			private readonly ChannelReader<TList> _source;
			public override Task Completion
				=> _source.Completion.ContinueWith(t =>
				{
					// Need to be sure writing is done before we continue...
					lock (_buffer)
					{
						_buffer.Writer.Complete(t.Exception);
					}
					return _buffer.Reader.Completion;
				})
				.Unwrap();

			bool TryPipeItems()
			{
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
				if (s.IsCompletedSuccessfully && !s.Result)
					return b;

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

		public static ChannelReader<T> Join<TList, T>(this ChannelReader<TList> source)
			where TList : IEnumerable<T>
			=> new JoiningChannelReader<TList, T>(source);
	}
}
