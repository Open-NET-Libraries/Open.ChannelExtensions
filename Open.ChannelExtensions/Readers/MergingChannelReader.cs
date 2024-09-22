using System.Collections.Immutable;

namespace Open.ChannelExtensions;

/// <summary>
/// Reads from multiple sources in a round-robin fashion.
/// </summary>
public sealed class MergingChannelReader<T> : ChannelReader<T>
{
	/// <summary>
	/// Initializes a new instance of the <see cref="MergingChannelReader{T}"/> class.
	/// </summary>
	public MergingChannelReader(ImmutableArray<ChannelReader<T>> sources)
	{
		_count = sources.Length;

		if (_count == 0)
		{
			_sources = ImmutableArray<ChannelReader<T>>.Empty;
			Completion = Task.CompletedTask;
			return;
		}

		Debug.Assert(sources.All(s => s is not null));

		_sources = sources;

		// Capture the initial list of completions.
		var completions = sources
			.Where(FilterOutCompleted)
			.Select(e => e.Completion)
			.ToList();

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

	/// <inheritdoc cref="MergingChannelReader{T}.MergingChannelReader(ImmutableArray{ChannelReader{T}})"/>
	internal MergingChannelReader(IEnumerable<ChannelReader<T>> sources)
		: this(ToImmutableArray(sources)) { }

	private static bool FilterOutCompleted(ChannelReader<T> source)
	{
		Debug.Assert(source is not null);
		return source.Completion.Status != TaskStatus.RanToCompletion;
	}

	private static ImmutableArray<ChannelReader<T>> ToImmutableArray(IEnumerable<ChannelReader<T>> sources)
		=> sources switch
		{
			null => throw new ArgumentNullException(nameof(sources)),
			ImmutableArray<ChannelReader<T>> a => a,
			_ => sources.Where(FilterOutCompleted).ToImmutableArray()
		};

	private readonly ImmutableArray<ChannelReader<T>> _sources;

	/// <inheritdoc />
	public override Task Completion { get; }

	readonly int _count;
	int _next = -1;

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <summary>
	/// Creates a new <see cref="MergingChannelReader{T}"/> with <paramref name="other"/> added to the list of sources.
	/// </summary>
	/// <remarks>
	/// Can be used to method chain additional sources.<br/>
	/// If the all the sources are known ahead of time,
	/// it's preferable to use the <see cref="Extensions.Merge{T}(IEnumerable{ChannelReader{T}})"/> method.
	/// </remarks>
	/// <exception cref="ArgumentNullException">If <paramref name="other"/> is null.</exception>"
	public MergingChannelReader<T> Merge(
		ChannelReader<T> other,
		params ChannelReader<T>[] others)
	{
		if (other is null)
			throw new ArgumentNullException(nameof(other));

		int count = _sources.Length + (others?.Length ?? 0);

		ImmutableArray<ChannelReader<T>>.Builder builder;
		if (other is MergingChannelReader<T> mcr)
		{
			count += mcr._sources.Length;
			builder = ImmutableArray.CreateBuilder<ChannelReader<T>>(count);
			builder.AddRange(_sources);
			builder.AddRange(mcr._sources);
		}
		else
		{
			count++;
			builder = ImmutableArray.CreateBuilder<ChannelReader<T>>(count);
			builder.AddRange(_sources);
			builder.Add(other);
		}

		if (others is not null) builder.AddRange(others);
		Debug.Assert(builder.Count == builder.Capacity);

		return new(builder.MoveToImmutable());
	}
}
