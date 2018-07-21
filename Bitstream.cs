using System;
using System.Text;
using System.Linq;

namespace TGE_connReplicant
{
	class BitStream
	{
		public byte[] dataBuffer;
		public byte[] stringBuffer;
		public int bitNum,bufSize,maxReadBitNum,maxWriteBitNum;
		public bool compressRelative;

		public BitStream(byte[] stream)
		{
			this.bitNum = 0;
			this.compressRelative = false;
			this.stringBuffer = null;
			this.dataBuffer = stream;
			this.bufSize = stream.Length;
			this.maxReadBitNum = stream.Length<<3;
			this.maxWriteBitNum = stream.Length<<3;

			Array.Resize(ref this.dataBuffer,this.dataBuffer.Length+10);
		}
		public void stringBuf()
		{
			if(this.stringBuffer == null)
				return;
			for(int i=0;i<this.stringBuffer.Length;i++)
				Console.Write(this.stringBuffer[i] + " ");
			Console.WriteLine();
		}
		public void echoStream()
		{
			for(int i=0;i<this.bitNum;i++)
				Console.WriteLine(i + "  " + this.dataBuffer[i]);
		}
		public int getPosition()
		{
			return (this.bitNum+7)>>3;
		}
		public void setPosition(int pos)
		{
			this.bitNum = pos<<3;
		}
		public int getCurPos()
		{
			return this.bitNum;
		}
		public void setCurPos(int pos)
		{
			if(!(pos < (this.bufSize<<3)))
				throw new Exception("Out of range bitposition");
			this.bitNum = pos;
		}
		 public bool readFlag()
		{
			if(this.bitNum > this.maxReadBitNum)
				throw new Exception("Out of range read FLAG");
			int mask = 1<<(this.bitNum&0x7);
			bool ret = (this.dataBuffer[this.bitNum>>3]&mask) != 0;
			this.bitNum++;
			return ret;
		}
		public bool writeFlag(bool val)
		{
			if(this.bitNum+1 > this.maxWriteBitNum)
				throw new Exception("Out of range write");
			if(val)
				this.dataBuffer[this.bitNum>>3] |= (byte)(1<<(this.bitNum&0x7));
			else
				this.dataBuffer[this.bitNum>>3] &= (byte)(~(1<<(this.bitNum&0x7)));
			this.bitNum++;
			return val;
		}
		public void readBits(int bitCount,byte[] bitBuf)
		{
			if(bitCount < 1)
				return;
			if(bitCount+this.bitNum > this.maxReadBitNum)
				throw new Exception("Out of range read");
			int stPtr = (this.bitNum>>3);
			int byteCount = (bitCount+7)>>3;
			int ptr = 0;

			int downShift = this.bitNum&0x7;
			int upShift = 8-downShift;

			byte curB = this.dataBuffer[stPtr];
			while(byteCount-- > 0)
			{
				byte nextB = this.dataBuffer[++stPtr];
				bitBuf[ptr++] = (byte)((curB>>downShift)|(nextB<<upShift));
				curB = nextB;
			}
			this.bitNum += bitCount;
		}
		public void writeBits(int bitCount,byte[] bitBuf)
		{
			if(bitCount < 1)
				return;
			if(bitCount+this.bitNum > this.maxWriteBitNum)
				throw new Exception("Out of range write");

			int ptr = 0;
			int stPtr = (this.bitNum>>3);
			int endPtr = ((bitCount+this.bitNum-1)>>3);

			int upShift = this.bitNum&0x7;
			int downShift = 8-upShift;

			int lastMask = 0xFF>>(7-((this.bitNum+bitCount-1)&0x7));
			int startMask = 0xFF>>downShift;

			int curB = bitBuf[ptr++];
			this.dataBuffer[stPtr] = (byte)((curB<<upShift)|(this.dataBuffer[stPtr]&startMask));

			stPtr++;
			while(stPtr <= endPtr)
			{
				byte nextB = bitBuf[ptr++];
				this.dataBuffer[stPtr++] = (byte)((curB>>downShift)|(nextB<<upShift));
				curB = nextB;
			}
			this.dataBuffer[endPtr] &= (byte)lastMask;
			this.bitNum += bitCount;
		}
		public void _read(int size,byte[] bitBuf)
		{
			this.readBits(size<<3,bitBuf);
		}
		public void _write(int size,byte[] bitBuf)
		{
			Array.Resize(ref bitBuf,bitBuf.Length+8);
			this.writeBits(size<<3,bitBuf);
		}
		public static int LEToInt32(byte[] i){return BitConverter.ToInt32(i,0);}
		public static byte LEToInt8(byte[] i){return i[0];}
		public static uint LEToUInt32(byte[] i){return BitConverter.ToUInt32(i,0);}
		public static float LEToFloat(byte[] i){return BitConverter.ToSingle(i,0);}
		public static byte[] Int8ToLE(sbyte i){return BitConverter.GetBytes(i);}
		public static byte[] Int32ToLE(int i){return BitConverter.GetBytes(i);}
		public static byte[] FloatToLE(float i){return BitConverter.GetBytes(i);}
		public static byte[] UInt8ToLE(byte i){return BitConverter.GetBytes(i);}
		public static byte[] UInt32ToLE(uint i){return BitConverter.GetBytes(i);}

