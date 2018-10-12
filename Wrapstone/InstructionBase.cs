using System.Collections.Generic;
using System.Diagnostics;

namespace Wrapstone {
	public abstract class InstructionBase<AddrT, OpcodeT, OpT> where AddrT : struct {
		public AddrT Address { get; internal set; }
		public uint Length { get; internal set; }
		public OpcodeT Opcode { get; internal set; }
		public string Mnemonic { get; internal set; }
		public string OpStr { get; internal set; }

		public readonly List<OpT> Operands = new List<OpT>();

		internal abstract unsafe void ParseDetails(CsDetailBase* detail);

		public void Deconstruct(out OpT _0, out OpT _1) {
			Debug.Assert(Operands.Count == 2);
			_0 = Operands[0];
			_1 = Operands[1];
		}

		public void Deconstruct(out OpT _0, out OpT _1, out OpT _2) {
			Debug.Assert(Operands.Count == 3);
			_0 = Operands[0];
			_1 = Operands[1];
			_2 = Operands[2];
		}
	}
}