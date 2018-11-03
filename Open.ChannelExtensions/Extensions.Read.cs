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
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static async Task ReadAll<T>(this ChannelReader<T> reader,
			Action<T, int> reciever,
			CancellationToken cancellationToken = default)
		{
			var localCount = 0;
			do
			{
				while (
					!cancellationToken.IsCancellationRequested
					&& reader.TryRead(out var item))
				{
					reciever(item, localCount++);
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
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task Read<T>(this Channel<T> channel,
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
		public static async Task ReadAll<T>(this ChannelReader<T> reader,
			Action<T> reciever,
			CancellationToken cancellationToken = default)
		{
			do
			{
				while (
					!cancellationToken.IsCancellationRequested
					&& reader.TryRead(out var item))
				{
					reciever(item);
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
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static Task Read<T>(this Channel<T> channel,
			Action<T> reciever,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAll(reciever, cancellationToken);

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
					await reciever(item, localCount++);
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
		public static Task ReadAsync<T>(this Channel<T> channel,
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
		public static async Task ReadAllAsync<T>(this ChannelReader<T> reader,
			Func<T, ValueTask> reciever,
			CancellationToken cancellationToken = default)
		{
			do
			{
				while (
					!cancellationToken.IsCancellationRequested
					&& reader.TryRead(out var item))
				{
					await reciever(item);
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
		public static Task ReadAsync<T>(this Channel<T> channel,
			Func<T, ValueTask> reciever,
			CancellationToken cancellationToken = default)
			=> channel.Reader.ReadAllAsync(reciever);
	}
}
