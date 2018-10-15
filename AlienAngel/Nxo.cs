using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using K4os.Compression.LZ4;
using PrettyPrinter;

namespace AlienAngel {
	public struct Segment {
		public Memory<byte> Data;
		public uint Addr;
	}

	enum DT {
		NULL, 
		NEEDED, 
		PLTRELSZ, 
		PLTGOT, 
		HASH, 
		STRTAB, 
		SYMTAB, 
		RELA, 
		RELASZ,
		RELAENT, 
		STRSZ, 
		SYMENT, 
		INIT, 
		FINI, 
		SONAME, 
		RPATH, 
		SYMBOLIC, 
		REL,
		RELSZ, 
		RELENT, 
		PLTREL, 
		DEBUG, 
		TEXTREL, 
		JMPREL, 
		BIND_NOW, 
		INIT_ARRAY,
		FINI_ARRAY, 
		INIT_ARRAYSZ, 
		FINI_ARRAYSZ, 
		RUNPATH, 
		FLAGS, 
		GNU_HASH = 0x6ffffef5, 
		VERSYM = 0x6ffffff0, 
		RELACOUNT = 0x6ffffff9, 
		RELCOUNT = 0x6ffffffa, 
		FLAGS_1 = 0x6ffffffb, 
		VERDEF = 0x6ffffffc, 
		VERDEFNUM = 0x6ffffffd
	}

	public enum SymbolType {
		None, 
		Object, 
		Func, 
		Section
	}

	public enum RelocationType {
		Abs64 = 257, 
		GlobDat = 1025, 
		JumpSlot = 1026, 
		Relative = 1027, 
		TlsDesc = 1031
	}

	public class Symbol {
		public readonly string Name;
		public readonly uint Info, Shndx;
		public readonly ulong Value, Size;

		public readonly SymbolType Type;

		public Symbol(string name, uint info, uint other, uint shndx, ulong value, ulong size) {
			Name = name;
			Info = info;
			Shndx = shndx;
			Value = value;
			Size = size;

			Type = (SymbolType) (info & 0xF);
		}
	}
	
	public class Nxo {
		public readonly string Filename;
		public Memory<byte> FileData { get; }
		public readonly Segment Text, Ro, Data;
		public readonly uint BssSize;
		public readonly List<Symbol> Symbols = new List<Symbol>();
		public readonly List<(ulong Offset, RelocationType Type, Symbol Symbol, long Addend)> Relocations = new List<(ulong, RelocationType, Symbol, long)>();
		
