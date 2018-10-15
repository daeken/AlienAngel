using System;
using System.Collections.Generic;

namespace AlienAngel {
	public class MemorySpace {
		readonly List<(ulong, Memory<byte>)> Data = new List<(ulong, Memory<byte>)>();

		public void Add(ulong address, Memory<byte> data) =>
			Data.Add((address, data));

		public Memory<byte> Get(ulong address) {
			foreach(var (@base, data) in Data)
				if(@base <= address && @base + (ulong) data.Length > address)
					return data.Slice((int) (address - @base));
			return null;
		}
	}
}