using System.Net.WebSockets;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Syncfusion.EJ2.Notifications;
using TesseractOCR.Pix;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
namespace SQCScanner.websoketManager
{
    public class WebSoketHandler
    {
        // 
        private readonly WebSocketConnectionManager _connectionManager;
        public WebSoketHandler(WebSocketConnectionManager connectionManager)
        {
            _connectionManager = connectionManager;
        }

        // Sabhi connected websoket client ko msg - Broadcast karna
        // Like - process hui, status update hua, notification aayi
        public async Task BroadcastMessageAsync(string message)
        {
            // Sabhi connected WebSocket clients ki list le aata hai.
            var sockets = _connectionManager.GetAllSockets();

            // Message ko byte array mein convert karta hai(kyunki WebSocket binary/ text data hi bhejta hai
            var bytes = Encoding.UTF8.GetBytes(message);

            // Sabhi open connections par loop chala ke message bhejta hai
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
