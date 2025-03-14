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

namespace Castle.MicroKernel.SubSystems.Conversion
{
	using System;

	using Castle.Core.Configuration;

	/// <summary>
	/// Converts a string representation to an enum value
	/// </summary>
	[Serializable]
	public class EnumConverter : AbstractTypeConverter
	{
		public override bool CanHandleType(Type type)
		{
			return type.IsEnum;
		}

		public override object PerformConversion(String value, Type targetType)
		{
			try
			{
				return Enum.Parse( targetType, value, true );
			}
			catch(ConverterException)
			{
				throw;
			}
			catch(Exception ex)
			{
				String message = String.Format(
					"Could not convert from '{0}' to {1}.", 
					value, targetType.FullName);

				throw new ConverterException(message, ex);
			}
		}
		
		public override object PerformConversion(IConfiguration configuration, Type targetType)
		{
			return PerformConversion(configuration.Value, targetType);
		}
	}
}
