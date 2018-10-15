using System.Collections.Generic;

namespace AlienAngel {
	public class Function {
		public readonly string Name;
		public readonly ulong Address;
		public readonly List<ulong> BasicBlocks = new List<ulong>();

		public void AddBlock(ulong addr) => BasicBlocks.Add(addr);
	}
}