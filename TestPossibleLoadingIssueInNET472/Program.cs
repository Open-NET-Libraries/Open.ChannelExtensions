using Open.ChannelExtensions;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace TestPossibleLoadingIssueInNET472
{
	internal class Program
	{
		static void Main()
		{
			const int expectedCount = 10000000;
			int count_ = 0;

			var queue = new BlockingCollection<int>();
			var processingTask = StartProcessingTask2(queue.GetConsumingEnumerable());

			Console.WriteLine("Starting to fill queue.");
			for (var i = 0; i < expectedCount; i++)
				queue.Add(i);

			Console.WriteLine("Queue fill complete.");

			queue.CompleteAdding();

			processingTask.Wait();


			Task StartProcessingTask2(IEnumerable<int> source)
				=> Channel.CreateUnbounded<int>()
					.Source(source, true)
					.ReadAll(IncrementCount2)
					.AsTask();

			void IncrementCount2(int c)
			{
				Interlocked.Increment(ref count_);
				if (c % 100000 == 0)
					Console.WriteLine($"Processing {c}");
			}
		}
	}
}
