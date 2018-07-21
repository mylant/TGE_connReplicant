using System;
using System.Collections.Generic;

namespace TGE_connReplicant 
{
	class NetStringTable 
	{
		public Dictionary<int,String> fMap;
		public Dictionary<String,int> bMap;
		public int EntryCount = 32;
		public int EntryBitSize = 5;
		public int InvalidEntryId = 32;
		public NetStringTable()
		{
			fMap = new Dictionary<int,String>();
			bMap = new Dictionary<String,int>();
		}
		public void mapString(int i,String s)
		{
			if(this.fMap.ContainsKey(i))
				return;
			if(this.bMap.ContainsKey(s))
				return;
			this.fMap.Add(i,s);
			this.bMap.Add(s.ToLower(),i);
		}
		public String getString(int i)
		{
			String ret = "";
			this.fMap.TryGetValue(i,out ret);
			return ret;
		}
		public int getIndex(String i)
		{
			int ret = 0;
			this.bMap.TryGetValue(i.ToLower(),out ret);
			return ret;
		}
	}
}
