namespace Http2Tiles
{
    using DotNetty.Buffers;
    using System;
    using System.Collections.Generic;

    public sealed class ImageCache
    {
        static readonly Dictionary<string, IByteBuffer> ImageBank;

        static ImageCache()
        {
            ImageBank = new Dictionary<string, IByteBuffer>(StringComparer.OrdinalIgnoreCase);

            var asm = typeof(ImageCache).Assembly;
            var path = typeof(ImageCache).Namespace + ".images.";
            for (int y = 0; y < 10; y++)
            {
                for (int x = 0; x < 20; x++)
                {
                    try
                    {
                        string name = Name(x, y);
                        var fileBytes = Unpooled.UnreleasableBuffer(Http2ExampleUtil.ToByteBuffer(asm.GetManifestResourceStream(path + name)));
                        ImageBank.Add(name, fileBytes);
                    }
                    catch (Exception)
                    {
                        //e.printStackTrace();
                    }
                }
            }
        }

        public static IByteBuffer Image(int x, int y)
        {
            return ImageBank.TryGetValue(Name(x, y), out var buf) ? buf : null;
        }

        public static string Name(int x, int y)
        {
            return $"tile-{y}-{x}.jpeg";
        }
    }
}
