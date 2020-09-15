using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;


namespace HA
{
    class WebsocketServer
    {
        public async void Start(string httpListenerPrefix)
        {
            HttpListener httpListener = new HttpListener();
            httpListener.Prefixes.Add(httpListenerPrefix);
            httpListener.Start();
            Console.WriteLine("Listening...");

            while (true)
            {
                HttpListenerContext httpListenerContext = await httpListener.GetContextAsync();
                if (httpListenerContext.Request.IsWebSocketRequest)
                {
                    ProcessRequest(httpListenerContext);
                }
                else
                {
                    //httpListenerContext.Response.StatusCode = 400;
                    //httpListenerContext.Response.Close();

                    byte[] buffer = Encoding.UTF8.GetBytes("<HTML><BODY><h1> " + DateTime.Now.ToString() + " </h1></BODY></HTML>");
                    httpListenerContext.Response.ContentLength64 = buffer.Length;
                    Stream output = httpListenerContext.Response.OutputStream;
                    output.Write(buffer, 0, buffer.Length);
                    output.Close();
                }
            }
        }

        static List<WebSocket> _clients = new List<WebSocket>() { };
        static readonly Object _lock = new Object();

        private async void ProcessRequest(HttpListenerContext httpListenerContext)
        {
            WebSocketContext webSocketContext = null;
            try
            {
                webSocketContext = await httpListenerContext.AcceptWebSocketAsync(subProtocol: null);
                string ipAddress = httpListenerContext.Request.RemoteEndPoint.Address.ToString();
                Console.WriteLine("Connected: IPAddress {0}", ipAddress);
            }
            catch (Exception e)
            {
                httpListenerContext.Response.StatusCode = 500;
                httpListenerContext.Response.Close();
                Console.WriteLine("Exception: {0}", e);
                return;
            }

            WebSocket webSocket = webSocketContext.WebSocket;
            lock (_lock) _clients.Add(webSocket);

            try
            {

                byte[] receiveBuffer = new byte[1024 * 64];
                while (webSocket.State == WebSocketState.Open)
                {
                    WebSocketReceiveResult receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), CancellationToken.None);
                    if (receiveResult.MessageType == WebSocketMessageType.Close)
                    {
                        lock (_lock) _clients.Remove(webSocket);
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
                    }
                    else
                    {
                        string message = Encoding.UTF8.GetString(receiveBuffer).TrimEnd('\0');
                        Console.WriteLine("-> SERVER: " + message);

                        byte[] bsend = Encoding.UTF8.GetBytes(Guid.NewGuid().ToString());
                        var adata = new ArraySegment<byte>(bsend, 0, bsend.Length);
                        lock (_lock)
                            foreach (var socket in _clients)
                                socket.SendAsync(adata, WebSocketMessageType.Text, receiveResult.EndOfMessage, CancellationToken.None).Wait();
                        webSocket.SendAsync(adata, WebSocketMessageType.Text, receiveResult.EndOfMessage, CancellationToken.None).Wait();

                        await webSocket.SendAsync(adata, WebSocketMessageType.Binary, receiveResult.EndOfMessage, CancellationToken.None);
                    }
                }
            }
            catch (Exception e)
            {
                //Console.WriteLine("Exception: {0}", e);
                Console.WriteLine("Exception: {0}", e.Message);
                lock (_lock) _clients.Remove(webSocket);
            }
            finally
            {
                if (webSocket != null)

                    webSocket.Dispose();
            }
        }


    }
}
