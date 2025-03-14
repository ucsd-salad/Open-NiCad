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

namespace Castle.Components.DictionaryAdapter
{
	using System;

	/// <summary>
	/// Assigns a prefix to the keyed properties of an interface.
	/// </summary>
	[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = true)]
	public class DictionaryAdapterKeyPrefixAttribute : Attribute
	{
		private String keyPrefix;

		/// <summary>
		/// Initializes a default instance of the <see cref="DictionaryAdapterKeyPrefixAttribute"/> class.
		/// </summary>
		public DictionaryAdapterKeyPrefixAttribute()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DictionaryAdapterKeyPrefixAttribute"/> class.
		/// </summary>
		/// <param name="keyPrefix">The prefix for the keyed properties of the interface.</param>
		public DictionaryAdapterKeyPrefixAttribute(String keyPrefix)
		{
			this.keyPrefix = keyPrefix;
		}

		/// <summary>
		/// Gets the prefix key added to the properties of the interface.
		/// </summary>
		public String KeyPrefix
		{
			get { return keyPrefix; }
			set { keyPrefix = value; }
		}
	}
}