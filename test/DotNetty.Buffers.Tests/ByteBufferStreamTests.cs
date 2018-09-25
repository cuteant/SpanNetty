using System.IO;
using System.Text;
using Xunit;
#if !TEST40
using System.Threading.Tasks;
#endif

namespace DotNetty.Buffers.Tests
{
    public class ByteBufferStreamTests
    {
        [Fact]
        public void CopyTo()
        {
            var byteBuffer = Unpooled.Buffer(4096);
            var bufferStream = new ByteBufferStream(byteBuffer, true);
            var text = "Hello World";
            var bytes = Encoding.UTF8.GetBytes(text);
            bufferStream.Write(bytes, 0, bytes.Length);
            Assert.Equal(bytes.Length, bufferStream.Length);
            var ms = new MemoryStream();
            bufferStream.CopyTo(ms);
            ms.Position = 0;
            Assert.Equal(text, Encoding.UTF8.GetString(ms.ToArray()));
            bufferStream.Close();
        }

#if !TEST40
        [Fact]
        public async Task CopyToAsync()
        {
            var byteBuffer = Unpooled.Buffer(4096);
            var bufferStream = new ByteBufferStream(byteBuffer, true);
            var text = "庄生晓梦迷蝴蝶，望帝春心托杜鹃。";
            var bytes = Encoding.UTF8.GetBytes(text);
            await bufferStream.WriteAsync(bytes, 0, bytes.Length);
            Assert.Equal(bytes.Length, bufferStream.Length);
            var ms = new MemoryStream();
            await bufferStream.CopyToAsync(ms);
            ms.Position = 0;
            Assert.Equal(text, Encoding.UTF8.GetString(ms.ToArray()));
            bufferStream.Close();
        }
#endif
    }
}
