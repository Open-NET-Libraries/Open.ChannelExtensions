using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Attempts to write to a channel and will asynchronously return false if the channel is closed/complete.
		/// First attempt is synchronous and it may return immediately.
		/// Subsequent attempts will continue until the channel is closed or value is accepted by the writer.
		/// </summary>
		/// <typeparam name="T">The type accepted by the channel writer.</typeparam>
		/// <param name="writer">The channel writer to write to.</param>
		/// <param name="value">The value to attempt to write.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A ValueTask containing true if successfully added and false if the channel is closed/complete.</returns>
		public static ValueTask<bool> TryWriteAsync<T>(this ChannelWriter<T> writer,
			T value, CancellationToken cancellationToken = default)
		{
			if (writer.TryWrite(value))
				return new ValueTask<bool>(true);

			return TryWriteAsyncCore(writer, value, cancellationToken);
		}

		static async ValueTask<bool> TryWriteAsyncCore<T>(ChannelWriter<T> writer,
			T value, CancellationToken cancellationToken = default)
		{
			while (await writer.WaitToWriteAsync(cancellationToken))
			{
				if (cancellationToken.IsCancellationRequested)
					break;
				if (writer.TryWrite(value))
					return true;
			}
			return false;
		}
	}
}
