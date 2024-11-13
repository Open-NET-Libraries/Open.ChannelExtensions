using BenchmarkDotNet.Attributes;
using System.Threading.Channels;

namespace Open.ChannelExtensions.Benchmarks;

[MemoryDiagnoser]
public class BatchDrain
{
	[Params(10, 100, 500, 1000, 3000, 12000)]
	public int BatchSize { get; set; }

	public int TotalItems = 10000000;

	private Channel<int>? _channel;

	private readonly Queue<List<int>> _listPool = new();
	private readonly Queue<Queue<int>> _queuePool = new();

	private int _noOpTarget;

	private int NoOp(int value) => _noOpTarget = value;

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		// Ensure the compiler doesn't optimize away the target.
		Console.WriteLine("NoOp Target: {0}", _noOpTarget);
	}

	[IterationSetup]
	public void IterationSetup()
	{
		_channel = Channel.CreateUnbounded<int>(new UnboundedChannelOptions
		{
			SingleReader = true,
			SingleWriter = true
		});

		for (int i = 0; i < TotalItems; i++)
		{
			_channel.Writer.TryWrite(i);
		}

		_channel.Writer.Complete();

		_listPool.Clear();
		_queuePool.Clear();

		_listPool.Enqueue(new List<int>(BatchSize));
		_listPool.Enqueue(new List<int>(BatchSize));
		_queuePool.Enqueue(new Queue<int>(BatchSize));
		_queuePool.Enqueue(new Queue<int>(BatchSize));
	}

	[Benchmark(Baseline = true)]
	public async Task BatchListDrain()
		=> await _channel!.Reader
			.Batch(BatchSize)
			.ReadAll(e =>
			{
				for (var i = 0; i < e.Count; i++)
					_noOpTarget -= NoOp(e[i]);
			});

	[Benchmark]
	public async Task BatchListDrainPooled()
	=> await _channel!.Reader
		.Batch(BatchSize, batchFactory: _ => _listPool.Dequeue())
		.ReadAll(e =>
		{
			for(var i = 0; i < e.Count; i++)
				_noOpTarget -= NoOp(e[i]);

			e.Clear(); // Simulate resetting the size.
			_listPool.Enqueue(e);
		});

	[Benchmark]
	public async Task BatchQueueDrain()
		=> await _channel!.Reader
			.BatchToQueues(BatchSize)
			.ReadAll(e =>
			{
				while (e.TryDequeue(out var value))
					_noOpTarget -= NoOp(value);
			});

	[Benchmark]
	public async Task BatchQueueDrainPooled()
		=> await _channel!.Reader
			.BatchToQueues(BatchSize, batchFactory: _ => _queuePool.Dequeue())
			.ReadAll(e =>
			{
				while(e.TryDequeue(out var value))
					_noOpTarget -= NoOp(value);

				_queuePool.Enqueue(e);
			});

	[Benchmark]
	public async Task BatchMemoryOwnerDrain()
		=> await _channel!.Reader
			.BatchAsMemory(BatchSize)
			.ReadAll(e =>
			{
				var span = e.Memory.Span;
				var len = span.Length;
				for (var i = 0; i < len; i++)
					_noOpTarget -= NoOp(span[i]);

				e.Dispose();
			});
}
