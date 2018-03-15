﻿using CQELight.DAL.Attributes;
using CQELight.DAL.Common;
using CQELight.DAL.Interfaces;
using CQELight.Tools;
using CQELight.Tools.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CQELight.DAL.EFCore
{
    /// <summary>
    /// Entity Framework Core Repository implementation.
    /// </summary>
    /// <typeparam name="T">Type of entity to manage.</typeparam>
    public class EFRepository<T> : DisposableObject, IDatabaseRepository<T>
        where T : BaseDbEntity
    {

        #region Members

        private bool _createMode;
        private SemaphoreSlim _lock;

        #endregion

        #region Properties

        protected DbSet<T> DataSet => Context.Set<T>();
        protected BaseDbContext Context { get; }
        protected bool Disposed { get; set; }
        protected ICollection<BaseDbEntity> _added { get; set; }
        protected ICollection<BaseDbEntity> _modified { get; set; }
        protected ICollection<BaseDbEntity> _deleted { get; set; }
        protected List<string> _deleteSqlQueries = new List<string>();
        private IStateManager StateManager => Context.GetService<IStateManager>();

        #endregion

        #region Constructor

        public EFRepository(BaseDbContext context)
        {
            Context = context;
            _added = new List<BaseDbEntity>();
            _modified = new List<BaseDbEntity>();
            _deleted = new List<BaseDbEntity>();
            _lock = new SemaphoreSlim(1);
        }

        #endregion

        #region IDataReaderRepository methods

        public IEnumerable<T> Get(
            Expression<Func<T, bool>> filter = null,
            Expression<Func<T, object>> orderBy = null,
            bool tracked = true,
            bool includeDeleted = false,
            params Expression<Func<T, object>>[] includes)
            => GetCore(filter, orderBy, tracked, includeDeleted, includes).AsEnumerable();

        public IAsyncEnumerable<T> GetAsync(
            Expression<Func<T, bool>> filter = null,
            Expression<Func<T, object>> orderBy = null,
            bool tracked = true,
            bool includeDeleted = false,
            params Expression<Func<T, object>>[] includes)
            => GetCore(filter, orderBy, tracked, includeDeleted, includes).ToAsyncEnumerable();

        public Task<T> GetByIdAsync<TId>(TId value) => DataSet.FindAsync(value);

        #endregion

        #region IDataUpdateRepository methods

        public virtual async Task<int> SaveAsync()
        {
            int dbResults = 0;

            await _lock.WaitAsync();
            try
            {
                _deleteSqlQueries.DoForEach(q => Context.Database.ExecuteSqlCommand(q));
                _deleteSqlQueries.Clear();
                dbResults = await Context.SaveChangesAsync();
            }
            catch
            {
                Context
                    .ChangeTracker
                    .Entries()
                    .Where(e => e.State.In(EntityState.Added, EntityState.Deleted, EntityState.Modified))
                    .DoForEach(e => e.State = EntityState.Detached);
                throw;
            }
            finally
            {
                _lock.Release();
            }
            _added.Clear();
            _modified.Clear();
            _deleted.Clear();
            _createMode = false;
            return dbResults;
        }

        public virtual void MarkForUpdate(T entity)
            => MarkEntityForUpdate(entity);

        public virtual void MarkForInsert(T entity)
            => MarkEntityForInsert(entity);

        public virtual void MarkForDelete(T entityToDelete, bool physicalDeletion = false)
        {
            if (physicalDeletion)
            {
                StateManager.GetOrCreateEntry(entityToDelete).SetEntityState(EntityState.Deleted, true);
            }
            else
            {
                MarkEntityForSoftDeletion(entityToDelete);
            }
            _deleted.Add(entityToDelete);
        }

        public void MarkForInsertRange(IEnumerable<T> entities)
            => entities.DoForEach(MarkForInsert);

        public void MarkForUpdateRange(IEnumerable<T> entities)
            => entities.DoForEach(MarkForUpdate);

        public void MarkForDeleteRange(IEnumerable<T> entitiesToDelete, bool physicalDeletion = false)
            => entitiesToDelete.DoForEach(e => MarkForDelete(e, physicalDeletion));

        public void MarkIdForDelete<TId>(TId id, bool physicalDeletion = false)
        {
            if (id?.Equals(default(TId)) == true)
            {
                throw new ArgumentNullException(nameof(id));
            }
            var instance = Context.Find<T>(id);
            if (instance == null)
            {
                throw new InvalidOperationException($"EFRepository.MarkIdForDelete() :" +
                    $" Cannot delete of type '{typeof(T).FullName}' with '{id}' because it doesn't exists anymore into database.");
            }
            MarkForDelete(instance, physicalDeletion);
        }

        #endregion

        #region ISQLRepository

        public Task<int> ExecuteSQLCommandAsync(string sql)
             => Context.Database.ExecuteSqlCommandAsync(sql);

        public async Task<TResult> ExecuteScalarAsync<TResult>(string sql)
        {
            var connection = Context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using (var command = connection.CreateCommand())
            {
                command.CommandText = sql;
                return (TResult)await command.ExecuteScalarAsync();
            }

        }

        #endregion

        #region protected virtual methods

        protected virtual void MarkEntityForUpdate<TEntity>(TEntity entity)
            where TEntity : BaseDbEntity
        {
            _lock.Wait();
            entity.EditDate = DateTime.Now;
            _modified.Add(entity);
            _createMode = false;
            Context.ChangeTracker.TrackGraph(entity, TrackGraph);
            _lock.Release();
        }

        protected virtual void MarkEntityForInsert<TEntity>(TEntity entity)
            where TEntity : BaseDbEntity
        {
            _lock.Wait();
            entity.EditDate = DateTime.Now;
            _added.Add(entity);
            _createMode = true;
            Context.ChangeTracker.TrackGraph(entity, TrackGraph);
            _lock.Release();
        }

        protected virtual void MarkEntityForSoftDeletion<TEntity>(TEntity entityToDelete)
            where TEntity : BaseDbEntity
        {
            entityToDelete.Deleted = true;
            entityToDelete.DeletionDate = DateTime.Now;
            StateManager.GetOrCreateEntry(entityToDelete).SetEntityState(EntityState.Modified, true);
        }

        protected virtual IQueryable<T> GetCore(
            Expression<Func<T, bool>> filter = null,
            Expression<Func<T, object>> orderBy = null,
            bool tracked = true,
            bool includeDeleted = false,
            params Expression<Func<T, object>>[] includes)
        {
            IQueryable<T> query = includeDeleted ? DataSet : DataSet.Where(m => !m.Deleted);
            if (!tracked)
            {
                query = query.AsNoTracking();
            }
            if (filter != null)
            {
                query = query.Where(filter);
            }
            if (includes?.Any() == true)
            {
                foreach (var incl in includes)
                {
                    query = query.Include(incl);
                }
            }
            if (orderBy != null)
            {
                return query.OrderBy(orderBy).AsQueryable();
            }
            else
            {
                return query;
            }
        }

        protected void TrackGraph(EntityEntryGraphNode obj)
        {
            var navAttr = obj.InboundNavigation?.PropertyInfo?.GetCustomAttribute<NotNaviguableAttribute>();
            if (CannotNaviguate(navAttr))
            {
                obj.Entry.State = EntityState.Unchanged;
                return;
            }
            if (obj.Entry.Entity is BaseDbEntity baseEntity)
            {
                baseEntity.EditDate = DateTime.Now;
            }
        }

        #endregion

        #region Private methods

        private bool CannotNaviguate(NotNaviguableAttribute navAttr)
        {
            return navAttr != null &&
                            ((_createMode && navAttr.Mode.HasFlag(NavigationMode.Create)) || (!_createMode && navAttr.Mode.HasFlag(NavigationMode.Update)));
        }

        #endregion

        #region IDisposable methods

        protected override void Dispose(bool disposing)
        {
            if (!Disposed)
            {
                if (disposing)
                {
                    Context.Dispose();
                }
            }
            this.Disposed = true;
        }
        #endregion

    }
}