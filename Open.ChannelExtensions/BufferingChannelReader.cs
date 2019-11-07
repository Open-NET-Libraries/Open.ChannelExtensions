using System;
using System.Diagnostics.Contracts;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	abstract class BufferingChannelReader<TIn, TOut> : ChannelReader<TOut>
	{
		protected ChannelReader<TIn>? Source;
		protected readonly Channel<TOut> Buffer;
		public BufferingChannelReader(ChannelReader<TIn> source, bool singleReader)
		{
			Source = source ?? throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();

			Buffer = Extensions.CreateChannel<TOut>(-1, singleReader);

			if (source.Completion.IsCompleted)
			{
				Buffer.Writer.Complete(source.Completion.Exception);
			}
			else
			{
				source.Completion.ContinueWith(t =>
				{
					// Need to be sure writing is done before we continue...
					lock (Buffer)
					{
						while (TryPipeItems()) { }
						Buffer.Writer.Complete(t.Exception);
					}

					Source = null;
				});
			}
		}

		public override Task Completion => Buffer.Reader.Completion;

		protected abstract bool TryPipeItems();

		public override bool TryRead(out TOut item)
		{
			do
			{
				if (Buffer.Reader.TryRead(out item))
					return true;
			}
			while (TryPipeItems());

#pragma warning disable CS8653 // A default expression introduces a null value for a type parameter.
			item = default;
#pragma warning restore CS8653 // A default expression introduces a null value for a type parameter.
			return false;
		}

		public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
		{
			if (cancellationToken.IsCancellationRequested)
				return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));

			var source = Source;
			var b = Buffer.Reader.WaitToReadAsync(cancellationToken);
			if (b.IsCompletedSuccessfully || source==null || source.Completion.IsCompleted)
				return b;

			var s = source.WaitToReadAsync(cancellationToken);
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
}
