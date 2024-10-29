using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace BasicTcp
{
	internal class Program
	{
		static void Main(string[] args)
		{
			try
			{
				IPEndPoint endPoint = new(IPAddress.Loopback, 55671);
				Console.WriteLine($"Basic TCP Client {endPoint}");

				BasicTcpClient client = new();

				client.MessageReceived += (con, dat) =>
				{
					string s = Encoding.UTF8.GetString(dat);
					Console.WriteLine($"Received: {s}");
				};

				client.Disconnected += (con) =>
				{
					Console.WriteLine("Disconnected.");
				};

				bool running = true;
				while (running)
				{
					Console.Write("> ");
					string input = (Console.ReadLine() ?? string.Empty);

					try
					{
						switch (input.ToLowerInvariant())
						{
							case "exit":
								Console.WriteLine("Exiting");
								client.Close();
								running = false;
								break;

							case "connect":
								Console.WriteLine("Connecting");
								client.Connect(endPoint, "It's_a_secret_to_everybody");
								break;

							case "close":
								Console.WriteLine("Closing");
								client.Close();
								break;

							default:
								Console.WriteLine("Sending");
								{
									List<byte[]> data = new();
									string[] strs = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
									for (int i = 1; i < strs.Length; ++i)
									{
										strs[i] = " " + strs[i];
									}
									client.Send(strs.Select(s => new ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(s))));
								}
								break;
						}
					}
					catch (Exception ex)
					{
						Console.WriteLine($"ERROR: {ex}");
					}
				}

				Console.WriteLine("Closing");
				client.Close();

			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: {ex}");
			}
		}
	}
}
