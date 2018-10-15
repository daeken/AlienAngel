using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using PrettyPrinter;
using static Wrapstone.Externs;

namespace Wrapstone {
	public abstract class DisassemblerBase<AddrT, InsnT, OpcodeT, OpT> where InsnT : InstructionBase<AddrT, OpcodeT, OpT> where AddrT : struct {
		readonly UIntPtr Handle;

		bool _Detail;
		public bool Detail {
			get => _Detail;
			set {
				_Detail = value;
				cs_option(Handle, CsOptType.Detail, (UIntPtr) (value ? 3 : 0));
			}
		}
		
		internal DisassemblerBase(Architecture arch, Mode mode) {
			if(cs_open(arch, mode, out Handle) != 0)
				throw new NotSupportedException();
		}

		public InsnT DisassembleOne(AddrT addr, Span<byte> code) =>
			Disassemble(addr, code, 1).FirstOrDefault();

		public unsafe List<InsnT> Disassemble(AddrT addr, Span<byte> code, int count) {
			fixed(byte* codePtr = &MemoryMarshal.GetReference(code)) {
				var ocount = cs_disasm(Handle, codePtr, (IntPtr) code.Length, Convert.ToUInt64(addr), (UIntPtr) count, out var insns);
				var oinsns = Enumerable.Range(0, (int) ocount).Select(i => ParseInsn(insns[i])).ToList();
				cs_free(insns, ocount);
				return oinsns;
			}
		}

		unsafe InsnT ParseInsn(CsInsn insn) {
			InsnT typed;
			if(typeof(InsnT) == typeof(Arm64.Instruction))
				typed = (InsnT) (object) new Arm64.Instruction();
			else
				throw new NotImplementedException($"Unsupported instruction type {typeof(InsnT)}");
			typed.Address = (AddrT) (object) insn.Address;
			typed.Length = insn.Size;
			typed.Opcode = (OpcodeT) (object) insn.Id;
			//typed.Mnemonic = Marshal.PtrToStringAnsi((IntPtr) insn.Mnemonic, 32).Split('\0', 2)[0];
			//typed.OpStr = Marshal.PtrToStringAnsi((IntPtr) insn.OpStr, 160).Split('\0', 2)[0];

			if(Detail && insn.Detail != null)
				typed.ParseDetails(insn.Detail);
			
			return typed;
		}
	}
}