
namespace DotNetty.Codecs.Http2.Tests
{
    using System.Collections.Generic;

    public class ArgumentCaptor<T>
    {
        readonly List<T> values = new List<T>();
        T value;

        public bool Capture(T t)
        {
            this.values.Add(t);
            this.value = t;
            return true;
        }

        public T GetValue() => this.value;

        public IList<T> GetAllValues() => this.values;
    }
}
