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

namespace Castle.MonoRail.Framework.Configuration
{
	using System;
	using System.Collections;
	using System.Configuration;
	using System.Xml;

	/// <summary>
	/// Represents the controller node configuration
	/// </summary>
	public class ControllersConfig : ISerializedConfig
	{
		private String[] assemblies;
		private Type customControllerFactory;
		
		#region ISerializedConfig implementation

		/// <summary>
		/// Deserializes the specified section.
		/// </summary>
		/// <param name="section">The section.</param>
		public void Deserialize(XmlNode section)
		{
			XmlNode customFactoryNode = section.SelectSingleNode("customControllerFactory");
			
			if (customFactoryNode != null)
			{
				XmlAttribute typeAtt = customFactoryNode.Attributes["type"];
				
				if (typeAtt == null || typeAtt.Value == String.Empty)
				{
					String message = "If the node customControllerFactory is " + 
						"present, you must specify the 'type' attribute";
					throw new ConfigurationErrorsException(message);
				}
				
				String typeName = typeAtt.Value;
				
				customControllerFactory = TypeLoadUtil.GetType(typeName);
			}
			
			XmlNodeList nodeList = section.SelectNodes("controllers/assembly");
			
			ArrayList items = new ArrayList();
			
			foreach(XmlNode node in nodeList)
			{
				items.Add(node.ChildNodes[0].Value);
			}
			
			assemblies = (String[]) items.ToArray(typeof(String));
		}
		
		#endregion

		/// <summary>
		/// Gets or sets the assemblies.
		/// </summary>
		/// <value>The assemblies.</value>
		public string[] Assemblies
		{
			get { return assemblies; }
			set { assemblies = value; }
		}

		/// <summary>
		/// Gets or sets the custom controller factory.
		/// </summary>
		/// <value>The custom controller factory.</value>
		public Type CustomControllerFactory
		{
			get { return customControllerFactory; }
			set { customControllerFactory = value; }
		}
	}
}
