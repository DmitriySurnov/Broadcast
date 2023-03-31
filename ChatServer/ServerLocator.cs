using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace ChatServer
{
    internal class ServerLocator : IDisposable
    {
        public static int Port = 0;
        private bool _isStarted;
        private Thread _serverLocatorSenderThread;
        private Thread _serverLocatorResieverThread;
        private Socket _udpBroadcastSocketReciever;
        private List<String> _listRequests;
        private static int _portReciever;
        private static object _lockListRequests;

        public ServerLocator()
        {
            _isStarted = false;
            _portReciever = 11111;
            _serverLocatorSenderThread = new Thread(ServerLocatorSender);
            _serverLocatorResieverThread = new Thread(ServerLocatorReciever);

            _udpBroadcastSocketReciever = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _udpBroadcastSocketReciever.EnableBroadcast = true;

            _listRequests = new List<String>();
            _lockListRequests = new object();
        }

        public void Start()
        {
            _isStarted = true;

            _serverLocatorSenderThread.Start();
            _serverLocatorResieverThread.Start();
        }

        public void Stop()
        {
            _isStarted = false;

            Task.Delay(100).Wait();

            _serverLocatorResieverThread.Abort();
            _serverLocatorSenderThread.Abort();
        }

        private void ServerLocatorSender()
        {
            while (_isStarted)
            {
                if (Port == 0)
                    continue;
                string IP_Adress_Port = "";
                lock (_lockListRequests)
                {
                    if (_listRequests.Count > 0)
                    {
                        IP_Adress_Port = _listRequests[0];
                    }
                }
                if (IP_Adress_Port == "")
                {
                    Task.Delay(100).Wait();
                    continue;
                }
                var mass = IP_Adress_Port.Split(':');
                if (mass.Length == 0)
                {
                    lock (_lockListRequests)
                    {
                        IP_Adress_Port.Remove(0);
                    }
                    continue;
                }
                Socket udpBroadcastSocketSender = new Socket(SocketType.Dgram, ProtocolType.Udp);
                udpBroadcastSocketSender.EnableBroadcast = true;
                IPAddress broadcastAddress = CreateBroadcastAddress();
                int port = int.Parse(mass[1]);
                Console.WriteLine("Sender ishod port - " + port);
                var broadcastIpEndPoint = new IPEndPoint(broadcastAddress, port);
                Console.WriteLine("Sender broadcastIpEndPoint port - " + port);
                udpBroadcastSocketSender.Connect(broadcastIpEndPoint);
                Console.WriteLine("Sender conect port - " + port);
                string Message = CreateLocalAddress() + ":" + Port + "&" + _portReciever;
                Console.WriteLine("ServerLocatorSender");
                Console.WriteLine(Message);
                SocketUtility.SendString(udpBroadcastSocketSender, Message, () => { });
                lock (_lockListRequests)
                {
                    _listRequests.RemoveAt(0);
                }
                Task.Delay(1000).Wait();
                udpBroadcastSocketSender.Close();
            }
        }

        private static string CreateLocalAddress()
        {
            return Dns
                                     .GetHostEntry(Dns.GetHostName())
                                     .AddressList
                                     .First(x => x.AddressFamily == AddressFamily.InterNetwork)
                                     .ToString();
        }

        private static IPAddress CreateBroadcastAddress()
        {
            var localIpAddess = Dns
                         .GetHostEntry(Dns.GetHostName())
                         .AddressList
                         .First(x => x.AddressFamily == AddressFamily.InterNetwork)
                         .ToString();
            var localIpAddessNumbers = localIpAddess.Split('.');
            localIpAddessNumbers[3] = "255";
            var remoteIpAddressInString = localIpAddessNumbers
                .Aggregate("", (acc, value) => $"{acc}.{value}")
                .Substring(1);
            var broadcastAddress = IPAddress.Parse(remoteIpAddressInString);
            return broadcastAddress;
        }

        private void ServerLocatorReciever()
        {
            byte[] dataBuffer = new byte[1024];
            try
            {
                _udpBroadcastSocketReciever.Bind(new IPEndPoint(IPAddress.Any, _portReciever));
                Console.WriteLine("Reciever port - " + _portReciever);
            }
            catch (SocketException ex)
            {
                _portReciever++;
                ServerLocatorReciever();
                return;
            }

            while (_isStarted)
            {
                while (_udpBroadcastSocketReciever.Available == 0)
                {
                    Task.Delay(100).Wait();
                }
                using (Stream dataStream = new MemoryStream())
                using (BinaryReader dataStreamReader = new BinaryReader(dataStream))
                {
                    string stroka = "";
                    long razmer = 0;
                    var receivedBufferSize = _udpBroadcastSocketReciever.Receive(dataBuffer);
                    dataStream.Seek(0, SeekOrigin.Begin);
                    dataStream.Write(dataBuffer, 0, sizeof(long));
                    dataStream.Seek(0, SeekOrigin.Begin);
                    razmer = dataStreamReader.ReadInt64();
                    dataStream.Seek(0, SeekOrigin.Begin);
                    dataStream.Write(dataBuffer, 8, Convert.ToInt32(razmer) + 1);
                    dataStream.Seek(0, SeekOrigin.Begin);
                    stroka = dataStreamReader.ReadString();
                    Console.WriteLine("ServerLocatorReciever");
                    Console.WriteLine(stroka);
                    lock (_lockListRequests)
                    {
                        _listRequests.Add(stroka);
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _udpBroadcastSocketReciever.Dispose();
        }
    }
}
