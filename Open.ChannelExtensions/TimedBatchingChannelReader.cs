namespace Open.ChannelExtensions
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading;
    using System.Threading.Channels;
    using System.Threading.Tasks;

    /// <summary>
    /// A ChannelReader that batches results.
    /// Use the .Batch extension instead of constructing this directly.
    /// </summary>
    public class TimedBatchingChannelReader<T> : BufferingChannelReader<T, List<T>>
    {
        readonly int      _batchSize;
        readonly TimeSpan _batchTimeout;

        List<T>? _batch;

        /// <summary>
        /// Constructs a BatchingChannelReader.
        /// Use the .Batch extension instead of constructing this directly.
        /// </summary>
        public TimedBatchingChannelReader(ChannelReader<T> source, int batchSize, TimeSpan batchTimeout, bool singleReader, bool syncCont = false) : base(
            source, singleReader, syncCont
        ) {
            if (batchSize < 1) throw new ArgumentOutOfRangeException(nameof(batchSize), batchSize, "Must be at least 1.");

            Contract.EndContractBlock();

            _batchSize    = batchSize;
            _batchTimeout = batchTimeout;
        }

        /// <inheritdoc />
        protected override bool TryPipeItems() {
            if (Buffer?.Reader.Completion.IsCompleted != false)
                return false;

            lock (Buffer) {
                if (Buffer.Reader.Completion.IsCompleted) return false;

                var c      = _batch;
                var source = Source;

                if (source?.Completion.IsCompleted != false) {
                    // All finished, release the last batch to the buffer.
                    if (c == null) return false;

                    c.TrimExcess();
                    _batch = null;
                    Buffer.Writer.TryWrite(c);
                    return true;
                }

                while (source.TryRead(out var item)) {
                    if (c == null) _batch = c = new List<T>(_batchSize) { item };
                    else c.Add(item);

                    if (c.Count == _batchSize) {
                        _batch = null;
                        Buffer.Writer.TryWrite(c);
                        return true;
                    }
                }

                return false;

                //
                // while (true) {
                //     var empty = !source.TryRead(out var item);
                //
                //     if (empty) {
                //         // wait for another item before timing out and releasing the last batch to the buffer.
                //         Task.Delay(_batchTimeout).GetAwaiter().GetResult();
                //
                //         var stillEmpty = !source.TryRead(out item);
                //
                //         // there is not hope so flush
                //         if (stillEmpty) {
                //             if (c?.Count > 0) {
                //                 c.TrimExcess();
                //                 _batch = null;
                //                 Buffer.Writer.TryWrite(c);
                //                 return true;
                //             }
                //
                //             return false;
                //         }
                //     }
                //
                //     if (c == null) _batch = c = new List<T>(_batchSize) { item };
                //     else c.Add(item);
                //
                //     if (c.Count == _batchSize) {
                //         _batch = null;
                //         Buffer.Writer.TryWrite(c);
                //         return true;
                //     }
                // }
            }
        }

        /// <inheritdoc />
        protected override async ValueTask<bool> WaitToReadAsyncCore(ValueTask<bool> bufferWait, CancellationToken cancellationToken) {
            var source = Source;

            if (source == null)
                return await bufferWait.ConfigureAwait(false);

            var b = bufferWait.AsTask();

            using var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var token = tokenSource.Token;

            start:

            if (b.IsCompleted) return await b.ConfigureAwait(false);

            var s = source.WaitToReadAsync(token);

            if (s.IsCompleted && !b.IsCompleted)
                await TryPipeItemsWithTimeout(cancellationToken).ConfigureAwait(false);

            if (b.IsCompleted) {
                tokenSource.Cancel();
                return await b.ConfigureAwait(false);
            }

            await Task.WhenAny(s.AsTask(), b).ConfigureAwait(false);

            if (b.IsCompleted) {
                tokenSource.Cancel();
                return await b.ConfigureAwait(false);
            }

            await TryPipeItemsWithTimeout(cancellationToken).ConfigureAwait(false);
            goto start;
        }

        async ValueTask<bool> TryPipeItemsWithTimeout(CancellationToken cancellationToken) {
            if (TryPipeItems()) return true;

            await Task.Delay(_batchTimeout, cancellationToken).ConfigureAwait(false);

            if (TryPipeItems()) return true; // still empty

            if (Buffer?.Reader.Completion.IsCompleted != false)
                return false;

            lock (Buffer) {
                if (Buffer.Reader.Completion.IsCompleted)
                    return false;

                var c = _batch;

                if (c?.Count > 0) {
                    c.TrimExcess();
                    _batch = null;
                    Buffer.Writer.TryWrite(c);
                    return true;
                }
            }

            return false;
        }
    }
}