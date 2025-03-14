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

namespace Castle.Facilities.NHibernateIntegration.Tests.Transactions
{
	using Castle.Services.Transaction;
	using NHibernate;

	[Transactional]
	public class OrderDao : NHibernateGenericDao
	{
		private readonly ISessionManager sessManager;

		public OrderDao(ISessionManager sessManager) : base(sessManager, "db2")
		{
			this.sessManager = sessManager;
		}

		[Transaction]
		public virtual Order Create(float val)
		{
			using(ISession session = sessManager.OpenSession("db2"))
			{
				NUnit.Framework.Assert.IsNotNull(session.Transaction);

				Order order = new Order();
				order.Value = val;
				session.Save(order);
				
				return order;
			}
		}

		[Transaction]
		public virtual void Update(Order order, float newval)
		{
			using(ISession session = sessManager.OpenSession("db2"))
			{
				NUnit.Framework.Assert.IsNotNull(session.Transaction);

				order.Value = newval;
				
				session.Update(order);
			}
		}

		[Transaction]
		public virtual void Delete(int orderId)
		{
			using(ISession session = sessManager.OpenSession("db2"))
			{
				NUnit.Framework.Assert.IsNotNull(session.Transaction);

				Order order = (Order) session.Load(typeof(Order), orderId);
				
				session.Delete(order);
			}
		}
	}
}
