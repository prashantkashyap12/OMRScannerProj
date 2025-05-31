using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.WebSockets;

namespace SQCScanner.websoketManager
{
    // WebSocket class hai jo objects ko ek list mein store karti hai.
    // aur provide karti hai.
    public class WebSocketConnectionManager
    {
        // har connected client ka WebSocket object is list mein store hoga. yh Blank List Declear.
        private readonly ConcurrentDictionary<string, WebSocket> _userSockets = new();

        //naya client connect hota hai, uska WebSocket object is method ke through list mein add kiya.
        // Add or update socket for user.


        public void AddSocket(string userId, WebSocket socket)
        {
            _userSockets.AddOrUpdate(userId, socket, (key, oldSocket) =>
            {
                if (oldSocket.State == WebSocketState.Open)
                {
                    oldSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Replaced by new connection", CancellationToken.None).Wait();
                }
                _userSockets[userId] = socket;
                return socket;
            });
        }

        // Get socket for userId
        public WebSocket GetSocketByUserId(string userId)
        {
            if (_userSockets.TryGetValue(userId, out var socket))
            {
                return socket;
            }
            return null;
        }

        // Remove socket for user
        public void RemoveSocket(string userId)
        {
            if (_userSockets.TryRemove(userId, out var socket))
            {
                if (socket.State == WebSocketState.Open)
                {
                    socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Connection closed", CancellationToken.None).Wait();
                }
            }
        }


        //broadcast list ko provide karta hai. matlab jitne user hai jo connect hai unki list data hai
        public IEnumerable<WebSocket> GetAllSockets() => _userSockets.Values;
    }
}
