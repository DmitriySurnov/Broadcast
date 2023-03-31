using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Utilities;

namespace ChatClient
{
    internal class ServerLocator : IDisposable
    {
        private static List<string> _servers;
        private static object _lockServers;
        private static object _locklistOtvetovServera;
        private List<string> _listOtvetovServera;
        private bool _isStarted;
        private Thread[] _serverLocatorSenderThread;
        private Thread _serverLocatorResieverThread;
        private Socket _udpBroadcastSocketSender;
        private Socket _udpBroadcastSocketResiever;
        private static int _portReciever;
        private static object _lockcountRequests;
        private static int countRequests;

        public List<string> Servers => _servers;

        public bool ServersFound()
        {
            lock (_lockcountRequests)
            {
                lock (_lockServers)
                {
                    return countRequests == 0 && _servers.Count > 0;
                }
            }
        }
        public ServerLocator()
        {
            _servers = new List<string>();
            _lockServers = new object();
            _isStarted = false;

            _serverLocatorSenderThread = new Thread[2];
            _serverLocatorSenderThread[0] = new Thread(ServerLocatorSender);
            //_serverLocatorSenderThread[1] = new Thread(ServerLocatorSender);

            _serverLocatorResieverThread = new Thread(ServerLocatorReciever);

            _udpBroadcastSocketSender = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _udpBroadcastSocketSender.EnableBroadcast = true;
            _udpBroadcastSocketResiever = new Socket(SocketType.Dgram, ProtocolType.Udp);
            _udpBroadcastSocketResiever.EnableBroadcast = true;
            _portReciever = CreatePort();
            _listOtvetovServera = new List<string>();
            _locklistOtvetovServera = new object();
            _lockcountRequests = new object();
            countRequests = 0;
        }

        public void Start()
        {
            _isStarted = true;
            _serverLocatorSenderThread[0].Start(11111);
            //_serverLocatorSenderThread[1].Start(11112);
            _serverLocatorResieverThread.Start();
        }

        public void Stop()
        {
            _isStarted = false;

            Task.Delay(100).Wait();

            _serverLocatorResieverThread.Abort();
            _serverLocatorSenderThread[0].Abort();
            //_serverLocatorSenderThread[1].Abort();
        }

        private void ServerLocatorSender(object nomerPortObj)
        {
            lock (_lockcountRequests)
            {
                countRequests++;
            }
            int nomerPort = (int)nomerPortObj;
            IPAddress broadcastAddress = CreateBroadcastAddress();
            var broadcastIpEndPoint = new IPEndPoint(broadcastAddress, nomerPort);
            _udpBroadcastSocketSender.Connect(broadcastIpEndPoint);
            string Message = CreateLocalAddress() + ":" + _portReciever;

            while (_isStarted)
            {
                lock (_locklistOtvetovServera)
                {
                    if (_listOtvetovServera.Count > 0)
                        if (_listOtvetovServera.Contains(nomerPort.ToString()))
                        {
                            _listOtvetovServera.Remove(nomerPort.ToString());
                            lock (_lockcountRequests)
                            {
                                countRequests--;
                            }
                            break;
                        }
                }
                Console.WriteLine("ServerLocatorSender - " + nomerPort);
                Console.WriteLine(Message);
                Console.WriteLine("");
                SocketUtility.SendString(_udpBroadcastSocketSender, Message, () => { });
                Task.Delay(2000).Wait();
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

        private static int CreatePort()
        {
            Random rnd = new Random();
            int value = rnd.Next(0, 10);
            value += 11000;
            return value;
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
            _udpBroadcastSocketResiever.Bind(new IPEndPoint(IPAddress.Any, _portReciever));

            while (_isStarted)
            {
                lock (_lockcountRequests)
                {
                    lock (_lockServers)
                    {
                        if (countRequests == 0 && _servers.Count > 0)
                        {
                            _isStarted = false;
                            _udpBroadcastSocketResiever.Close();
                            _udpBroadcastSocketSender.Close();
                            continue;
                        }
                    }
                }
                while (_udpBroadcastSocketResiever.Available == 0)
                {
                    Task.Delay(100).Wait();
                }
                using (Stream dataStream = new MemoryStream())
                using (BinaryReader dataStreamReader = new BinaryReader(dataStream))
                {
                    string stroka = "";
                    long razmer = 0;
                    var receivedBufferSize = _udpBroadcastSocketResiever.Receive(dataBuffer);
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
                    Console.WriteLine("");
                    var mass = stroka.Split('&');
                    if (mass.Length == 0)
                        continue;
                    lock (_lockServers)
                    {
                        if (!_servers.Contains(mass[0]))
                        {
                            _servers.Add(mass[0]);
                            //Console.WriteLine(stroka);
                            lock (_locklistOtvetovServera)
                            {
                                _listOtvetovServera.Add(mass[1]);
                            }
                        }
                    }
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _udpBroadcastSocketSender.Dispose();
        }
    }
}
