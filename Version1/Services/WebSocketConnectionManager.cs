using System.Net.WebSockets;

namespace SQCScanner.Services
{
    public class WebSocketConnectionManager
    {
        private readonly List<WebSocket> _sockets = new();

        public void AddSocket(WebSocket socket)
        {
            _sockets.Add(socket);
        }

        public List<WebSocket> GetAllSockets()
        {
            return _sockets.Where(s => s.State == WebSocketState.Open).ToList();
        }
    }
}
