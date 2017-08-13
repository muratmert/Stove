﻿using System.Collections.Generic;
using System.Reflection;
using System.Transactions;

using DapperExtensions.Sql;

using Stove.Bootstrapping;
using Stove.Reflection.Extensions;

namespace Stove.EntityFrameworkCore.Dapper.Tests
{
	[DependsOn(
		typeof(StoveEntityFrameworkCoreBootstrapper),
		typeof(StoveDapperBootstrapper)
	)]
	public class StoveEfCoreDapperTestBootstrapper : StoveBootstrapper
	{
		public override void PreStart()
		{
			Configuration.DefaultNameOrConnectionString = "Default";
			Configuration.UnitOfWork.IsolationLevel = IsolationLevel.Unspecified;
		}

		public override void Start()
		{
			DapperExtensions.DapperExtensions.SqlDialect = new SqliteDialect();

			DapperExtensions.DapperExtensions.SetMappingAssemblies(new List<Assembly> { typeof(StoveEfCoreDapperTestBootstrapper).GetAssembly() });
		}
	}
}
