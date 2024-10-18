using System;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

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
			lock (sync)
			{
				Close();
				TcpClient client = new TcpClient();
				client.Connect(endPoint);

				byte[] handshake = SHA512.HashData(Encoding.UTF8.GetBytes("BasicTcp" + handshakeSecret));
				var s = client.GetStream();
				s.Write(handshake);
				byte[] answer = new byte[1];
				s.ReadExactly(answer);

				if (answer[0] != 1)
				{
					client.Close();
					throw new Exception("Handshake connection failed");
				}

				connection = client;

				ReceiveData rd = new();
				rd.target = 4;
				rd.receivingLen = true;
				rd.connection = connection;
				connection.GetStream().BeginRead(rd.buffer, rd.pos, rd.target, OnDataReceived, rd);
			}
		}

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
			}
			Disconnected?.Invoke(this);
		}

		private class ReceiveData
		{
			public byte[] buffer = new byte[1024];
			public int pos = 0;
			public int target;
			public bool receivingLen = true;
			public TcpClient? connection;
		}

		private object sync = new object();
		private TcpClient? connection;

		private void OnDataReceived(IAsyncResult ar)
		{
			lock(sync)
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

				try
				{
					connection.GetStream().BeginRead(rd.buffer, rd.pos, rd.target - rd.pos, OnDataReceived, rd);
				}
				catch
				{
					Close();
					return;
				}
			}
		}

	}
}
