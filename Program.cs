using System;
using System.Threading;
using System.Runtime.InteropServices;

namespace TGE_connReplicant 
{
	class Program
	{
		public static ClientConn conn;
		public static HuffmanProcessor g_huffProcessor = new HuffmanProcessor();
		static void Main(string[] args)
		{
			handler = new ConsoleEventDelegate(ConsoleEventCallback);
			SetConsoleCtrlHandler(handler, true);
			ClientConn conn = Program.conn =  new ClientConn("GAME","PLAYERNAME","SERVERPASSWORD","127.0.0.1",10000);
			while(true){Thread.Sleep(500);}
		}
		static bool ConsoleEventCallback(int eventType) 
		{
			if(eventType == 2 && Program.conn.isConnected) 
				Program.conn.sendDisconnectPacket("");
			return false;
		}
		static ConsoleEventDelegate handler; 
		private delegate bool ConsoleEventDelegate(int eventType);
		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
	}
}
