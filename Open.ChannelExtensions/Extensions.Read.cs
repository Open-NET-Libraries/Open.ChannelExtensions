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
		/// <param name="channel">The channel to read from.</param>
		/// <param name="reciever">The reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static async Task Read<T>(this Channel<T> channel,
			Action<T, int> reciever,
			CancellationToken cancellationToken = default)
		{
			var localCount = 0;
			var reader = channel.Reader;
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
		public static async Task Read<T>(this Channel<T> channel,
			Action<T> reciever,
			CancellationToken cancellationToken = default)
		{
			var reader = channel.Reader;
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
		/// <param name="reciever">The async reciever function.</param>
		/// <param name="cancellationToken">An optional cancellation token.</param>
		/// <returns>A task that completes when no more reading is to be done.</returns>
		public static async Task Read<T>(this Channel<T> channel,
			Func<T, int, Task> reciever,
			CancellationToken cancellationToken = default)
		{
			var localCount = 0;
			var reader = channel.Reader;
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
		public static async Task Read<T>(this Channel<T> channel,
			Func<T, Task> reciever,
			CancellationToken cancellationToken = default)
		{
			var reader = channel.Reader;
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
	}
}
