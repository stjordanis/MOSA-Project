// Copyright (c) MOSA Project. Licensed under the New BSD License.

// This code was generated by an automated template.

using Mosa.Compiler.Framework;

namespace Mosa.Platform.x86.Instructions
{
	/// <summary>
	/// Ret
	/// </summary>
	/// <seealso cref="Mosa.Platform.x86.X86Instruction" />
	public sealed partial class Ret : X86Instruction
	{

		private static readonly byte[] opcode = new byte[] { 0xC3 };

		public Ret()
			: base(0, 0)
		{
		}

		public override FlowControl FlowControl { get { return FlowControl.Return; } }

		public override void Emit(InstructionNode node, BaseCodeEmitter emitter)
		{
			emitter.Write(opcode);
		}

		public override byte[] __opcode { get { return opcode; } }
	}
}

