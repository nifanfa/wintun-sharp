using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

internal unsafe class Program
{
    public static ushort checksum(ushort* addr, uint count)
    {
        ulong sum = 0;
        while (count > 1)
        {
            sum += *addr++;
            count -= 2;
        }
        if (count > 0)
        {
            sum += (ulong)((*addr) & ntohs(0xFF00));
        }
        while ((sum >> 16) != 0)
        {
            sum = (sum & 0xffff) + (sum >> 16);
        }
        sum = ~sum;
        return ((ushort)sum);
    }

    public static ushort ntohs(ushort Value)
    {
        return ((ushort)((((Value) & 0xff) << 8) | (((Value) & 0xff00) >> 8)));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

    public delegate bool HandlerRoutine(CtrlTypes CtrlType);

    public enum CtrlTypes
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT,
        CTRL_CLOSE_EVENT,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT
    }

    static void Main(string[] args)
    {
        string ip = "10.6.7.7";
        string gateway = "10.6.7.1";

        Process.Start("cmd.exe", $"/c start ping -t {gateway}");
        wintun tun = new wintun("Demo", "Example");
        tun.Configure(IPAddress.Parse(ip), IPAddress.Parse(gateway), 24);
        new Thread(() =>
        {
            for (; ; )
            {
                byte[] buffer = tun.Read();
                Console.WriteLine($"Packet read {buffer.Length} bytes!");

                //pretend the gateway exists
                //ip_header + icmp_header
                if (buffer.Length > 20 + 8)
                {
                    fixed (byte* ptr = buffer)
                    {
                        //icmp && request
                        if (ptr[9] == 1 && ptr[20] == 8)
                        {
                            Console.WriteLine("ICMP request!");

                            *(uint*)(ptr + 16) = *(uint*)(ptr + 12);
                            *(uint*)(ptr + 12) = (uint)IPAddress.Parse(gateway).Address;

                            *(ushort*)(ptr + 10) = 0;
                            *(ushort*)(ptr + 10) = checksum((ushort*)ptr, 20);

                            //response
                            *(ushort*)(ptr + 20) = 0;

                            *(ushort*)(ptr + 22) = 0;
                            *(ushort*)(ptr + 22) = checksum((ushort*)(ptr + 20), (uint)(buffer.Length - 20));

                            tun.Write(buffer);
                        }
                    }
                }
            }
        }).Start();
        Thread.Sleep(-1);
    }
}