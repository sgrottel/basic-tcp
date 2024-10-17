using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

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
				lock (sync)
				{
					var s = client?.GetStream();
					if (s == null) throw new Exception("Not connected");

					s.Write(BitConverter.GetBytes((UInt32)message.Length));
					if (message.Length > 0)
					{
						s.Write(message);
					}
				}
			}

			public void Close()
			{
				lock (sync)
				{
					if (client == null) return;
					try
					{
						client.Close();
					}
					catch { }
					client = null;
					Disconnected?.Invoke(this);

					if (owner.TryGetTarget(out BasicTcpServer? server))
					{
						try
						{
							lock (server.sync)
							{
								server.clients.Remove(this);
							}
						}
						catch { }
					}
				}
			}

			private class ReceiveData
			{
				public byte[] buffer = new byte[1024];
				public int pos = 0;
				public int target;
				public bool receivingLen = true;
			}

			private TcpClient? client;
			private WeakReference<BasicTcpServer> owner;
			private object sync = new object();

			internal Connection(BasicTcpServer owner, TcpClient client)
			{
				this.client = client;
				this.owner = new(owner);

				ReceiveData rd = new();
				rd.target = 4;
				rd.receivingLen = true;
				this.client.GetStream().BeginRead(rd.buffer, rd.pos, rd.target, OnDataReceived, rd);
			}

			private void OnDataReceived(IAsyncResult ar)
			{
				lock (sync)
				{
					if (client == null) return;

					if (!client.Connected)
					{
						Close();
						return;
					}

					int len = 0;
					try
					{
						len = client.GetStream().EndRead(ar);
					}
					catch { }
					if (len == 0)
					{
						Close();
						return;
					}

					ReceiveData rd = (ReceiveData)ar.AsyncState!;
					if (rd.pos + len == rd.target)
					{
						if (rd.receivingLen)
						{
							uint msglen = BitConverter.ToUInt32(rd.buffer, 0);
							if (msglen > 0)
							{
								rd.pos = 0;
								rd.target = (int)msglen;
								if (rd.buffer.Length < rd.target)
								{
									rd.buffer = new byte[rd.target];
								}
								rd.receivingLen = false;
							}
							else
							{
								rd.pos = 0;
								rd.target = 4;
								rd.receivingLen = true;
							}
						}
						else
						{
							MessageReceived?.Invoke(this, rd.buffer.AsSpan(0, rd.target));

							rd.pos = 0;
							rd.target = 4;
							rd.receivingLen = true;
						}
					}
					else
					{
						rd.pos += len;
					}

					client.GetStream().BeginRead(rd.buffer, rd.pos, rd.target, OnDataReceived, rd);
				}
			}

		}

		public delegate void NewConnectionEventHandler(BasicTcpServer server, Connection con);

		public event NewConnectionEventHandler? NewConnection;

		public void StartListen(IPEndPoint endPoint, string handshakeSecret)
		{
			lock (sync)
			{
				StopListen();

				handshakeData = SHA512.HashData(Encoding.UTF8.GetBytes("BasicTcp" + handshakeSecret));

				listener = new TcpListener(endPoint);
				listener.Start();
				listener.BeginAcceptTcpClient(OnAccept, null);
			}
		}

		public void StopListen()
		{
			lock (sync)
			{
				listener?.Stop();
				listener = null;
			}
		}

		public void CloseAllConnections()
		{
			Connection[] cons;
			lock (sync)
			{
				cons = clients.ToArray();
				clients.Clear();
			}
			foreach (Connection c in cons)
			{
				c.Close();
			}
		}

		private byte[] handshakeData = [64];
		private TcpListener? listener;
		private List<Connection> clients = new();
		private object sync = new object();

		private void OnAccept(IAsyncResult ar)
		{
			lock (sync)
			{
				TcpClient? client = listener?.EndAcceptTcpClient(ar);
				if (client != null)
				{
					client.ReceiveTimeout = 1000;

					try
					{
						var s = client.GetStream();

						byte[] handshake = new byte[64];
						s.ReadExactly(handshake);

						client.ReceiveTimeout = 0;

						if (handshake.SequenceEqual(handshakeData))
						{
							byte[] answer = [1];
							s.Write(answer);

							Connection con = new(this, client);

							clients.Add(con);

							NewConnection?.Invoke(this, con);

						}
						else
						{
							byte[] answer = [0];
							s.Write(answer);
							client.Close();
						}

					}
					catch
					{
						try
						{
							client?.Close();
						}
						catch { }
					}
				}
				listener?.BeginAcceptTcpClient(OnAccept, null);
			}
		}

	}
}
