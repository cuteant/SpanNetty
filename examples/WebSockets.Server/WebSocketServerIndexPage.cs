namespace WebSockets.Server
{
    using DotNetty.Buffers;
    using System.Text;

    /// <summary>
    /// Generates the demo HTML page which is served at http://localhost:8080/
    /// </summary>
    static class WebSocketServerIndexPage
    {
        const string Newline = "\r\n";

        public static IByteBuffer GetContent(string webSocketLocation) => Unpooled.CopiedBuffer(
            "<html><head><title>Web Socket Test</title></head>" + Newline +
            "<body>" + Newline +
            "<script type=\"text/javascript\">" + Newline +
            "var socket;" + Newline +
            "if (!window.WebSocket) {" + Newline +
            "  window.WebSocket = window.MozWebSocket;" + Newline +
            '}' + Newline +
            "if (window.WebSocket) {" + Newline +
            "  socket = new WebSocket(\"" + webSocketLocation + "\");" + Newline +
            "  socket.onmessage = function(event) {" + Newline +
            "    var ta = document.getElementById('responseText');" + Newline +
            "    ta.value = ta.value + '\\n' + event.data" + Newline +
            "  };" + Newline +
            "  socket.onopen = function(event) {" + Newline +
            "    var ta = document.getElementById('responseText');" + Newline +
            "    ta.value = \"Web Socket opened!\";" + Newline +
            "  };" + Newline +
            "  socket.onclose = function(event) {" + Newline +
            "    var ta = document.getElementById('responseText');" + Newline +
            "    ta.value = ta.value + \"Web Socket closed\"; " + Newline +
            "  };" + Newline +
            "} else {" + Newline +
            "  alert(\"Your browser does not support Web Socket.\");" + Newline +
            '}' + Newline +
            Newline +
            "function send(message) {" + Newline +
            "  if (!window.WebSocket) { return; }" + Newline +
            "  if (socket.readyState == WebSocket.OPEN) {" + Newline +
            "    socket.send(message);" + Newline +
            "  } else {" + Newline +
            "    alert(\"The socket is not open.\");" + Newline +
            "  }" + Newline +
            '}' + Newline +
            "</script>" + Newline +
            "<form onsubmit=\"return false;\">" + Newline +
            "<input type=\"text\" name=\"message\" value=\"Hello, World!\"/>" +
            "<input type=\"button\" value=\"Send Web Socket Data\"" + Newline +
            "       onclick=\"send(this.form.message.value)\" />" + Newline +
            "<h3>Output</h3>" + Newline +
            "<textarea id=\"responseText\" style=\"width:500px;height:300px;\"></textarea>" + Newline +
            "</form>" + Newline +
            "</body>" + Newline +
            "</html>" + Newline,
            Encoding.ASCII);
    }
}
