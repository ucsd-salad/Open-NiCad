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

namespace Castle.MonoRail.Framework
{
	using System;

	/// <summary>
	/// Decorates a public property in a <see cref="ViewComponent"/>
	/// to have the framework automatically bind the value using 
	/// the <see cref="ViewComponent.ComponentParams"/> dictionary. 
	/// By default The property name is going to be used as a key to query the params. 
	/// <para>
	/// You can also use the <see cref="ViewComponentParamAttribute.Required"/>
	/// property to define that a property is non-optional. 
	/// </para>
	/// </summary>
	[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true), Serializable]
	public class ViewComponentParamAttribute : Attribute
	{
		private string paramName;
		private bool required;

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewComponentParamAttribute"/> class.
		/// </summary>
		public ViewComponentParamAttribute()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ViewComponentParamAttribute"/> class 
		/// allowing you to override the parameter name to be queried on 
		/// the <see cref="ViewComponent.ComponentParams"/> dictionary.
		/// </summary>
		/// <param name="paramName">Overrides the name of the parameter.</param>
		public ViewComponentParamAttribute(string paramName)
		{
			this.paramName = paramName;
		}

		/// <summary>
		/// Gets or sets a value indicating whether a value for this property is required.
		/// </summary>
		/// <value><c>true</c> if required; otherwise, <c>false</c>.</value>
		public bool Required
		{
			get { return required; }
			set { required = value; }
		}

		/// <summary>
		/// Gets the name of the param.
		/// </summary>
		/// <value>The name of the param.</value>
		public string ParamName
		{
			get { return paramName; }
		}
	}
}
