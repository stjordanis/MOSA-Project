/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Michael Ruck (grover) <sharpos@michaelruck.de>
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

using Mosa.Runtime.Metadata.Loader;
using Mosa.Runtime.Metadata;
using Mosa.Runtime.Metadata.Signatures;
using Mosa.Runtime.Metadata.Tables;

namespace Mosa.Runtime.TypeSystem
{
	/// <summary>
	/// Represents an attribute in runtime type information.
	/// </summary>
	public class RuntimeAttribute
	{
		#region Data members

		/// <summary>
		/// Specifies the blob, which contains the attribute initialization.
		/// </summary>
		private readonly TokenTypes attributeBlob;

		/// <summary>
		/// Holds the ctor of the attribute type to invoke.
		/// </summary>
		private readonly TokenTypes ctor;

		/// <summary>
		/// Holds the ctor method of the attribute type.
		/// </summary>
		private readonly RuntimeMethod ctorMethod;

		#endregion // Data members

		#region Construction

		/// <summary>
		/// Initializes a new instance of the <see cref="RuntimeAttribute"/> class.
		/// </summary>
		/// <param name="attributeBlog">The attribute blog.</param>
		/// <param name="ctor">The ctor.</param>
		/// <param name="ctorMethod">The ctor method.</param>
		public RuntimeAttribute(TokenTypes attributeBlog, TokenTypes ctor, RuntimeMethod ctorMethod)
		{
			this.ctorMethod = ctorMethod;
			this.attributeBlob = attributeBlog;
			this.ctor = ctor;
		}

		#endregion // Construction

		#region Methods

		#endregion // Methods

		#region Properties

		/// <summary>
		/// Gets the runtime type.
		/// </summary>
		/// <value>The runtime type.</value>
		public RuntimeType Type { get { return ctorMethod.DeclaringType; } }

		/// <summary>
		/// Gets the ctor method.
		/// </summary>
		/// <value>The ctor method.</value>
		public RuntimeMethod CtorMethod { get { return ctorMethod; } }

		/// <summary>
		/// Gets the attribute BLOB.
		/// </summary>
		/// <value>The attribute BLOB.</value>
		public TokenTypes AttributeBlob { get { return attributeBlob; } }

		#endregion // Properties

	}
}