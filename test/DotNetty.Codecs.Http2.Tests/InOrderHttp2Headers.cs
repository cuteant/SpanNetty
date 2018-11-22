
namespace DotNetty.Codecs.Http2.Tests
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// Http2Headers implementation that preserves headers insertion order.
    /// </summary>
    public class InOrderHttp2Headers : DefaultHeaders<ICharSequence, ICharSequence>, IHttp2Headers
    {
        public InOrderHttp2Headers() : base(CharSequenceValueConverter.Default) { }

        public override bool Equals(object obj)
        {
            return obj is IHttp2Headers http2Headers && this.Equals(http2Headers, AsciiString.CaseSensitiveHasher);
        }

        public override int GetHashCode()
        {
            return this.HashCode(AsciiString.CaseSensitiveHasher);
        }

        public ICharSequence Method
        {
            get => this.Get(PseudoHeaderName.Method.Value, null);
            set => this.Set(PseudoHeaderName.Method.Value, value);
        }

        public ICharSequence Scheme
        {
            get => this.Get(PseudoHeaderName.Scheme.Value, null);
            set => this.Set(PseudoHeaderName.Scheme.Value, value);
        }

        public ICharSequence Authority
        {
            get => this.Get(PseudoHeaderName.Authority.Value, null);
            set => this.Set(PseudoHeaderName.Authority.Value, value);
        }

        public ICharSequence Path
        {
            get => this.Get(PseudoHeaderName.Path.Value, null);
            set => this.Set(PseudoHeaderName.Path.Value, value);
        }

        public ICharSequence Status
        {
            get => this.Get(PseudoHeaderName.Status.Value, null);
            set => this.Set(PseudoHeaderName.Status.Value, value);
        }

        public bool Contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            return this.Contains(name, value, caseInsensitive ? AsciiString.CaseInsensitiveHasher : AsciiString.CaseSensitiveHasher);
        }
    }
}
