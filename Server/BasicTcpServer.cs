using System;
using System.Collections.Generic;
using System.Linq;
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
					var s = connection?.GetStream();
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
					if (connection == null) return;
					try
					{
						connection.Close();
					}
					catch { }
					connection = null;
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

			private enum ReceiveState
			{
				MessageLength,
				MessageData
			};

			private class ReceiveData
			{
				public byte[] buffer = new byte[1024];
				// The write position with the buffer
				public int pos = 0;
				// The number of bytes expected to be received in total
				public int lengthToReceive = 0;
				// The state of what element of the message to receive next
				public ReceiveState state;
				// The connection receive from
				public TcpClient? connection;
			}

			private TcpClient? connection;
			private WeakReference<BasicTcpServer> owner;
			private object sync = new object();

			internal Connection(BasicTcpServer owner, TcpClient client)
			{
				connection = client;
				this.owner = new(owner);

				ReceiveData rd = new();
				rd.lengthToReceive = 4;
				rd.state = ReceiveState.MessageLength;
				rd.connection = client;
				connection.GetStream().BeginRead(rd.buffer, rd.pos, rd.lengthToReceive, OnDataReceived, rd);
			}

			private void OnDataReceived(IAsyncResult ar)
			{
				lock (sync)
				{
					if (connection == null) return;

					ReceiveData rd = (ReceiveData)ar.AsyncState!;
					if (rd.connection != connection)
					{
						// old connection already closed
						// can happen here, because we can reuse the BasicTcpClient objects
						return;
					}

					if (!connection.Connected)
					{
						Close();
						return;
					}

					int len = 0;
					try
					{
						len = connection.GetStream().EndRead(ar);
					}
					catch { }
					if (len == 0)
					{
						Close();
						return;
					}

					if (rd.pos + len == rd.lengthToReceive)
					{
						if (rd.state == ReceiveState.MessageLength)
						{
							uint msglen = BitConverter.ToUInt32(rd.buffer, 0);
							if (msglen > 0)
							{
								rd.pos = 0;
								rd.lengthToReceive = (int)msglen;
								if (rd.buffer.Length < rd.lengthToReceive)
								{
									rd.buffer = new byte[rd.lengthToReceive];
								}
								rd.state = ReceiveState.MessageData;
							}
							else
							{
								rd.pos = 0;
								rd.lengthToReceive = 4;
								rd.state = ReceiveState.MessageLength;
							}
						}
						else
						{
							MessageReceived?.Invoke(this, rd.buffer.AsSpan(0, rd.lengthToReceive));

							rd.pos = 0;
							rd.lengthToReceive = 4;
							rd.state = ReceiveState.MessageLength;
						}
					}
					else
					{
						rd.pos += len;
					}

					try
					{
						connection.GetStream().BeginRead(rd.buffer, rd.pos, rd.lengthToReceive - rd.pos, OnDataReceived, rd);
					}
					catch
					{
						Close();
						return;
					}
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
