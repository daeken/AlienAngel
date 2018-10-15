using System.IO;
using System.Linq;
using MoreLinq.Extensions;
using PrettyPrinter;

namespace AlienAngel {
	class Program {
		static void Main(string[] args) {
			"Starting load...".Print();
			var recompiler = new Recompiler();
			args.ForEach(fn =>
				recompiler.Load(Nso.Read(fn, File.OpenRead(fn))));
			"Loaded".Print();
			recompiler.Run();
		}
	}
}