namespace Open.ChannelExtensions;

public static partial class Extensions
{
	sealed class MergingChannelReader<T> : ChannelReader<T>
	{
		public MergingChannelReader(IEnumerable<ChannelReader<T>> sources)
		{
			if (sources is null) throw new ArgumentNullException(nameof(sources));
			Contract.EndContractBlock();

			var active = sources.Where(s =>
			{
				Debug.Assert(s is not null);
				return s.Completion.Status != TaskStatus.RanToCompletion;
			}).ToArray();

			_count = active.Length;

			if (_count == 0)
			{
				_sources = [];
				Completion = Task.CompletedTask;
				return;
			}

			_sources = active;

			// Capture the initial list of completions.
			var completions = active.Select(e => e.Completion).ToList();

			// Create a task that completes when any of the sources are faulted:
			Completion = Task.Run(async () =>
			{
				// Wait for any of the tasks to complete and let it throw if it faults.
				while (completions.Count != 0)
				{
					var completed = await Task.WhenAny(completions).ConfigureAwait(false);
					// Propagate the exception.
					await completed.ConfigureAwait(false);
					completions.Remove(completed);
				}
			});
		}

		private readonly ChannelReader<T>[] _sources;
		public override Task Completion { get; }

		readonly int _count;
		int _next = -1;

		public override bool TryRead(out T item)
		{
			int previous = -1;
			// Try as many times as there are sources before giving up.
			for (var attempt = 0; attempt < _count; attempt++)
			{
				// If the value overflows, it will be negative, which is fine, we'll adapt.
				var i = Interlocked.Increment(ref _next) % _count;
				if (i < 0) i += _count;

				var source = _sources[i];

				if (source.TryRead(out T? s))
				{
					item = s;
					return true;
				}

				// Help the round-robin to try each source at least once.
				// If previous is not -1 and i is not the next in the sequence,
				// then another thread has already tried that source.
				if (previous != -1 && (previous + 1) % _count != i)
					attempt--; // Allow for an extra attempt. 

				previous = i;
			}

			item = default!;
			return false;
		}

		public override ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
		{
			var completion = Completion;
			if (Completion.IsCompleted)
			{
				return completion.IsFaulted
					? new ValueTask<bool>(Task.FromException<bool>(completion.Exception!))
					: new ValueTask<bool>(false);
			}

			if (cancellationToken.IsCancellationRequested)
				return new ValueTask<bool>(Task.FromCanceled<bool>(cancellationToken));

			// Not complete or cancelled?  Wait on the sources.
			return WaitToReadAsyncCore(cancellationToken);
		}

		private async ValueTask<bool> WaitToReadAsyncCore(CancellationToken cancellationToken)
		{
		retry:
			// We don't care about ones that have already completed.
			var active = _sources.Where(s => s.Completion.Status != TaskStatus.RanToCompletion).ToArray();
			if (active.Length == 0) return false;

			var next = await Task.WhenAny(active.Select(s => s.WaitToReadAsync(cancellationToken).AsTask())).ConfigureAwait(false);

			// Allow for possible exception to be thrown.
			var result = await next.ConfigureAwait(false);
			if (result) return true;

			// If result was false, then there's one less and we should try again.
			goto retry;
		}
	}

	/// <summary>
	/// Reads from multiple sources in a round-robin fashion.
	/// </summary>
	/// <typeparam name="T">The source type.</typeparam>
	/// <param name="sources">The channels to read from.</param>
	/// <returns>
	/// A <see cref="ChannelReader{T}"/> that reads from all sources in a round-robin fashion.
	/// </returns>
	public static ChannelReader<T> Merge<T>(this IEnumerable<ChannelReader<T>> sources)
		=> new MergingChannelReader<T>(sources);
}
