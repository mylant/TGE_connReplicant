using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Text.RegularExpressions;

namespace TGE_connReplicant 
{
	class UdpState
	{
		public IPEndPoint e;
		public UdpClient u;
	}
	class ClientConn
	{
		public enum PacketType
		{
			ConnectChallengeRequest = 28,
			ConnectChallengeReject = 30,
			ConnectChallengeResponse = 32,
			ConnectRequest = 34,
			ConnectReject = 36,
			ConnectAccept = 38,
			Disconnect = 40
		}
		public enum EventId
		{
			NetStringEvent = 7,
			RemoteCommandEvent = 9,
			SecureRemoteCommandEvent = 10
		}
		public enum NetStringType
		{
			NullString = 0,
			CString = 1,
			TagString = 2,
			Integer = 3
		}
		public enum NetPacketType
		{
			DataPacket = 0,
			PingPacket = 1,
			AckPacket = 2,
			InvalidPacketType = 3
		}
		public NetStringTable stringTable = new NetStringTable();
		public Dictionary<String,int> blidMap = new Dictionary<String,int>();
		public Dictionary<int,String> nameMap = new Dictionary<int,String>();
		public UdpClient socketO;
		public String ip = "";
		public int port = 0;
		public byte[] gPacketBuf;
		public BitStream gPacketStream;

		public byte[,] packetQueue = new byte[80,1500];
		public int packetQueuePos = 0;

		public String GameString = "";
		public int CurrentProtocolVersion = 14;
		public int MinRequiredProtocolVersion = 14;
		public int MaxRemoteCommandArgs = 20;
		public int CommandArgsBits = 5;

		public String lastJoinName = "";

		public bool s_packetRecieved = false;

		public int connectSequence;
		public byte[] addressDigest;

		public int[] lastSeqRecvdAtSend = new int[32];
		public int lastSendSeq = 0;
		public int lastSeqRecvd = 0;
		public int highestAckedSeq = 0;
		public int ackMask = 0;
		public int lastRecvAckAck = 0;
		public bool connectionEstablished = false;

		public String connectName = "";
		public String connectPass = "";
		public int connectArgsCnt = 0;
		public String[] connectArgs;
		public bool isConnected = false;

		public int ghostIdBitSize;

		IPEndPoint ep;
		
