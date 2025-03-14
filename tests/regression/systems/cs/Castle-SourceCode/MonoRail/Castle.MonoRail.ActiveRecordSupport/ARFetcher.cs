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

namespace Castle.MonoRail.ActiveRecordSupport
{
	using System;
	using System.Reflection;

	using NHibernate;
	using NHibernate.Expression;

	using Castle.ActiveRecord;
	using Castle.ActiveRecord.Framework.Internal;
	using Castle.Components.Binder;
	using Castle.MonoRail.Framework;

	/// <summary>
	/// Class responsible on loading records for parameters marked with the <see cref="ARFetchAttribute" />.
	/// </summary>
	public class ARFetcher
	{
		private readonly IConverter converter;

		public ARFetcher(IConverter converter)
		{
			this.converter = converter;
		}

		public object FetchActiveRecord(ParameterInfo param, ARFetchAttribute attr, IRequest request)
		{
			Type type = param.ParameterType;

			bool isArray = type.IsArray;

			if (isArray) type = type.GetElementType();

			ActiveRecordModel model = ActiveRecordModel.GetModel(type);

			if (model == null)
			{
				throw new RailsException(String.Format("'{0}' is not an ActiveRecord " +
					"class. It could not be bound to an [ARFetch] attribute.", type.Name));
			}

			if (model.CompositeKey != null)
			{
				throw new RailsException("ARFetch only supports single-attribute primary keys");
			}

			String webParamName = attr.RequestParameterName != null ? attr.RequestParameterName : param.Name;

			if (!isArray)
			{
				return LoadActiveRecord(type, request.Params[webParamName], attr, model);
			}

			object[] pks = request.Params.GetValues(webParamName);

			if (pks == null)
			{
				pks = new object[0];
			}

			Array objs = Array.CreateInstance(type, pks.Length);

			for(int i = 0; i < objs.Length; i++)
			{
				objs.SetValue(LoadActiveRecord(type, pks[i], attr, model), i);
			}

			return objs;
		}

		private object LoadActiveRecord(Type type, object pk, ARFetchAttribute attr, ActiveRecordModel model)
		{
			object instance = null;

			if (pk != null && !String.Empty.Equals(pk))
			{
				PrimaryKeyModel pkModel = ObtainPrimaryKey(model);

				Type pkType = pkModel.Property.PropertyType;

				bool conversionSucceeded;
				object convertedPk = converter.Convert(pkType, pk.GetType(), pk, out conversionSucceeded);
				
				if (!conversionSucceeded)
				{
					throw new RailsException("ARFetcher could not convert PK {0} to type {1}", pk, pkType);
				}

				if (attr.Eager == null || attr.Eager.Length == 0)
				{
					// simple load
					instance = ActiveRecordMediator.FindByPrimaryKey(type, convertedPk, attr.Required);
				}
				else
				{
					// load using eager fetching of lazy collections
					DetachedCriteria criteria = DetachedCriteria.For(type);
					criteria.Add(Expression.Eq(pkModel.Property.Name, convertedPk));
					foreach (string associationToEagerFetch in attr.Eager.Split(','))
					{
						string clean = associationToEagerFetch.Trim();
						if (clean.Length == 0)
						{
							continue;
						}
						
						criteria.SetFetchMode(clean, FetchMode.Eager);
					}
					
					object[] result = (object[]) ActiveRecordMediator.FindAll(type, criteria);
					if (result.Length > 0)
						instance = result[0];
				}
			}

			if (instance == null && attr.Create)
			{
				instance = Activator.CreateInstance(type);
			}

			return instance;
		}

		private static PrimaryKeyModel ObtainPrimaryKey(ActiveRecordModel model)
		{
			if (model.IsJoinedSubClass || model.IsDiscriminatorSubClass)
			{
				return ObtainPrimaryKey(model.Parent);
			}
			return model.PrimaryKey;
		}
 
	}
}
