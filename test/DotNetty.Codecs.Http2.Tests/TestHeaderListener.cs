
namespace DotNetty.Codecs.Http2.Tests
{
    using System.Collections.Generic;
    using DotNetty.Common.Utilities;

    class TestHeaderListener : DefaultHttp2Headers
    {
        private readonly List<HpackHeaderField> headers;

        public TestHeaderListener(List<HpackHeaderField> headers)
        {
            this.headers = headers;
        }

        public override IHeaders<ICharSequence, ICharSequence> Add(ICharSequence name, ICharSequence value)
        {
            this.headers.Add(new HpackHeaderField(name, value));
            return this;
        }
    }
}
