using System.Net.WebSockets;

namespace SQCScanner.websoketManager
{
    public class WebSocketConnectionManager
    {
        private readonly List<WebSocket> _sockets = new();

        public void AddSocket(WebSocket socket)
        {
            _sockets.Add(socket);
        }
        public List<WebSocket> GetAllSockets() => _sockets;
    }
}
