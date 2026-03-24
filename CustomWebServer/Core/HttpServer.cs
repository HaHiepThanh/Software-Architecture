using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CustomWebServer.Core
{
    public class HttpServer
    {
        private TcpListener _listener;
        private bool _isRunning = false;
        private readonly int _port;
        private readonly WebRouter _router;

        public HttpServer(WebRouter router, int port = 8080)
        {
            _router = router;
            _port = port;
            _listener = new TcpListener(IPAddress.Any, _port);
        }

        public void Start()
        {
            _listener.Start();
            _isRunning = true;
            Console.WriteLine($"[INFO] Server started on http://localhost:{_port}");

            // The main thread accepts client connections indefinitely
            while (_isRunning)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    
                    // Handle each connection concurrently in a separate thread pool thread
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Connection error: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener.Stop();
        }

        private void HandleClient(object state)
        {
            using (TcpClient client = (TcpClient)state)
            using (NetworkStream stream = client.GetStream())
            {
                try
                {
                    // 1. Receive Byte Stream from Socket
                    byte[] buffer = new byte[8192];
                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0) return;

                    // Convert to string to parse
                    string rawRequest = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                    
                    // 2. Parse into HttpRequest
                    HttpRequest request = HttpRequest.Parse(rawRequest);
                    
                    if (request != null)
                    {
                        Console.WriteLine($"[HTTP] {request.Method} {request.Url}");

                        // 3. Route Request
                        HttpResponse response = _router.Route(request);
                        
                        // 4. Send Response byte stream back to browser
                        byte[] responseBytes = response.ToBytes();
                        stream.Write(responseBytes, 0, responseBytes.Length);
                        stream.Flush();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Handling client: {ex.Message}");
                }
                finally
                {
                    client.Close();
                }
            }
        }
    }
}
