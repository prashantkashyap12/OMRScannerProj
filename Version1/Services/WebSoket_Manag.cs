using System.Net.WebSockets;
using System.Text;

namespace SQCScanner.Services
{
    public class WebSoketHandler
    {
        private readonly WebSocketConnectionManager _connectionManager;

        public WebSoketHandler(WebSocketConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        public async Task BroadcastMessageAsync(string message)
        {
            var sockets = _connectionManager.GetAllSockets();
            var bytes = Encoding.UTF8.GetBytes(message);

            foreach (var socket in sockets)
            {
                if (socket.State == WebSocketState.Open)
                {
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
            }
        }
    }
}
