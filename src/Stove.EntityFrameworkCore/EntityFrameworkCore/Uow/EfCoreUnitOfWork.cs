using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;

using Autofac.Extras.IocManager;

using Microsoft.EntityFrameworkCore;

using Stove.Domain.Uow;
using Stove.EntityFramework.Common;
using Stove.Extensions;

namespace Stove.EntityFrameworkCore.Uow
{
	/// <summary>
	///     Implements Unit of work for Entity Framework.
	/// </summary>
	public class EfCoreUnitOfWork : UnitOfWorkBase, ITransientDependency
	{
		private readonly IDbContextResolver _dbContextResolver;
		private readonly IDbContextTypeMatcher _dbContextTypeMatcher;
		private readonly IEfCoreTransactionStrategy _transactionStrategy;

		/// <summary>
		///     Creates a new <see cref="EfCoreUnitOfWork" />.
		/// </summary>
		public EfCoreUnitOfWork(
			IConnectionStringResolver connectionStringResolver,
			IUnitOfWorkFilterExecuter filterExecuter,
			IDbContextResolver dbContextResolver,
			IUnitOfWorkDefaultOptions defaultOptions,
			IDbContextTypeMatcher dbContextTypeMatcher,
			IEfCoreTransactionStrategy transactionStrategy)
			: base(
				connectionStringResolver,
				defaultOptions,
				filterExecuter)
		{
			_dbContextResolver = dbContextResolver;
			_dbContextTypeMatcher = dbContextTypeMatcher;
			_transactionStrategy = transactionStrategy;

			ActiveDbContexts = new Dictionary<string, DbContext>();
		}

		protected IDictionary<string, DbContext> ActiveDbContexts { get; }

		protected override void BeginUow()
		{
			if (Options.IsTransactional == true)
			{
				_transactionStrategy.InitOptions(Options);
			}
		}

		public override void SaveChanges()
		{
			foreach (DbContext dbContext in GetAllActiveDbContexts())
			{
				SaveChangesInDbContext(dbContext);
			}
		}

		public override async Task SaveChangesAsync()
		{
			foreach (DbContext dbContext in GetAllActiveDbContexts())
			{
				await SaveChangesInDbContextAsync(dbContext);
			}
		}

		protected override void CompleteUow()
		{
			SaveChanges();
			CommitTransaction();
		}

		protected override async Task CompleteUowAsync()
		{
			await SaveChangesAsync();
			CommitTransaction();
		}

		private void CommitTransaction()
		{
			if (Options.IsTransactional == true)
			{
				_transactionStrategy.Commit();
			}
		}

		public IReadOnlyList<DbContext> GetAllActiveDbContexts()
		{
			return ActiveDbContexts.Values.ToImmutableList();
		}

		public virtual TDbContext GetOrCreateDbContext<TDbContext>()
			where TDbContext : DbContext
		{
			Type concreteDbContextType = _dbContextTypeMatcher.GetConcreteType(typeof(TDbContext));

			var connectionStringResolveArgs = new ConnectionStringResolveArgs();
			connectionStringResolveArgs["DbContextType"] = typeof(TDbContext);
			connectionStringResolveArgs["DbContextConcreteType"] = concreteDbContextType;
			string connectionString = ResolveConnectionString(connectionStringResolveArgs);

			string dbContextKey = concreteDbContextType.FullName + "#" + connectionString;

			DbContext dbContext;
			if (!ActiveDbContexts.TryGetValue(dbContextKey, out dbContext))
			{
				if (Options.IsTransactional == true)
				{
					dbContext = _transactionStrategy.CreateDbContext<TDbContext>(connectionString, _dbContextResolver);
				}
				else
				{
					dbContext = _dbContextResolver.Resolve<TDbContext>(connectionString, null);
				}

				if (Options.Timeout.HasValue &&
				    dbContext.Database.IsRelational() &&
				    !dbContext.Database.GetCommandTimeout().HasValue)
				{
					dbContext.Database.SetCommandTimeout(Options.Timeout.Value.TotalSeconds.To<int>());
				}

				//TODO: Object materialize event
				//TODO: Apply current filters to this dbcontext

				ActiveDbContexts[dbContextKey] = dbContext;
			}

			return (TDbContext)dbContext;
		}

		protected override void DisposeUow()
		{
			if (Options.IsTransactional == true)
			{
				_transactionStrategy.Dispose();
			}
			else
			{
				foreach (DbContext context in GetAllActiveDbContexts())
				{
					Release(context);
				}
			}

			ActiveDbContexts.Clear();
		}

		protected virtual void SaveChangesInDbContext(DbContext dbContext)
		{
			dbContext.SaveChanges();
		}

		protected virtual async Task SaveChangesInDbContextAsync(DbContext dbContext)
		{
			await dbContext.SaveChangesAsync();
		}

		protected virtual void Release(DbContext dbContext)
		{
			dbContext.Dispose();
		}

		//}
		//    dbContext.Configuration.AutoDetectChangesEnabled = true;

		//    dbContext.Entry(e.Entity).State = previousState;

		//    DateTimePropertyInfoHelper.NormalizeDatePropertyKinds(e.Entity, entityType);
		//    var previousState = dbContext.Entry(e.Entity).State;

		//    dbContext.Configuration.AutoDetectChangesEnabled = false;
		//    var entityType = ObjectContext.GetObjectType(e.Entity.GetType());
		//{

		//private static void ObjectContext_ObjectMaterialized(DbContext dbContext, ObjectMaterializedEventArgs e)
	}
}
