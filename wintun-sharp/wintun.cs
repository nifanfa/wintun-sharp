using System.Net;
using System.Runtime.InteropServices;

internal unsafe class wintun
{
    [DllImport("wintun.dll", SetLastError = true)]
    private static extern IntPtr WintunCreateAdapter(
        [MarshalAs(UnmanagedType.LPWStr)]
        string Name,
        [MarshalAs(UnmanagedType.LPWStr)]
        string TunnelType,
        ref Guid guid);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern uint WintunGetRunningDriverVersion();

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern void WintunGetAdapterLUID(IntPtr Adapter, out ulong Luid);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern IntPtr WintunStartSession(IntPtr Adapter, uint Capacity);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern IntPtr WintunGetReadWaitEvent(IntPtr Session);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern IntPtr WintunReceivePacket(IntPtr Session, out uint PacketSize);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern void WintunSendPacket(IntPtr Session, IntPtr Packet);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern void WintunEndSession(IntPtr Session);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern void WintunCloseAdapter(IntPtr Adapter);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern IntPtr WintunAllocateSendPacket(IntPtr Session, uint PacketSize);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern IntPtr WintunOpenAdapter(
        [MarshalAs(UnmanagedType.LPWStr)]
        string Name);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern bool WintunDeleteDriver();

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern void WintunReleaseReceivePacket(IntPtr Session, IntPtr Packet);

    [DllImport("wintun.dll", SetLastError = true)]
    private static extern void WintunSetLogger(WINTUN_LOGGER_CALLBACK NewLogger);

    private delegate void WINTUN_LOGGER_CALLBACK(
        WINTUN_LOGGER_LEVEL Level,
        ulong Timestamp,
        [MarshalAs(UnmanagedType.LPWStr)]
        string Message);

    private enum WINTUN_LOGGER_LEVEL
    {
        WINTUN_LOG_INFO, /**< Informational */
        WINTUN_LOG_WARN, /**< Warning */
        WINTUN_LOG_ERR   /**< Error */
    }

    private IntPtr WaitHandle, Adapter, Session;

    public wintun(string Name, string TunnelType)
    {
        Guid ExampleGuid = Guid.NewGuid();
        Adapter = WintunCreateAdapter(Name, TunnelType, ref ExampleGuid);
        if (Adapter == 0)
        {
            throw new InvalidOperationException($"Failed to create adapter {Marshal.GetLastWin32Error():x2}");
        }
        uint Version = WintunGetRunningDriverVersion();
        Console.WriteLine($"Wintun v{(Version >> 16) & 0xff}.{(Version >> 0) & 0xff} loaded");
        Session = WintunStartSession(Adapter, 0x400000);
        if (Session == 0)
        {
            throw new InvalidOperationException($"Failed to create adapter");
        }

        WaitHandle = WintunGetReadWaitEvent(Session);
    }

    [DllImport("kernel32.dll")]
    private static extern uint WaitForSingleObject(IntPtr hHandle, uint dwMilliseconds);

    public byte[] Read()
    {
        for (; ; )
        {
            IntPtr Packet = WintunReceivePacket(Session, out var PacketSize);
            if (Packet != 0)
            {
                byte[] bytes = new byte[PacketSize];
                Marshal.Copy(Packet, bytes, 0, bytes.Length);
                WintunReleaseReceivePacket(Session, Packet);
                return bytes;
            }
            else
            {
                if (Marshal.GetLastWin32Error() == 259L)
                {
                    WaitForSingleObject(WaitHandle, 0xFFFFFFFF);
                }
            }
        }
    }

    public void Write(byte[] buffer)
    {
        IntPtr Packet = WintunAllocateSendPacket(Session, (uint)buffer.Length);
        if (Packet != 0)
        {
            Marshal.Copy(buffer, 0, Packet, buffer.Length);
            WintunSendPacket(Session, Packet);
        }
        else
        {
            if (Marshal.GetLastWin32Error() == 111L)
            {
                Console.WriteLine("Packet write failed");
            }
        }
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 80)]
    private struct MIB_UNICASTIPADDRESS_ROW
    {
        [FieldOffset(0)]
        public ushort sin_family;
        [FieldOffset(4)]
        public uint sin_addr;
        [FieldOffset(32)]
        public ulong InterfaceLuid;
        [FieldOffset(60)]
        public byte OnLinkPrefixLength;
        [FieldOffset(64)]
        public int DadState;
    }

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 104)]
    private struct MIB_IPFORWARD_ROW2
    {
        [FieldOffset(0)]
        public ulong InterfaceLuid;
        [FieldOffset(12)]
        public ushort si_family;
        [FieldOffset(16)]
        public uint sin_addr;
        [FieldOffset(40)]
        public byte PrefixLength;
        [FieldOffset(48)]
        public uint NextHop_sin_addr;
        [FieldOffset(44)]
        public ushort NextHop_si_family;
        [FieldOffset(84)]
        public uint Metric;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern void InitializeUnicastIpAddressEntry(ref MIB_UNICASTIPADDRESS_ROW Row);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint CreateUnicastIpAddressEntry(ref MIB_UNICASTIPADDRESS_ROW Row);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern void InitializeIpForwardEntry(ref MIB_IPFORWARD_ROW2 Row);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint CreateIpForwardEntry2(ref MIB_IPFORWARD_ROW2 Row);

    public void Configure(IPAddress Address, IPAddress Gateway, byte PrefixLength)
    {
        WintunGetAdapterLUID(Adapter, out ulong Luid);
        {
            MIB_UNICASTIPADDRESS_ROW AddressRow = default;
            InitializeUnicastIpAddressEntry(ref AddressRow);
            AddressRow.sin_family = 2;
            AddressRow.sin_addr = (uint)Address.Address;
            AddressRow.OnLinkPrefixLength = PrefixLength;
            AddressRow.DadState = 4;
            AddressRow.InterfaceLuid = Luid;
            uint LastError = CreateUnicastIpAddressEntry(ref AddressRow);
            if (LastError != 0) throw new InvalidOperationException();
        }
        {
            MIB_IPFORWARD_ROW2 row = default;
            InitializeIpForwardEntry(ref row);
            row.InterfaceLuid = Luid;
            row.PrefixLength = 0;
            row.si_family = 2;
            row.NextHop_si_family = 2;
            row.sin_addr = 0;
            row.NextHop_sin_addr = (uint)Gateway.Address;
            row.Metric = 9999;
            uint LastError = CreateIpForwardEntry2(ref row);
            if (LastError != 0) throw new InvalidOperationException();
        }
    }
}