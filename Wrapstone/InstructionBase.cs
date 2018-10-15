using System;
using System.Collections.Generic;
using System.Diagnostics;
using MessagePack;

namespace Wrapstone {
	[MessagePackObject]
	public abstract class InstructionBase<AddrT, OpcodeT, OpT> where AddrT : struct {
		[Key(0)] public AddrT Address;
		[Key(1)] public uint Length;
		[Key(2)] public OpcodeT Opcode;

		[Key(3)]
		public List<OpT> Operands = new List<OpT>();

		internal abstract unsafe void ParseDetails(CsDetailBase* detail);

		public OpT this[int index] => Operands[index];

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