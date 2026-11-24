using System.Net;
using System.Net.Sockets;

namespace EventFlow.Infrastructure.Webhooks;

public sealed class BlockedAddressException(string message) : Exception(message);

/// <summary>
/// Proteção contra SSRF: impede que um webhook cadastrado faça o servidor chamar
/// endereços internos (localhost, rede privada, metadados de nuvem). A checagem é
/// feita no IP realmente conectado, então não é burlada por DNS.
/// </summary>
internal static class SafeConnect
{
    public static SocketsHttpHandler CreateHandler(bool allowPrivateNetworks) => new()
    {
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        AllowAutoRedirect = false,
        ConnectCallback = async (context, ct) =>
        {
            var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, ct);
            var allowed = addresses.Where(a => allowPrivateNetworks || IsPublic(a)).ToArray();
            if (allowed.Length == 0)
                throw new BlockedAddressException($"Endereço bloqueado para webhooks: {context.DnsEndPoint.Host}");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(allowed, context.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        },
    };

    public static bool IsPublic(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        if (IPAddress.IsLoopback(address) || address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any)) return false;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var b = address.GetAddressBytes();
            return !(b[0] == 10
                || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
                || (b[0] == 192 && b[1] == 168)
                || (b[0] == 169 && b[1] == 254)          // link-local / metadados de nuvem
                || (b[0] == 100 && b[1] >= 64 && b[1] <= 127) // CGNAT
                || b[0] == 0 || b[0] >= 224);
        }

        return !(address.IsIPv6LinkLocal || address.IsIPv6SiteLocal || address.IsIPv6Multicast
            || (address.GetAddressBytes()[0] & 0xFE) == 0xFC); // fc00::/7 (rede privada IPv6)
    }
}