		public ClientConn(String inGameStr,String inName,String inPass,String inIp,int inPort)
		{
			this.GameString = inGameStr;
			this.connectName = inName;
			this.connectPass = inPass;
			this.ip = inIp;
			this.port = inPort;
			this.iniClient();
			this.connect();
		}
		public void iniClient()
		{
			this.gPacketBuf = new byte[1500];
			this.gPacketStream = new BitStream(gPacketBuf);
			this.addressDigest = new byte[16];
			this.connectArgs = new String[10];
			this.connectArgs[0] = this.connectName;
			this.connectArgs[1] = this.connectName;
			this.connectArgs[2] = "";
			this.connectArgs[3] = "";
			this.connectArgs[4] = "";
			this.connectArgsCnt = 5;
		}
		public int nameToBlid(String name)
		{
			int ret = -1;
			if(!this.blidMap.TryGetValue(name,out ret))
				ret = -1;
			return ret;
		}
		public String blidToName(int blid)
		{
			String ret = "";
			if(!this.nameMap.TryGetValue(blid,out ret))
				ret = "";
			return ret;
		}
		public void addBlidMap(String name,int blid)
		{
			if(this.blidToName(blid) != "")
			{
				this.nameMap.Remove(blid);
			}
			if(this.nameToBlid(name) != -1)
			{
				this.blidMap.Remove(name);
			}
			this.blidMap.Add(name,blid);
			this.nameMap.Add(blid,name);
		}
		public BitStream getPacketStream()
		{
			gPacketStream.setPosition(0);
			return gPacketStream;
		}
		public void sendPacket()
		{
			this.socketO.Send(this.gPacketStream.dataBuffer,this.gPacketStream.getPosition());
		}
		public void sendPacket(BitStream bs)
		{
			this.socketO.Send(bs.dataBuffer,bs.getPosition());
		}
		public void buildSendPacketHeader(BitStream stream,int packetType)
		{
			int ackByteCount = ((this.lastSeqRecvd-this.lastRecvAckAck + 7)>>3);
			if(ackByteCount > 4)
				throw new Exception("Too few ack bytes!");

			if(packetType == (int)NetPacketType.DataPacket)
				this.lastSendSeq++;

			stream.writeFlag(true);
			stream.writeUInt((uint)this.connectSequence & 1, 1);
			stream.writeUInt((uint)this.lastSendSeq, 9);
			stream.writeUInt((uint)this.lastSeqRecvd, 9);
			stream.writeUInt((uint)packetType, 2);
			stream.writeUInt((uint)ackByteCount, 3);
			stream.writeUInt((uint)this.ackMask, ackByteCount * 8);

			if(packetType == (int)NetPacketType.DataPacket)
				this.lastSeqRecvdAtSend[this.lastSendSeq&0x1F] = this.lastSeqRecvd;
		}
		public void sendAckPacket()
		{
			byte[] buf = new byte[16];
			BitStream bs = new BitStream(buf);
			this.buildSendPacketHeader(bs,(int)NetPacketType.AckPacket);
			this.sendPacket(bs);
		}
		public void processRawPacket(BitStream pstream)
		{
			pstream.readFlag();
			int pkConnectSeqBit = pstream.readUInt(1);
			int pkSequenceNumber = pstream.readUInt(9);
			int pkHighestAck = pstream.readUInt(9);
			int pkPacketType = pstream.readUInt(2);
			int pkAckByteCount = pstream.readUInt(3);
			
			if(pkConnectSeqBit != (this.connectSequence&1))
				return;

			if(pkAckByteCount > 4 || pkPacketType >= (int)NetPacketType.InvalidPacketType)
				return;
			
			int pkAckMask = pstream.readUInt(8*pkAckByteCount);

			pkSequenceNumber |= (byte)((byte)(this.lastSeqRecvd) & 0xFFFFFE00);

			if(pkSequenceNumber < this.lastSeqRecvd)
				pkSequenceNumber += 0x200;

			if(pkSequenceNumber > this.lastSeqRecvd + 31)
				return;
			pkHighestAck |= (byte)((byte)(this.highestAckedSeq) & 0xFFFFFE00);
			
			if(pkHighestAck < this.highestAckedSeq)
				pkHighestAck += 0x200;
			
			if(pkHighestAck > this.lastSendSeq)
				 return;

			this.ackMask <<= pkSequenceNumber - this.lastSeqRecvd;
			
			if(pkPacketType == (int)NetPacketType.DataPacket)
				this.ackMask |= 1;

			for(int i=this.highestAckedSeq+1; i<=pkHighestAck;i++) 
			{
				bool packetTransmitSuccess = ((pkAckMask & (1 << (pkHighestAck - i))) == 0 ? false : true);
				if(packetTransmitSuccess) 
				{
					this.lastRecvAckAck = this.lastSeqRecvdAtSend[i & 0x1F];

					if(!this.connectionEstablished) 
					{
						this.connectionEstablished = true;
						Console.WriteLine("Connection Established");
						//this.handleConnectionEstablished();  not really nessecary for our purposes 
					}
				}
			}
			if(pkSequenceNumber - this.lastRecvAckAck > 32)
				this.lastRecvAckAck = pkSequenceNumber - 32;

			this.highestAckedSeq = pkHighestAck;
			if(pkPacketType == (int)NetPacketType.PingPacket)
			{
				this.sendAckPacket();
			}
			//this.keepAlive();  not really nessecary for our purposes
			if (this.lastSeqRecvd != pkSequenceNumber && pkPacketType == (int)NetPacketType.DataPacket)
				this.handlePacket(pstream);

			this.lastSeqRecvd = pkSequenceNumber;
		}
		public void handlePacket(BitStream bstream)
		{
			if(bstream.readFlag())
			{
				bstream.readUInt(10);
				bstream.readUInt(10);
			}
			if(bstream.readFlag())
			{
				bstream.readUInt(10);
				bstream.readUInt(10);
			}
			this.readPacket(bstream);
		}
		public void readPacket(BitStream bstream)
		{
			bstream.stringBuffer = new byte[256];
			bstream.readUInt(32); //lastMoveAck
			if(bstream.readFlag())
			{
				if(bstream.readFlag())
					bstream.readUInt(24);
				if(bstream.readFlag())
					bstream.readUInt(24);
			}
			if(bstream.readFlag())
			{
				if(bstream.readFlag())
				{
					bstream.readUInt(this.ghostIdBitSize);
				}
				else
				{
					bstream.readUInt(24);
					bstream.readUInt(24);
					bstream.readUInt(24);
				}
			}
			if(bstream.readFlag())
				bstream.readUInt(this.ghostIdBitSize);
			if(bstream.readFlag())
				bstream.readUInt(8);
			this.eventReadPacket(bstream);
			if(bstream.readFlag())
				bstream.readUInt(this.ghostIdBitSize);

			bstream.stringBuffer = null;
		}
		public void eventReadPacket(BitStream bstream)
		{
			int prevSeq = -1;
			bool unguaranteedPhase = true;

			while(true)
			{
				bool bit = bstream.readFlag();
				if(unguaranteedPhase && !bit)
				{
					unguaranteedPhase = false;
					bit = bstream.readFlag();
				}
				if(!unguaranteedPhase && !bit)
					break;

				int seq = -1;
				if(!unguaranteedPhase)
				{
					if(bstream.readFlag())
						seq = (prevSeq + 1)&0x7f;
					else
						seq = bstream.readUInt(7);
					prevSeq = seq;
				}
				int classId = bstream.readClassId(2,0);
				int argc;
				String[] argv;
				String cmd = "";
				String tmp;
				switch(classId)
				{
					case (int)EventId.NetStringEvent:
						int index = bstream.readUInt(this.stringTable.EntryBitSize);
						String str = bstream.readString();
						this.stringTable.mapString(index,str);
						break;
					case (int)EventId.RemoteCommandEvent:
						argc = bstream.readUInt(this.CommandArgsBits);
						argv = new String[argc];
						for(int i=0;i<argc;i++)
						{
							tmp = this.unpackString(bstream,(i != 0 ? true : false));
							if(tmp == null)
								continue;
							if(i == 0)
								cmd = tmp.Substring(0,tmp.Length-1);
							else
								argv[i-1] = tmp;
						}
						handleCommand(cmd,argv);
						break;
					case (int)EventId.SecureRemoteCommandEvent:
						argc = bstream.readUInt(this.CommandArgsBits);
						argv = new String[argc];
						for(int i=0;i<argc;i++)
						{
							tmp = this.unpackString(bstream,(i != 0 ? true : false));
							if(tmp == null)
								continue;
							if(i == 0)
								cmd = tmp.Substring(0,tmp.Length-1);
							else
								argv[i-1] = tmp;
						}
						cmd = "SECURE_" + cmd;
						handleCommand(cmd,argv);
						break;
				}
			}
		}
		public String unpackString(BitStream stream)
		{
			return this.unpackString(stream,false);
		}
		public String unpackString(BitStream stream,bool slice)
		{
			int code = stream.readUInt(2);
			String ret = "";
			switch(code)
			{
				case (int)NetStringType.NullString:
					ret = "";
					slice = false;
					break;
				case (int)NetStringType.CString:
					ret = stream.readString();
					break;
				case (int)NetStringType.TagString:
					int tag = stream.readUInt(this.stringTable.EntryBitSize);
					ret = this.stringTable.getString(tag);
					break;
				case (int)NetStringType.Integer:
					bool neg = stream.readFlag();
					int num;
					if(stream.readFlag())
						num = stream.readUInt(7);
					else if(stream.readFlag())
						num = stream.readUInt(15);
					else
						num = stream.readUInt(31);
					if(neg)
						num = -num;
					ret = "" + num;
					slice = false;
					break;
			}
			if(slice && ret != null && ret.Length > 1)
				ret = ret.Substring(0,ret.Length-1);
			return ret;
		}
		public void onPacket(byte[] sockStream)
		{
			BitStream bst = new BitStream(sockStream);
			if((sockStream[0]&1) != 0)
			{
				this.processRawPacket(bst);
			}
			else
			{
				int packetType = (int)bst.readUInt8();
				String reason = "";
				switch(packetType)
				{
					case (int)PacketType.ConnectChallengeResponse:
						Console.WriteLine("Connect Challenge Response");
							connectSequence = bst.readUInt32();
							BitStream stream = getPacketStream();
							stream.writeUInt8((int)PacketType.ConnectRequest);
							stream.writeUInt32((uint)this.connectSequence);
							for(int i=5;i<=20;i++)
								stream._write(1,new byte[] {sockStream[i]});
							stream.writeString("GameConnection");
						
							stream.writeUInt32(0);
							stream.writeUInt32(0xFFFFFFFF);
							stream.writeString(GameString);
							stream.writeUInt32(this.CurrentProtocolVersion);
							stream.writeUInt32(this.MinRequiredProtocolVersion);

							stream.writeString(this.connectPass);
							stream.writeUInt32((int)this.connectArgsCnt);
							for(int i=0;i<this.connectArgsCnt;i++)
								stream.writeString(this.connectArgs[i]);
							this.sendPacket();
						break;
						
					case (int)PacketType.ConnectAccept:
						Console.WriteLine("Connection Accepted");
						bst.readUInt32();
						bst.readUInt32();
						this.ghostIdBitSize = bst.readUInt32();
						break;
					case (int)PacketType.ConnectReject:
						bst.readUInt32();
						reason = bst.readString();
						Console.WriteLine("Connection Rejected: " + reason);
						this.isConnected = false;
						break;
					case (int)PacketType.Disconnect:
						bst.readUInt32();
						reason = bst.readString();
						Console.WriteLine("Disconnect " + reason);
						this.isConnected = false;
						break;
					default:
						Console.WriteLine("Unhandled packet " + packetType);
						break;

				}
			}
		}
		public void handleCommand(String cmd,String[] args)
		{
			String ret = "";
			String tmp = "";
			String name;
			int blid;
			for(int i=0;i<args.Length;i++)
			{
				tmp = args[i];
				if(tmp == null)
					continue;
				tmp = tmp.Trim();
				ret += (i != 0 ? "," : "") + "'" + tmp + "'";
			}
			switch(cmd)
			{
				case "chatMessage":
				case "ChatMessage":
					name = args[5];
					blid = this.nameToBlid(name);
					tmp = "";
					if(blid != -1)
						tmp = "[" + blid + "]";
					Console.WriteLine(tmp + name + ": " + args[7]);
					break;
				case "SECURE_ClientDrop":
					name = args[0];
					blid = this.nameToBlid(name);
					if(blid != -1)
						tmp = "[" + blid + "]";
					Console.WriteLine(" " + tmp + name + " disconnected");
					break;
				case "SECURE_ClientJoin":
					tmp = args[2];
					blid = -1;
					Int32.TryParse(tmp,out blid);
					name = args[0];
					if(name[0] == (char)0)
					{
						if(this.blidToName(blid) != "")
						{
							name = this.blidToName(blid);
						}
						else if(lastJoinName != "")
						{
							name = lastJoinName;
							lastJoinName = "";
							this.addBlidMap(name,blid);
						}
					}
					else
					{
						this.addBlidMap(name,blid);
					}
					if(name == null || name.Length == 0)
						break;
					Console.WriteLine(" [" + blid + "]" + name + " joined");
					break;
				case "ServerMessage":
					switch(args[0])
					{
						case "MsgClientJoin":
							lastJoinName = args[2];
							break;
						case "":
							tmp = args[1];
							if(args.Length > 1)
							{
								for(int i=2;i<args.Length;i++)
								{
									tmp = tmp.Replace("%" + (i-1),args[i]);
								}
							}
							tmp = Regex.Replace(tmp,@"<[^>]+>","").Trim();
							Console.WriteLine(" " + tmp);
							break;
					}
					break;
			}
		}
		public void onPacketRecieved(IAsyncResult res)
		{
			UdpClient u = (UdpClient)((UdpState)(res.AsyncState)).u;
			IPEndPoint e = (IPEndPoint)((UdpState)(res.AsyncState)).e;
			byte[] receiveBytes = u.EndReceive(res,ref e);
			s_packetRecieved = true;
			this.onPacket(receiveBytes);
			this.getPackets();
		}
		public void getPackets()
		{
			UdpState state = new UdpState();
			state.u = this.socketO;
			state.e = this.ep;
			s_packetRecieved = false;
			this.socketO.BeginReceive(new AsyncCallback(onPacketRecieved),state);
		}
		public void connect()
		{
			ep = new IPEndPoint(IPAddress.Parse(this.ip),this.port);
			this.socketO = new UdpClient();
			this.socketO.Connect(ep);
			BitStream stream = this.getPacketStream();
			Console.WriteLine("Sending Challenge Request");
			stream.writeUInt8((int)PacketType.ConnectChallengeRequest);
			this.sendPacket();
			this.isConnected = true;
			Thread th = new Thread(getPackets);
			th.Start();
		}
		public void writeConnectionRequest()
		{
			BitStream stream = this.gPacketStream;
			stream.writeUInt32(0);
			stream.writeUInt32(0xFFFFFFFF);
			stream.writeString(GameString);
			stream.writeUInt32(this.CurrentProtocolVersion);
			stream.writeUInt32(this.MinRequiredProtocolVersion);

			stream.writeString(this.connectPass);
			stream.writeUInt32((int)this.connectArgsCnt);
			for(int i=0;i<this.connectArgsCnt;i++)
				stream.writeString(this.connectArgs[i]);
		}
		public void sendDisconnectPacket(String reason)
		{
			BitStream stream = this.getPacketStream();
			stream.writeUInt8((int)PacketType.Disconnect);
			stream.writeUInt32(this.connectSequence);
			stream.writeString(reason);
			this.sendPacket();
		}
	}
}
