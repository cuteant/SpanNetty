using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Transport.Channels
{
    public interface IChannelInitializer<TChannel> where TChannel : IChannel
    {
        /// <summary>This method will be called once the <see cref="IChannel"/> was registered. After the method returns this instance
        /// will be removed from the <see cref="IChannelPipeline"/> of the <see cref="IChannel"/>.</summary>
        /// <param name="channel">The <see cref="IChannel"/> which was registered.</param>
        void InitChannel(TChannel channel);
    }
}
