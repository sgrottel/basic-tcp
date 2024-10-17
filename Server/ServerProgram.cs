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
				Console.WriteLine($"Basic TCP Server {endPoint}");

				int nextConnectionNumber = 1;

				BasicTcpServer server = new();

				server.NewConnection += (s, c) =>
				{
					c.UserData = nextConnectionNumber++;
					Console.WriteLine($"New connection {c.UserData}");

					c.MessageReceived += (c, m) =>
					{
						string s = Encoding.UTF8.GetString(m);
						Console.WriteLine($"Received from conn {c.UserData}: {s}");

						c.Send(Encoding.UTF8.GetBytes($"Echo: '{s}'"));
					};

					c.Disconnected += (c) =>
					{
						Console.WriteLine($"Disconnected {c.UserData}");
					};
				};

				Console.WriteLine("Listening");
				server.StartListen(endPoint, "It's_a_secret_to_everybody");

				bool running = true;
				while (running)
				{
					Console.Write("> ");
					string input = (Console.ReadLine() ?? string.Empty).ToLowerInvariant();

					switch (input)
					{
						case "exit":
							Console.WriteLine("Exiting");
							running = false;
							break;

						case "closeall":
							Console.WriteLine("Close all connections");
							server.CloseAllConnections();
							break;
					}
				}

				Console.WriteLine("Stop listening");
				server.StopListen();

				Console.WriteLine("Close all connections");
				server.CloseAllConnections();

			}
			catch (Exception ex)
			{
				Console.WriteLine($"ERROR: {ex}");
			}
		}

	}
}
