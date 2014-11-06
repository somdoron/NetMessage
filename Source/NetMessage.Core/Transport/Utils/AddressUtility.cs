using System;
using System.Net;
using System.Net.Sockets;

namespace NetMessage.Transport.Utils
{
    public static class AddressUtility
    {
        public static EndPoint ResolveAddress(string address, bool ip4Only)
        {
            int portPosition = address.LastIndexOf(':');

            if (portPosition == -1)
                throw new ArgumentException("No port specified", "address");

            portPosition++;

            int port;

            if (!int.TryParse(address.Substring(portPosition), out port) || port < 0 || port > 65535)
            {
                throw new ArgumentException("Invalid port specified", "address");
            }

            address = address.Substring(0, portPosition - 1);

            if (address == "*")
            {
                if (ip4Only)
                    return new IPEndPoint(IPAddress.Any, port);
                else
                    return new IPEndPoint(IPAddress.IPv6Any, port);
            }

            if (address == "localhost")
            {
                if (ip4Only)
                    return new IPEndPoint(IPAddress.Loopback, port);
                else
                    return new IPEndPoint(IPAddress.IPv6Loopback, port);
            }

            IPAddress ipAddress;

            if (IPAddress.TryParse(address, out ipAddress))
            {
                return new IPEndPoint(ipAddress, port);    
            }
            else
            {
                return new DnsEndPoint(address, port, ip4Only ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);   
            }            
        }
    }
}
