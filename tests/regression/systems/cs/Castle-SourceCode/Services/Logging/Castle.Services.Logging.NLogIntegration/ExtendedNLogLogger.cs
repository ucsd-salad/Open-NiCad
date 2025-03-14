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

namespace Castle.Services.Logging.NLogIntegration
{
	using Castle.Core.Logging;
	using Castle.Services.Logging.NLogtIntegration;
	using NLog;
	using ExtendedLogger = Core.Logging.IExtendedLogger;

	public class ExtendedNLogLogger : NLogLogger, ExtendedLogger
	{
		private static readonly IContextProperties globalProperties = new GlobalContextProperties();
		private static readonly IContextProperties threadProperties = new ThreadContextProperties();
		private static readonly IContextStacks threadStacks = new ThreadContextStacks();

		private ExtendedNLogFactory factory;

		public ExtendedNLogLogger(Logger logger, ExtendedNLogFactory factory)
		{
			Logger = logger;
			Factory = factory;
		}

		public ExtendedLogger CreateExtendedChildLogger(string name)
		{
			return Factory.Create(Logger.Name + "." + name);
		}

		protected internal new ExtendedNLogFactory Factory
		{
			get { return factory; }
			set { factory = value; }
		}

		#region IExtendedLogger Members

		/// <summary>
		/// Exposes the Global Context of the extended logger. 
		/// </summary>
		public IContextProperties GlobalProperties
		{
			get { return globalProperties; }
		}

		/// <summary>
		/// Exposes the Thread Context of the extended logger.
		/// </summary>
		public IContextProperties ThreadProperties
		{
			get { return threadProperties; }
		}

		/// <summary>
		/// Exposes the Thread Stack of the extended logger.
		/// </summary>
		public IContextStacks ThreadStacks
		{
			get { return threadStacks; }
		}

		#endregion
	}
}
