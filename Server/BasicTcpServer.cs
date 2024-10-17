using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace BasicTcp
{
	public class BasicTcpServer
	{
		public class Connection
		{
			public delegate void MessageReceivedEventHandler(Connection con, ReadOnlySpan<byte> data);
			public event MessageReceivedEventHandler? MessageReceived;
			public delegate void DisconnectedEventHandler(Connection con);
			public event DisconnectedEventHandler? Disconnected;
			public object? UserData { get; set; }
			public void Send(ReadOnlySpan<byte> message)
			{
			}
			public void Close()
			{
			}
		}
		public delegate void NewConnectionEventHandler(BasicTcpServer server, Connection con);
		public event NewConnectionEventHandler? NewConnection;
		public void StartListen(IPEndPoint endPoint, string handshakeSecret)
		{
		}
		public void StopListen()
		{
		}
		public void CloseAllConnections()
		{
		}
	}
}