		public Nxo(string fn, Segment text, Segment ro, Segment data, uint bssSize) {
			Filename = fn;
			Text = text;
			Ro = ro;
			Data = data;
			BssSize = bssSize;

			var full = new byte[Text.Data.Length + Ro.Data.Length + Data.Data.Length];
			FileData = full;
			Array.Copy(Text.Data.ToArray(), 0, full, 0, Text.Data.Length);
			Array.Copy(Ro.Data.ToArray(), 0, full, Ro.Addr, Ro.Data.Length);
			Array.Copy(Data.Data.ToArray(), 0, full, Data.Addr, Data.Data.Length);
			
			using(var ms = new MemoryStream(full))
				using(var br = new BinaryReader(ms)) {
					ms.Position = 4;
					var modOff = br.ReadUInt32();
					ms.Position = modOff;
					if(Encoding.ASCII.GetString(br.ReadBytes(4)) != "MOD0")
						throw new NotSupportedException();

					var dynamicOff = modOff + br.ReadUInt32();
					var bssOff = modOff + br.ReadUInt32();
					var bssEnd = modOff + br.ReadUInt32();
					var unwindOff = modOff + br.ReadUInt32();
					var unwindEnd = modOff + br.ReadUInt32();
					var moduleOff = modOff + br.ReadUInt32();

					ms.Position = dynamicOff;
					var dyn = new Dictionary<DT, List<ulong>>();
					for(var i = 0; ; ++i) {
						var tag = (DT) br.ReadUInt64();
						var value = br.ReadUInt64();
						if(tag == DT.NULL) break;
						if(!dyn.ContainsKey(tag)) dyn[tag] = new List<ulong>();
						dyn[tag].Add(value);
					}

					ms.Position = (long) dyn[DT.STRTAB][0];
					var dynstr = Encoding.ASCII.GetString(br.ReadBytes((int) dyn[DT.STRSZ][0]));

					ms.Position = (long) dyn[DT.SYMTAB][0];
					while(true) {
						var st_name = br.ReadUInt32();
						if(st_name > dynstr.Length)
							break;
						var symName = dynstr.Substring((int) st_name,
							dynstr.IndexOf('\0', (int) st_name) - (int) st_name);
						Symbols.Add(new Symbol(
							symName, 
							br.ReadByte(), 
							br.ReadByte(), 
							br.ReadUInt16(), 
							br.ReadUInt64(), 
							br.ReadUInt64()
						));
					}

					void ProcessRelocations(int offset, int size) {
						ms.Position = offset;
						var count = size / 0x18;
						for(var i = 0; i < count; ++i) {
							ulong roff = br.ReadUInt64(), info = br.ReadUInt64();
							var addend = br.ReadInt64();
							var type = (RelocationType) info;
							var rsym = (int) (info >> 32);
							var sym = rsym == 0 ? null : Symbols[rsym];
							
							Relocations.Add((roff, type, sym, addend));
						}
					}
					
					if(dyn.ContainsKey(DT.REL))
						ProcessRelocations((int) dyn[DT.REL][0], (int) dyn[DT.RELSZ][0]);
					if(dyn.ContainsKey(DT.RELA))
						ProcessRelocations((int) dyn[DT.RELA][0], (int) dyn[DT.RELASZ][0]);
					if(dyn.ContainsKey(DT.JMPREL))
						ProcessRelocations((int) dyn[DT.JMPREL][0], (int) dyn[DT.PLTRELSZ][0]);
				}
		}
	}

	public static class Nso {
		public static Nxo Read(string fn, Stream stream) {
			using(var br = new BinaryReader(stream, Encoding.ASCII, leaveOpen: true)) {
				if(Encoding.ASCII.GetString(br.ReadBytes(4)) != "NSO0")
					return null;

				stream.Position = 0xC;
				var flags = br.ReadUInt32();

				uint toff = br.ReadUInt32(), tloc = br.ReadUInt32(), tsize = br.ReadUInt32();
				stream.Position = 0x20;
				uint roff = br.ReadUInt32(), rloc = br.ReadUInt32(), rsize = br.ReadUInt32();
				stream.Position = 0x30;
				uint doff = br.ReadUInt32(), dloc = br.ReadUInt32(), dsize = br.ReadUInt32();
				var bssSize = br.ReadUInt32();

				stream.Position = 0x60;
				uint tfilesize = br.ReadUInt32(), rfilesize = br.ReadUInt32(), dfilesize = br.ReadUInt32();
				
				Segment ReadSegment(uint off, uint addr, uint nextAddr, uint decompSize, uint compSize) {
					Span<byte> comp = new byte[compSize];
					Debug.Assert(nextAddr >= addr + decompSize);
					Memory<byte> decomp = new byte[decompSize + nextAddr - (addr + decompSize)];
					stream.Position = off;
					stream.Read(comp);
					LZ4Codec.Decode(comp, decomp.Span);
					return new Segment { Data=decomp, Addr=addr };
				}

				return new Nxo(
					fn, 
					ReadSegment(toff, tloc, rloc, tsize, tfilesize), 
					ReadSegment(roff, rloc, dloc, rsize, rfilesize), 
					ReadSegment(doff, dloc, dloc+dsize, dsize, dfilesize), 
					bssSize
				);
			}
		}
	}
}