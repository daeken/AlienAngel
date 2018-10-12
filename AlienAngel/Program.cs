using PrettyPrinter;
using Wrapstone;
using Wrapstone.Arm64;

namespace AlienAngel {
	class Program {
		static void Main(string[] args) {
			var da = new Disassembler { Detail = true };
			foreach(var insn in da.Disassemble(0x71004A2848, new byte[] { 0x6C, 0x01, 0x80, 0xB9, 0xCC, 0x04, 0x00, 0x34, 0xCA, 0x71, 0x1A, 0x39 }))
				switch(insn.Opcode) {
					case Opcode.CBZ:
						"Cbz!".Print();
						break;
				}
		}
	}
}