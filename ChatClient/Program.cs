using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace ChatClient
{
    internal class Program
    {
        private static bool _isStopPoiskServer = false;
        static void Main(string[] args)
        {
            using (var serverLocator = new ServerLocator())
            {
                serverLocator.Start();

                var socket = ServerSelection(serverLocator);

                var chatContent = ReceiveChatContent(socket);

                ShowChatContent(chatContent);

                //var message = GetClientMessage();

                //SendMessageToServer(socket, message);

                /*
                 * Потенциально будет нужна в ходе дальнейшей разработки
                 * В текущей версии строку ожидания Enter заменяет ожидание в
                 * 1 секунду ниже
                 */
                WaitForEnterPressedToCloseApplication();

                DisconnectClientFromServer(socket);

                Thread.Sleep(TimeSpan.FromSeconds(1));

                DisposeClientSocket(socket);
            }
        }

        private static Socket ServerSelection(ServerLocator locator)
        {
            Enter(locator);
            while (true)
            {
                if (locator.ServersFound() || _isStopPoiskServer)
                {
                    var servers = locator.Servers;
                    Console.WriteLine("Выбетие сервер: ");
                    for (int i = 0; i < servers.Count; i++)
                    {
                        Console.WriteLine($"{i} {servers[i]}");
                    }
                    Console.Write("Введите номер сервера к которому хотите подключиться - ");
                    int nomerServera = int.Parse(Console.ReadLine());
                    if (!(nomerServera >= 0 && servers.Count > nomerServera))
                    {
                        Console.WriteLine("Введен не правельный номер серрвера");
                        continue;
                    }
                    var mass = servers[nomerServera].Split(':');
                    return ConnectClientToServer(new IPEndPoint(IPAddress.Parse(mass[0]), int.Parse(mass[1])));
                }
                else
                {
                    Task.Delay(1000);
                    continue;
                }
            }
        }

        private static async void Enter(ServerLocator locator)
        {
            await Task.Run(() =>
            {
                ConsoleKeyInfo keyInfo;
                do
                {
                    keyInfo = Console.ReadKey();
                } while (keyInfo.Key != ConsoleKey.Enter);
            });
            locator.Stop();
            _isStopPoiskServer = true;
        }

        private static void DisposeClientSocket(Socket socket)
        {
            socket.Close();
            socket.Dispose();
        }

        private static void DisconnectClientFromServer(Socket socket)
        {
            socket.Disconnect(false);
            Console.WriteLine("Client disconnected from server");
        }

        private static void WaitForEnterPressedToCloseApplication()
        {
            Console.Write("Press [Enter] to close client console application");
            Console.ReadLine();
        }

        private static void SendMessageToServer(Socket socket, string message)
        {
            Console.WriteLine("Sending message to server");
            SocketUtility.SendString(socket, message,
                () => { Console.WriteLine($"Send string to server data check client side exception"); });
            Console.WriteLine("Message sent to server");
        }

        private static string GetClientMessage()
        {
            Console.Write("Your message:");
            var message = Console.ReadLine();
            return message;
        }

        private static void ShowChatContent(string chatContent)
        {
            Console.WriteLine("---------------Chat content--------------------");
            Console.WriteLine(chatContent);
            Console.WriteLine("------------End of chat content----------------");
            Console.WriteLine();
        }

        private static string ReceiveChatContent(Socket socket)
        {
            string chatContent = SocketUtility.ReceiveString(socket,
                () => { Console.WriteLine($"Receive string size check from server client side exception"); },
                () => { Console.WriteLine($"Receive string data check from server client side exception"); });
            return chatContent;
        }

        private static Socket ConnectClientToServer(IPEndPoint serverEndPoint)
        {
            Socket socket = new Socket(SocketType.Stream, ProtocolType.IP);

            socket.Connect(serverEndPoint);

            Console.WriteLine($"Client connected Local {socket.LocalEndPoint} Remote {socket.RemoteEndPoint}");

            return socket;
        }
    }
}