		public void writeUInt8(int val)
		{
			this.writeUIntS((uint)val,1);
		}
		public void writeUInt32(uint val)
		{
			this.writeUIntS(val,4);
		}
		public void writeUInt32(int val)
		{
			this.writeUIntS((uint)val,4);
		}
		public int readUInt8()
		{
			return this.readUIntS(1);
		}
		public int readUInt32()
		{
			return this.readUIntS(4);
		}
		public void writeUInt(uint val,int bitCount)
		{
			byte[] bitBuf = BitStream.UInt32ToLE(val);
			Array.Resize(ref bitBuf,8);
			this.writeBits(bitCount,bitBuf);
		}
		public void writeUIntS(uint val,int bitCount)
		{
			byte[] bitBuf = BitStream.UInt32ToLE(val);
			this._write(bitCount,bitBuf);
		}
		public int readUInt(int bitCount)
		{
			byte[] bitBuf = new byte[32];
			this.readBits(bitCount,bitBuf);
			int ret = BitStream.LEToInt32(bitBuf);
			if(bitCount != 32)
				ret &= (1<<bitCount)-1;
			return ret;
		}

		public void writeInt(int val,int bitCount)
		{
			byte[] bitBuf = BitStream.Int32ToLE(val);
			this._write(bitCount,bitBuf);
		}
		public int readUIntS(int bitCount)
		{
			byte[] bitBuf = new byte[32];
			this._read(bitCount,bitBuf);
			int ret = BitStream.LEToInt32(bitBuf);
			//if(bitCount != 32)
			//	ret &= (1<<bitCount)-1;
			return ret;
		}
		public int readInt(int bitCount)
		{
			byte[] bitBuf = new byte[32];
			this._read(bitCount,bitBuf);
			
			int ret = BitStream.LEToInt32(bitBuf);
			if(bitCount != 32)
				ret &= (1<<bitCount)-1;
			return ret;
		}
		public String readString()
		{
			char[] res;
			int pos = 0;
			if(this.stringBuffer != null)
			{
				if(this.readFlag())
				{
					int offset = (int)this.readUInt(8);
					byte[] slice = this.stringBuffer.Take(offset).ToArray();
					Array.Resize(ref slice,256);
					Program.g_huffProcessor.readHuffBuffer(this,slice);
					for(int i=0;i<slice.Length;i++)
					{
						if(slice[i] == 0)
						{
							pos = i;
							break;
						}
					}
					res = new char[pos+1];
					Array.Copy(slice,res,pos);
					return new String(res);
				}
			}

			byte[] buf = new byte[256];
			Program.g_huffProcessor.readHuffBuffer(this,buf);
			for(int i=0;i<buf.Length;i++)
			{
				if(buf[i] == 0)
				{
					pos = i;
					break;
				}
			}
			res = new char[pos+1];
			for(int i=0;i<pos;i++)
				res[i] = (char)(buf[i]);
			return new String(res);
		}
		public int readClassId(int classType, int classGroup) 
		{
			int ret = this.readUInt(5);

			if (ret > 1024) 
			{
				return -1;
			}

			return ret;
		}
		public void writeString(String str)
		{
			int maxLen = 255;
			
			char[] tmp = str.ToCharArray();
			byte[] dat = new byte[tmp.Length+1];
			for(int i=0;i<tmp.Length;i++)
				dat[i] = (byte)tmp[i];
			int len = dat.Length;

			Array.Resize(ref dat,dat.Length+1);
			/*if(stringBuffer != null)  the functionality of this is a godamn mess this code should not be nessecary anyway
			{
				for(int i=0;i<maxLen && this.stringBuffer[i] == dat[i];i++)
				{
					Array.Copy(dat,this.stringBuffer,maxLen);
					this.stringBuffer[maxLen] = '\0';
					if(this.writeFlag(i > 2))
					{
						writeInt(i,8);
						Program.g_huffProcessor.writeHuffBuffer(this,dat,maxLen-i);
						return;
					}
				}
			}*/
			Program.g_huffProcessor.writeHuffBuffer(this,dat,maxLen);
		}
	}
	class HuffNode
	{
		public int pop,index0,index1;
		public HuffNode(){this.pop=this.index0=this.index1=0;}
	}
	class HuffLeaf
	{
		public int pop,numBits,symbol,code;
		public HuffLeaf(){this.pop=this.numBits=this.symbol=this.code=0;}
	}
	class HuffWrap
	{
		public HuffNode pNode;
		public HuffLeaf pLeaf;
		public HuffWrap(){this.pNode=null;this.pLeaf=null;}
		public void set(HuffLeaf in_leaf){this.pNode = null;this.pLeaf = in_leaf;}
		public void set(HuffNode in_node){this.pLeaf = null;this.pNode = in_node;}
		public int getPop(){if(pNode != null)return pNode.pop;return pLeaf.pop;}
	}
	class HuffmanProcessor
	{
		int[] csm_charFreqs = {0,0,0,0,0,0,0,0,0,329,21,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,2809,68,0,27,0,58,3,62,4,7,0,0,15,65,554,3,394,404,189,117,30,51,27,15,34,32,80,1,142,3,142,39,0,144,125,44,122,275,70,135,61,127,8,12,113,246,122,36,185,1,149,309,335,12,11,14,54,151,0,0,2,0,0,211,0,2090,344,736,993,2872,701,605,646,1552,328,305,1240,735,1533,1713,562,3,1775,1149,1469,979,407,553,59,279,31,0,0,0,68,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0};
		
