using System;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace SQCScanner.websoketManager
{
    // WebSocket class hai jo objects ko ek list mein store karti hai.
    // aur provide karti hai.
    public class WebSocketConnectionManager
    {
        // har connected client ka WebSocket object is list mein store hoga. yh Blank List Declear.
        private readonly List<WebSocket> _sockets = new();
        //naya client connect hota hai, uska WebSocket object is method ke through list mein add kiya.
        public void AddSocket(WebSocket socket)
        {
            _sockets.Add(socket);
        }
        //broadcast list ko provide karta hai.
        public List<WebSocket> GetAllSockets() => _sockets;
    }
}
