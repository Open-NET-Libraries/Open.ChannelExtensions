using System.Collections.Immutable;

namespace Open.ChannelExtensions;

public static partial class Extensions
{
	/// <summary>
	/// Creates a <see cref="MergingChannelReader{T}"/>
	/// that reads from multiple sources in a round-robin fashion.
	/// </summary>
	/// <typeparam name="T">The source type.</typeparam>
	/// <param name="sources">The channels to read from.</param>
	public static MergingChannelReader<T> Merge<T>(this IEnumerable<ChannelReader<T>> sources) => new(sources);

	/// <summary>
	/// Merges the <paramref name="primary"/> with the <paramref name="secondary"/>
	/// as a <see cref="MergingChannelReader{T}"/>
	/// that reads from multiple sources in a round-robin fashion.
	/// </summary>
	/// <exception cref="ArgumentNullException">
	/// If the <paramref name="primary"/>
	/// or <paramref name="secondary"/> sources are null.
	/// </exception>
	/// <inheritdoc cref="MergingChannelReader{T}.Merge(ChannelReader{T}, ChannelReader{T}[])"/>/>
	public static MergingChannelReader<T> Merge<T>(
		this ChannelReader<T> primary,
		ChannelReader<T> secondary,
		params ChannelReader<T>[] others)
	{
		if (primary is null) throw new ArgumentNullException(nameof(primary));
		if (secondary is null) throw new ArgumentNullException(nameof(secondary));
		Contract.EndContractBlock();

		// Is this already a merging reader? Then recapture the sources so it flattens the hierarchy.
		if (primary is MergingChannelReader<T> mcr)
			return mcr.Merge(secondary, others);

		if (others is null || others.Length == 0)
			return new MergingChannelReader<T>(ImmutableArray.Create(primary, secondary));

		var builder = ImmutableArray.CreateBuilder<ChannelReader<T>>(2 + others.Length);
		builder.Add(primary);
		builder.Add(secondary);
		builder.AddRange(others);
		return new MergingChannelReader<T>(builder.MoveToImmutable());
	}
}
