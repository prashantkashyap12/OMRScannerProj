using System.Net.WebSockets;
using System.Text;
using static System.Net.Mime.MediaTypeNames;
using Syncfusion.EJ2.Notifications;
using TesseractOCR.Pix;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using Newtonsoft.Json.Linq;
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

        //public async Task BroadcastMessageAsync(string message)
        //{
        //    var sockets = _connectionManager.GetAllSockets();
        //    var bytes = Encoding.UTF8.GetBytes(message);
        //    foreach (var socket in sockets)
        //    {
        //        if (socket.State == WebSocketState.Open)
        //        {
        //            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        //        }
        //    }
        //}


        // Send message to a specific user by userId
        public async Task UserMessageAsync(string userId, string message)
        {
            var socket = _connectionManager.GetSocketByUserId(userId);

            if (socket != null && socket.State == WebSocketState.Open)
            {
                var bytes = Encoding.UTF8.GetBytes(message);
                try
                {
                    await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch
                {
                    _connectionManager.RemoveSocket(userId);
                }
            }
        }


    }
}
