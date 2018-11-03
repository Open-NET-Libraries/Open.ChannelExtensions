using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public static partial class Extensions
	{
		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="reciever">The async reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static async Task ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, int, ValueTask> reciever,
			CancellationToken cancellationToken = default)
		{
			var localCount = 0;
			do
			{
				while (
					!cancellationToken.IsCancellationRequested
					&& reader.TryRead(out var item))
				{
					var result = reciever(item, localCount++);
					if (!result.IsCompletedSuccessfully)
						await result.ConfigureAwait(false);
				}
			}
			while (
				!cancellationToken.IsCancellationRequested
				&& await reader.WaitToReadAsync()
					.ConfigureAwait(false));
		}

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="reciever">The async reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllAsync<T>(this Channel<T> channel,
			Func<T, int, ValueTask> reciever,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync(reciever, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="reciever">The async reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, ValueTask> reciever,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllAsync((e, i) => reciever(e), cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="reciever">The async reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAllAsync<T>(this Channel<T> channel,
			Func<T, ValueTask> reciever,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync(reciever);

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAll<T>(this ChannelReader<T> reader,
			Action<T, int> reciever,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllAsync((e, i) =>
				{
					reciever(e, i);
					return new ValueTask(Task.CompletedTask);
				},
				cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAll<T>(this Channel<T> channel,
			Action<T, int> reciever,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAll(reciever, cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="reader">The channel reader to read from.</param>
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAll<T>(this ChannelReader<T> reader,
			Action<T> reciever,
			CancellationToken cancellationToken = default)
			=> reader.ReadAllAsync((e, i) =>
				{
					reciever(e);
					return new ValueTask(Task.CompletedTask);
				},
				cancellationToken);

		/// <summary>
		/// Reads items from the channel and passes them to the reciever.
		/// </summary>
		/// <typeparam name="T">The item type.</typeparam>
		/// <param name="channel">The channel to read from.</param>
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task ReadAll<T>(this Channel<T> channel,
			Action<T> reciever,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAll(reciever, cancellationToken);
	}
}
