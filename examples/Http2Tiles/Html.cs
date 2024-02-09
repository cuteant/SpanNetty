namespace Http2Tiles
{
    using System;
    using System.Text;

    /**
     * Static and dynamically generated HTML for the example.
     */
    public sealed class Html
    {
        public static readonly byte[] FOOTER = Encoding.UTF8.GetBytes("</body></html>");

        public static readonly byte[] HEADER = Encoding.UTF8.GetBytes("<!DOCTYPE html><html><head lang=\"en\"><title>Netty HTTP/2 Example</title>"
                + "<style>body {background:#DDD;} div#netty { line-height:0;}</style>"
                + "<link rel=\"shortcut icon\" href=\"about:blank\">"
                + "<meta charset=\"UTF-8\"></head><body>A grid of 200 tiled images is shown below. Compare:"
                + "<p>[<a href='https://" + Url(Http2Server.PORT) + "?latency=0'>HTTP/2, 0 latency</a>] [<a href='http://"
                + Url(HttpServer.PORT) + "?latency=0'>HTTP/1, 0 latency</a>]<br/>" + "[<a href='https://"
                + Url(Http2Server.PORT) + "?latency=30'>HTTP/2, 30ms latency</a>] [<a href='http://" + Url(HttpServer.PORT)
                + "?latency=30'>HTTP/1, 30ms latency</a>]<br/>" + "[<a href='https://" + Url(Http2Server.PORT)
                + "?latency=200'>HTTP/2, 200ms latency</a>] [<a href='http://" + Url(HttpServer.PORT)
                + "?latency=200'>HTTP/1, 200ms latency</a>]<br/>" + "[<a href='https://" + Url(Http2Server.PORT)
                + "?latency=1000'>HTTP/2, 1s latency</a>] [<a href='http://" + Url(HttpServer.PORT)
                + "?latency=1000'>HTTP/1, " + "1s latency</a>]<br/>");

        private static readonly int IMAGES_X_AXIS = 20;

        private static readonly int IMAGES_Y_AXIS = 10;

        private static string Url(int port)
        {
            return "localhost:" + port + "/http2";
        }

        public static byte[] Body(int latency)
        {
            int r = Math.Abs(new Random().Next());
            // The string to be built contains 13192 fixed characters plus the variable latency and random cache-bust.
            int numberOfCharacters = 13192 + StringLength(latency) + StringLength(r);
            StringBuilder sb = new StringBuilder(numberOfCharacters).Append("<div id=\"netty\">");
            for (int y = 0; y < IMAGES_Y_AXIS; y++)
            {
                for (int x = 0; x < IMAGES_X_AXIS; x++)
                {
                    sb.Append("<img width=30 height=29 src='/http2?x=")
                    .Append(x)
                    .Append("&y=").Append(y)
                    .Append("&cachebust=").Append(r)
                    .Append("&latency=").Append(latency)
                    .Append("'>");
                }
                sb.Append("<br/>\r\n");
            }
            sb.Append("</div>");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }

        private static int StringLength(int value)
        {
            return value.ToString().Length * IMAGES_X_AXIS * IMAGES_Y_AXIS;
        }
    }
}