		public bool m_tablesBuilt;
		public HuffNode[] m_huffNodes;
		public HuffLeaf[] m_huffLeaves;
		public int m_huffNodeCnt = 0;
		public HuffmanProcessor()
		{
			this.m_tablesBuilt = false;
		}
		public int determineIndex(HuffWrap rWrap)
		{
			if(rWrap.pLeaf != null)
			{
				if(rWrap.pNode != null)
					throw new Exception("Got a non-NULL pNode in a HuffWrap with a non-NULL leaf.");
				return -(Array.IndexOf(this.m_huffLeaves,rWrap.pLeaf)+1);
			}
			else
			{
				if(rWrap.pNode == null)
					throw new Exception("Got a NULL pNode in a HuffWrap with a NULL leaf.");
				return Array.IndexOf(this.m_huffNodes,rWrap.pNode);
			}
		}
		public void buildTables()
		{
			if(m_tablesBuilt)
				return;
			m_tablesBuilt = true;
			this.m_huffLeaves = new HuffLeaf[256];
			this.m_huffNodes = new HuffNode[256];
			this.m_huffNodeCnt = 0;
			for(int i=0;i<256;i++)
			{
				this.m_huffLeaves[i] = new HuffLeaf();
				this.m_huffLeaves[i].pop = this.csm_charFreqs[i]+1;
				this.m_huffLeaves[i].symbol = i;
				this.m_huffLeaves[i].code = 0;
				this.m_huffLeaves[i].numBits = 0;
			}
			int currWraps = 256;
			HuffWrap[] pWrap = new HuffWrap[256];
			for(int i=0;i<256;i++)
			{
				pWrap[i] = new HuffWrap();
				pWrap[i].set(this.m_huffLeaves[i]);
			}
			while(currWraps != 1)
			{
				uint min1 = 0xfffffffe;
				uint min2 = 0xffffffff;
				int index1 = -1;
				int index2 = -1;
				for(int i=0;i<currWraps;i++)
				{
					if(pWrap[i].getPop() < min1)
					{
						min2 = min1;
						index2 = index1;
						min1 = (uint)pWrap[i].getPop();
						index1 = i;
					}
					else if(pWrap[i].getPop() < min2)
					{
						min2 = (uint)pWrap[i].getPop();
						index2 = i;
					}
				}
				if(!(index1 != -1 && index2 != -1 && index1 != index2))
					throw new Exception("hrph");
				HuffNode rNode = new HuffNode();
				this.m_huffNodes[this.m_huffNodeCnt] = rNode;
				this.m_huffNodes[this.m_huffNodeCnt].pop = pWrap[index1].getPop() + pWrap[index2].getPop();
				this.m_huffNodes[this.m_huffNodeCnt].index0 = this.determineIndex(pWrap[index1]);
				this.m_huffNodes[this.m_huffNodeCnt].index1 = this.determineIndex(pWrap[index2]);

				this.m_huffNodeCnt++;

				int mergeIndex = (index1 > index2 ? index2 : index1);
				int nukeIndex = (index1 > index2 ? index1 : index2);
				pWrap[mergeIndex].set(rNode);
				if(index2 != (currWraps-1))
					pWrap[nukeIndex] = pWrap[currWraps-1];
				currWraps--;
			}
			if(currWraps != 1)
				throw new Exception("wrong wraps?");
			if(!(pWrap[0].pNode != null && pWrap[0].pLeaf == null))
				throw new Exception("Wrong wrap type!");
			this.m_huffNodes[0] = pWrap[0].pNode;

			byte[] code = new byte[6];
			BitStream bs = new BitStream(code);
			this.generateCodes(bs,0,0);
		}
		public void generateCodes(BitStream bs,int index,int depth)
		{
			return; //skip this not nessecary for our purposes
			if(index < 0)
			{
				HuffLeaf rLeaf = this.m_huffLeaves[-(index+1)];
				rLeaf.code = BitStream.LEToInt32(bs.dataBuffer);
				rLeaf.numBits = depth;
			}
			else
			{
				HuffNode rNode = this.m_huffNodes[index];		  
				int pos = bs.getCurPos();
				bs.writeFlag(false);
				this.generateCodes(bs,rNode.index0,depth+1);

				bs.setCurPos(pos);
				bs.writeFlag(true);
				this.generateCodes(bs,rNode.index1,depth+1);

				bs.setCurPos(pos);
			}
		}
		public void readHuffBuffer(BitStream pStream,byte[] out_pBuffer)
		{
			if(!this.m_tablesBuilt)
				this.buildTables();
			bool compressed = pStream.readFlag();
			int len = pStream.readUInt(8);
			if(compressed)
			{
				for(int i=0;i<len;i++)
				{
					int index = 0;
					while(true)
					{
						if(index >= 0)
						{
							if(pStream.readFlag())
								index = this.m_huffNodes[index].index1;
							else
								index = this.m_huffNodes[index].index0;
						}
						else
						{
							out_pBuffer[i] = (byte)this.m_huffLeaves[-(index+1)].symbol;
							break;
						}
					}
				}
			}
			else
			{
				pStream._read(len,out_pBuffer);
				out_pBuffer[len] = 0;
			}
		}
		public void writeHuffBuffer(BitStream pStream,byte[] out_pBuffer,int maxLen)
		{
			if(out_pBuffer == null)
			{
				pStream.writeFlag(false);
				pStream.writeInt(0,8);
				return;
			}
			if(!this.m_tablesBuilt)
				this.buildTables();

			int len = out_pBuffer.Length-2;
			if(len > 255)
				throw new Exception("String TOO long for writeString");
			pStream.writeFlag(false);
			pStream.writeUInt((uint)len,8);
			pStream._write(len,out_pBuffer);
		}
	}
}
