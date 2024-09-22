using System.Threading.Tasks;
using System.Xml.Linq;

namespace Open.ChannelExtensions.Tests;
public class PipelineExceptionTests
{
	const int BatchSize = 20;
	const int Elements = 100;
	readonly Channel<int> _channel;
	int _thrown = -2;

	public PipelineExceptionTests()
	{
		_channel = Channel.CreateBounded<int>(10000);
		for (var i = 0; i < 100; i++)
		{
			if (!_channel.Writer.TryWrite(i))
				throw new Exception("Failed to write " + i);
		}
	}

	Func<int, int> CreateThrowIfEqual(int elementToThrow) => element =>
	{
		if (elementToThrow != -1 && element != elementToThrow)
			return element;
		_thrown = element;
		throw new Exception("Thrown at " + element);
	};

	ChannelReader<int> PrepareStage1(int elementToThrow) =>
		_channel.Reader
			.Pipe(1, CreateThrowIfEqual(elementToThrow))
			.Pipe(2, evt => evt * 2);

	async Task AssertException(ValueTask<long> task, int elementToThrow)
	{
		if (elementToThrow == Elements)
			_channel.Writer.Complete(new Exception());
		else
			_channel.Writer.Complete();

		await Assert.ThrowsAsync<AggregateException>(async () => await task);
		await Assert.ThrowsAsync<ChannelClosedException>(async () => await _channel.CompleteAsync());
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(BatchSize - 1)]
	[InlineData(BatchSize)]
	[InlineData(BatchSize + 1)]
	[InlineData(-1)]
	[InlineData(Elements)]
	public Task Regular(int elementToThrow)
	{
		var task = PrepareStage1(elementToThrow)
			//.Batch(20)
			.PipeAsync(1, evt => new ValueTask<int>(evt))
			.ReadAll(_ => { });

		return AssertException(task, elementToThrow);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(BatchSize - 1)]
	[InlineData(BatchSize)]
	[InlineData(BatchSize + 1)]
	[InlineData(-1)]
	[InlineData(Elements)]
	public Task Batched(int elementToThrow)
	{
		var task = PrepareStage1(elementToThrow)
			.Batch(20)
			.ReadAll(_ => { });

		return AssertException(task, elementToThrow);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(BatchSize - 1)]
	[InlineData(BatchSize)]
	[InlineData(BatchSize + 1)]
	[InlineData(-1)]
	[InlineData(Elements)]
	public Task BatchPiped(int elementToThrow)
	{
		var task = PrepareStage1(elementToThrow)
			.Batch(20)
			.PipeAsync(1, evt => new ValueTask<List<int>>(evt))
			.ReadAll(_ => { });

		return AssertException(task, elementToThrow);
	}
}