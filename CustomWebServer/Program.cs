using System;
using CustomWebServer.Core;

namespace CustomWebServer
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Initializing Socket-based Web Server...");

            WebRouter router = new WebRouter();
            HttpServer server = new HttpServer(router, 8080);
            server.Start();
        }
    }
}
