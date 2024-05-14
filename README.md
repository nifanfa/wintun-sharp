# wintun-sharp
wintun implementation for C#  
<img src="https://www.wintun.net/img/wintun.png" alt="drawing" width="200"/>  
  
wintun.dll can be found at https://www.wintun.net/  
# Synopsis  
Wintun is a very simple and minimal TUN driver for the Windows kernel, which provides userspace programs with a simple network adapter for reading and writing packets. It is akin to Linux's /dev/net/tun and BSD's /dev/tun. Originally designed for use in WireGuard, Wintun is meant to be generally useful for a wide variety of layer 3 networking protocols and experiments. The driver is open source, so anybody can inspect and build it. Due to Microsoft's driver signing requirements, we provide precompiled and signed versions that may be distributed with your software. The goal of the project is to be as simple as possible, opting to do things in the most pure and straight-forward way provided by NDIS.
