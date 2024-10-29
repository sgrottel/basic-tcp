using System;
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
					string input = (Console.ReadLine() ?? string.Empty).ToLowerInvariant();

					try
					{
						switch (input)
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
								client.Send(Encoding.UTF8.GetBytes(input));
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
