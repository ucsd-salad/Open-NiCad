// Copyright 2004-2007 Castle Project - http://www.castleproject.org/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Castle.MonoRail.Framework.Internal.Test
{
	using System.Web;

	/// <summary>
	/// Helper class to store the context to be used 
	/// for the test cases (that use the ASP.Net Runtime)
	/// </summary>
	public abstract class TestContextHolder
	{
		private static HttpContext _context;

		/// <summary>
		/// Initializes a new instance of the <see cref="TestContextHolder"/> class.
		/// </summary>
		private TestContextHolder()
		{
		}

		/// <summary>
		/// Sets the context.
		/// </summary>
		/// <param name="context">The context.</param>
		public static void SetContext(HttpContext context)
		{
			_context = context;
		}

		/// <summary>
		/// Gets the context.
		/// </summary>
		/// <value>The context.</value>
		public static HttpContext Context
		{
			get { return _context; }
		}
	}
}
