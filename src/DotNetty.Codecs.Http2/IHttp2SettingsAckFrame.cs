using System;
using System.Collections.Generic;
using System.Text;

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// An ack for a previously received <see cref="IHttp2SettingsFrame"/>.
    /// <para>
    /// The <a href="https://tools.ietf.org/html/rfc7540#section-6.5">HTTP/2 protocol</a> enforces that ACKs are applied in
    /// order, so this ACK will apply to the earliest received and not yet ACKed <see cref="IHttp2SettingsFrame"/> frame.
    /// </para>
    /// </summary>
    public interface IHttp2SettingsAckFrame : IHttp2Frame
    {
    }
}
