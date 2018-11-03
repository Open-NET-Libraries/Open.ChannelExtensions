using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Open.ChannelExtensions
{
	public class TransformChannel<TIn, TOut> : Channel<TIn, TOut>
	{
		public TransformChannel(Func<TIn, ValueTask<TOut>> transform)
		{
			//var inChannel = 

		}
	}
}
