using System;

namespace Wrapstone {
	public enum Architecture {
		Arm = 0, 
		Arm64 = 1, 
		Mips = 2, 
		X86 = 3, 
		Ppc = 4, 
		Sparc = 5, 
		SysZ = 6, 
		XCore = 7
	}

	[Flags]
	public enum Mode {
		Arm = 0
	}
}