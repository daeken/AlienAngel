using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using MessagePack;
using PrettyPrinter;
using Wrapstone.Arm64;
using static Wrapstone.Arm64.Opcode;
using static Wrapstone.Arm64.Reg;

namespace AlienAngel {
	public class Recompiler {
		readonly Disassembler Dis = new Disassembler { Detail = true };
		readonly MemorySpace Mem = new MemorySpace();
		uint ModuleCount;

		readonly Dictionary<(ulong Start, ulong End), Instruction[]> Instructions = new Dictionary<(ulong Start, ulong End), Instruction[]>();
		readonly Dictionary<string, ulong> Symbols = new Dictionary<string, ulong>();
		readonly HashSet<ulong> CallTargets = new HashSet<ulong>();
		readonly HashSet<ulong> BasicBlocks = new HashSet<ulong>();
		readonly HashSet<ulong> RelocationTargets = new HashSet<ulong>();
		
		public void Load(Nxo nxo) {
			var loadbase = 0x7100000000U + 0x100000000U * ModuleCount++;
			Mem.Add(loadbase, nxo.FileData);
			var found = false;
			foreach(var sym in nxo.Symbols)
				if(sym.Type == SymbolType.Func) {
					Symbols[sym.Name] = loadbase + sym.Value;
					found = true;
				}
			if(!found)
				Symbols["__rtld_entry"] = loadbase;

			foreach(var rel in nxo.Relocations)
				if(rel.Type == RelocationType.Relative && rel.Addend < nxo.Text.Data.Length)
					RelocationTargets.Add(loadbase + (ulong) rel.Addend);

			var hash = string.Join("", SHA256.Create().ComputeHash(nxo.FileData.ToArray()).Select(x => $"{x:x02}"));
			var cfn = $"{hash}.cache";
			var end = loadbase + (ulong) nxo.Text.Data.Length;
			var bf = new BinaryFormatter();
			if(File.Exists(cfn)) {
				"Reading cached disassembly".Print();
				using(var fp = File.OpenRead(cfn))
					Instructions[(loadbase, end)] = MessagePackSerializer.Deserialize<Instruction[]>(fp);
				"Done".Print();
			} else {
				"No cached disassembly. Disassembling .text".Print();
				DisassembleAll(loadbase, end);
				"Writing cache".Print();
				using(var fp = File.OpenWrite(cfn))
					MessagePackSerializer.Serialize(fp, Instructions[(loadbase, end)]);

				"Done".Print();
			}
		}

		void DisassembleAll(ulong start, ulong end) {
			var insns = Instructions[(start, end)] = new Instruction[(end - start) / 4];
			var total = insns.Length;
			var mem = Mem.Get(start);

			for(var cur = 0; cur < total; ) {
				var these = Dis.Disassemble(start + (ulong) cur * 4, mem.Slice(cur * 4).Span, total - cur).ToArray();
				Array.Copy(these, 0, insns, cur, these.Length);
				cur += these.Length + 1;
			}
		}

		List<Instruction> Disassemble(ulong addr, int count) {
			foreach(var ((start, end), insns) in Instructions)
				if(start <= addr && addr < end)
					return insns.Skip((int) (addr - start) / 4).Take(count).ToList();
			return new List<Instruction>();
		}

		IEnumerable<Instruction> Disassemble(ulong addr) {
			foreach(var ((start, end), insns) in Instructions)
				if(start <= addr && addr < end)
					return insns.Skip((int) (addr - start) / 4);
			return new Instruction[0];
		}

		public void Run() {
			FindBlocks();
		}
		
