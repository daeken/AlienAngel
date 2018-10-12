using System;
using System.Runtime.InteropServices;

namespace Wrapstone {
	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct CsInsn {
		internal readonly uint Id;
		internal readonly ulong Address;
		internal readonly ushort Size;

		internal fixed byte Bytes[16];
		internal fixed byte Mnemonic[32];
		internal fixed byte OpStr[160];

		internal readonly CsDetailBase *Detail;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal unsafe struct CsDetailBase {
		internal fixed ushort RegsRead[12];
		internal readonly byte RegsReadCount;

		internal fixed ushort RegsWritten[20];
		internal readonly byte RegsWrittenCount;

		internal fixed byte Groups[8];
		internal readonly byte GroupCount;

		internal ulong ArchSpecificPlaceholder;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct CsArm64Detail {
		internal readonly uint CC;
		internal readonly bool UpdateFlags, WriteBack;
		internal readonly byte OpCount;
		internal uint OpsPlaceholder;
	}

	[StructLayout(LayoutKind.Sequential)]
	internal struct CsArm64Op {
		internal readonly int VectorIndex;
		internal readonly uint Vas, Vess;
		internal readonly uint ShiftType, ShiftValue;
		internal readonly uint Ext;
		internal readonly uint OpType, Padding1;
		internal uint Placeholder0, Placeholder1, Placeholder2, Padding2;
		internal readonly byte Access;
		internal readonly uint Padding3;
	}

	internal enum CsOptType {
		Detail = 2
	}
	
	internal static unsafe class Externs {
		[DllImport("libcapstone.4.dylib", CallingConvention = CallingConvention.Cdecl)]
		internal static extern int cs_open(Architecture arch, Mode mode, out UIntPtr handle);
		
		[DllImport("libcapstone.4.dylib", CallingConvention = CallingConvention.Cdecl)]
		internal static extern UIntPtr cs_disasm(UIntPtr handle, byte* code, IntPtr codeSize, ulong address, UIntPtr count, out CsInsn* insns);

		[DllImport("libcapstone.4.dylib", CallingConvention = CallingConvention.Cdecl)]
		internal static extern void cs_free(CsInsn* ptr, UIntPtr count);
		
		[DllImport("libcapstone.4.dylib", CallingConvention = CallingConvention.Cdecl)]
		internal static extern void cs_option(UIntPtr handle, CsOptType type, UIntPtr value);
	}
}