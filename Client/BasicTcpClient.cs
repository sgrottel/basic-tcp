using System;
using System.Collections.Generic;
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
				rd.lengthToReceive = 4;
				rd.state = ReceiveState.MessageLength;
				rd.connection = connection;
				connection.GetStream().BeginRead(rd.buffer, rd.pos, rd.lengthToReceive, OnDataReceived, rd);
			}
		}

		public void Send(byte[] message)
		{
			Send([message]);
		}

		public void Send(ReadOnlyMemory<byte> message)
		{
			Send([message]);
		}

		/// <summary>
		/// Sends one message split in multiple parts
		/// This does NOT send multiple messages!
		/// </summary>
		public void Send(IEnumerable<ReadOnlyMemory<byte>> messageParts)
		{
			lock (sync)
			{
				var s = connection?.GetStream();
				if (s == null) throw new Exception("Not connected");

				UInt32 totalLen = 0;
				if (messageParts != null)
				{
					foreach (ReadOnlyMemory<byte> part in messageParts)
					{
						totalLen += (uint)part.Length;
					}
				}
				s.Write(BitConverter.GetBytes(totalLen));
				if (totalLen > 0)
				{
					foreach(ReadOnlyMemory<byte> part in messageParts!)
					{
						if (part.Length == 0) continue;
						s.Write(part.Span);
					}
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
}