		void FindBlocks() {
			var testedBlocks = new HashSet<ulong>(Symbols.Values);
			testedBlocks.UnionWith(RelocationTargets);
			var blockQueue = new Queue<ulong>(testedBlocks);

			void AddBlock(ulong addr) {
				if(testedBlocks.Contains(addr)) return;
				blockQueue.Enqueue(addr);
				testedBlocks.Add(addr);
			}
			
			var knownJumptables = new HashSet<ulong>();
			var knownNotJumptables = new HashSet<ulong>();

			void DetectJumptable(ulong addr) {
				if(knownJumptables.Contains(addr) || knownNotJumptables.Contains(addr)) return;
				const int distance = 1024;
				var hasBHi = false;
				var hasCmp = false;
				var saddr = addr - 4 * (distance - 1);
				var insns = Disassemble(saddr, distance);
				insns.Reverse();
				foreach(var insn in insns.Take(8)) {
					if(insn.Opcode == Cmp) hasCmp = true;
					else if(insn.Opcode == B && insn.Condition == ConditionCode.Hi) hasBHi = true;
				}

				if(!hasCmp || !hasBHi) {
					knownNotJumptables.Add(addr);
					return;
				}

				var regs = new ulong[31];
				var checkpoints = new Dictionary<ulong, ulong[]>();
				ulong GetValue(Operand op) {
					if(op is RegOperand _) {
						Reg reg = op;
						if(reg == Wzr) return 0;
						if(reg >= X0 && reg <= X28) return regs[reg - X0];
						if(reg >= W0 && reg <= W30) return regs[reg - W0] & 0xFFFFFFFF;
						return 0;
					}
					if(op is ImmOperand imm) return imm.Value;
					return 0;
				}
				void SetValue(Operand op, ulong value) {
					if(!(op is RegOperand)) return;
					Reg reg = op;
					if(reg >= X0 && reg <= X28)
						regs[reg - X0] = value;
					else if(reg >= W0 && reg <= W30)
						regs[reg - W0] = value & 0xFFFFFFFF;
				}
				insns.Reverse();
				foreach(var insn in insns)
					switch(insn.Opcode) {
						case Mov:
						case Adrp:
							SetValue(insn[0], GetValue(insn[1]));
							break;
						case Add:
							SetValue(insn[0], GetValue(insn[1]) + GetValue(insn[2]));
							break;
						case Ldrsw:
							checkpoints[insn.Address] = regs.Select(x => x).ToArray();
							break;
					}
				insns.Reverse();
				var numCases = -1;
				var jumptableAddr = 0UL;
				var defaultAddr = 0UL;
				var hitBHi = false;
				foreach(var insn in insns) {
					switch(insn.Opcode) {
						case B when insn.Condition == ConditionCode.Hi && !hitBHi:
							defaultAddr = insn[0];
							hitBHi = true;
							break;
						case Cmp when hitBHi && numCases == -1:
							numCases = (int) (ulong) insn[1] + 1;
							break;
						case Ldrsw when jumptableAddr == 0:
							regs = checkpoints[insn.Address];
							jumptableAddr = GetValue(new RegOperand(((MemOperand) insn[1]).Base));
							break;
					}
					if(numCases != -1 && jumptableAddr != 0 && defaultAddr != 0) break;
				}

				var jtmem = Mem.Get(jumptableAddr);
				if(jtmem.IsEmpty) {
					knownNotJumptables.Add(addr);
					return;
				}
				knownJumptables.Add(addr);
				AddBlock(defaultAddr);
				for(var i = 0; i < numCases; ++i) {
					var elem = unchecked(jumptableAddr + (ulong) BitConverter.ToInt32(jtmem.Slice(i * 4).Span));
					AddBlock(elem);
				}
			}

			while(blockQueue.Count != 0) {
				var addr = blockQueue.Dequeue();
				var hasContents = false;
				var done = false;
				foreach(var insn in Disassemble(addr)) {
					if(insn == null || done) break;
					hasContents = true;
					switch(insn.Opcode) {
						case B:
							AddBlock(insn[0]);
							if(insn.Condition != ConditionCode.Invalid)
								AddBlock(insn.Address + 4);
							done = true;
							break;
						case Cbz:
						case Cbnz:
							AddBlock(insn[1]);
							AddBlock(insn.Address + 4);
							done = true;
							break;
						case Bl:
							AddBlock(insn[0]);
							CallTargets.Add(insn[0]);
							break;
						case Br:
							DetectJumptable(insn.Address);
							done = true;
							break;
						case Ret:
							done = true;
							break;
					}
				}

				if(hasContents) {
					//$"Got block {addr:X}".Print();
					BasicBlocks.Add(addr);
				}
			}
		}
	}
}