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
		protected readonly Channel<TOut>? Buffer;
		public BufferingChannelReader(ChannelReader<TIn> source, bool singleReader, bool syncCont = false)
		{
			Source = source ?? throw new ArgumentNullException(nameof(source));
			Contract.EndContractBlock();

			if (source.Completion.IsCompleted)
			{
				Buffer = null;
			}
			else
			{
				Buffer = Extensions.CreateChannel<TOut>(-1, singleReader, syncCont);

				source.Completion.ContinueWith(t =>
				{
					// Need to be sure writing is done before we continue...
					lock (Buffer)
					{
						while (TryPipeItems()) { }
						Buffer.Writer.Complete(t.Exception);
					}

					Source = null;
				}, TaskScheduler.Current);
			}
		}

		public override Task Completion => Buffer?.Reader.Completion ?? Task.CompletedTask;

		protected abstract bool TryPipeItems();

		public override bool TryRead(out TOut item)
		{
			if (Buffer != null) do
				{
					if (Buffer.Reader.TryRead(out item))
						return true;
				}
				while (TryPipeItems());

			item = default!;
			return false;
		}

		public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
		{
			if (Buffer == null || Buffer.Reader.Completion.IsCompleted)
				return new ValueTask<bool>(false);

			if (cancellationToken.IsCancellationRequested)
				return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));

			var b = Buffer.Reader.WaitToReadAsync(cancellationToken);
			if (b.IsCompleted)
				return b;

			var source = Source;
			if (source == null)
				return b;

			return WaitCore();

			async ValueTask<bool> WaitCore()
			{

			start:

				if (b.IsCompleted) return await b;

				var s = source!.WaitToReadAsync(cancellationToken);
				if (s.IsCompleted && !b.IsCompleted)
					TryPipeItems();

				if (b.IsCompleted) return await b;
				await s;
				if (b.IsCompleted) return await b;
				TryPipeItems();

				goto start;
			}
		}
	}
}
