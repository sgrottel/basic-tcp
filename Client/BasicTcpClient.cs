using System.Net;

namespace BasicTcp
{
	public class BasicTcpClient
	{
		public delegate void MessageReceivedEventHandler(BasicTcpClient con, ReadOnlySpan<byte> data);
		public event MessageReceivedEventHandler? MessageReceived;
		public delegate void DisconnectedEventHandler(BasicTcpClient con);
		public event DisconnectedEventHandler? Disconnected;
		public void Connect(IPEndPoint endPoint, string handshakeSecret)
		{
		}
		public void Send(ReadOnlySpan<byte> message)
		{
		}
		public void Close()
		{
		}
	}
}
