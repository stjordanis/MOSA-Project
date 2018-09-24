// Copyright (c) MOSA Project. Licensed under the New BSD License.

// This code was generated by an automated template.

namespace Mosa.Compiler.Framework.IR
{
	/// <summary>
	/// StoreInt64
	/// </summary>
	/// <seealso cref="Mosa.Compiler.Framework.IR.BaseIRInstruction" />
	public sealed class StoreInt64 : BaseIRInstruction
	{
		public override int ID { get { return 150; } }

		public StoreInt64()
			: base(3, 0)
		{
		}

		public override bool IsMemoryWrite { get { return true; } }
	}
}