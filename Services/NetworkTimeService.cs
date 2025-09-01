using System;
using System.Net;
using System.Net.Sockets;

namespace BackupApp.Services
{
    public class NetworkTimeService
    {
        private static readonly string[] NtpServers = {
            "time.windows.com",
            "time.google.com",
            "pool.ntp.org"
        };

        public DateTime GetNetworkTime()
        {
            Exception lastException = null;
            foreach (var server in NtpServers)
            {
                try
                {
                    var ntpData = new byte[48];
                    ntpData[0] = 0x1B; // LeapIndicator = 0, Version = 3, Mode = 3

                    var addresses = Dns.GetHostEntry(server).AddressList;
                    var ipEndPoint = new IPEndPoint(addresses[0], 123);
                    
                    using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        socket.ReceiveTimeout = 3000;
                        socket.Connect(ipEndPoint);
                        socket.Send(ntpData);
                        socket.Receive(ntpData);
                        socket.Close();
                    }

                    ulong intPart = (ulong)ntpData[40] << 24 | (ulong)ntpData[41] << 16 | (ulong)ntpData[42] << 8 | ntpData[43];
                    ulong fractPart = (ulong)ntpData[44] << 24 | (ulong)ntpData[45] << 16 | (ulong)ntpData[46] << 8 | ntpData[47];

                    var milliseconds = (intPart * 1000) + ((fractPart * 1000) / 0x100000000L);
                    var networkDateTime = new DateTime(1900, 1, 1).AddMilliseconds((long)milliseconds);

                    return networkDateTime.ToLocalTime();
                }
                catch (Exception ex)
                {
                    lastException = ex;
                    continue;
                }
            }
            
            throw new Exception($"Ne mogu da dobijem mrežno vreme: {lastException?.Message}", lastException);
        }
    }
}
