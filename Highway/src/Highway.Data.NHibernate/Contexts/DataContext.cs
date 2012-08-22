﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using Common.Logging;
using Common.Logging.Simple;
using Highway.Data.Interceptors.Events;
using Highway.Data.Interfaces;
using NHibernate;
using NHibernate.Linq;

namespace Highway.Data
{
    /// <summary>
    /// A base implementation of the Code First Data DataContext for Entity Framework
    /// </summary>
    public class DataContext : ISession, IObservableDataContext
    {
        private readonly IMappingConfiguration _mapping;
        private readonly ILog _log;

        /// <summary>
        /// Constructs a context
        /// </summary>
        /// <param name="connectionString">The standard SQL connection string for the Database</param>
        /// <param name="mapping">The Mapping Configuration that will determine how the tables and objects interact</param>
        public DataContext(ISession session)
        {
            _session = session;
        }
                
        /// <summary>
        /// This gives a mockable wrapper around the normal <see cref="DbSet{T}"/> method that allows for testablity
        /// </summary>
        /// <typeparam name="T">The Entity being queried</typeparam>
        /// <returns><see cref="IQueryable{T}"/></returns>
        public IQueryable<T> AsQueryable<T>() where T : class
        {
            _log.DebugFormat("Querying Object {0}", typeof(T).Name);
            var result = _session.Query<T>();
            _log.DebugFormat("Queried Object {0}", typeof(T).Name);
            return result;
        }

        /// <summary>
        /// Adds the provided instance of <typeparamref name="T"/> to the data context
        /// </summary>
        /// <typeparam name="T">The Entity Type being added</typeparam>
        /// <param name="item">The <typeparamref name="T"/> you want to add</param>
        /// <returns>The <typeparamref name="T"/> you added</returns>
        public T Add<T>(T item) where T : class
        {
            _log.DebugFormat("Adding Object {0}",item);
            _session.Save(item);
            _log.TraceFormat("Added Object {0}", item);
            return item;
        }

        /// <summary>
        /// Removes the provided instance of <typeparamref name="T"/> from the data context
        /// </summary>
        /// <typeparam name="T">The Entity Type being removed</typeparam>
        /// <param name="item">The <typeparamref name="T"/> you want to remove</param>
        /// <returns>The <typeparamref name="T"/> you removed</returns>
        public T Remove<T>(T item) where T : class
        {
            _log.DebugFormat("Removing Object {0}", item);
            _session.Delete(item);
            _log.TraceFormat("Removed Object {0}", item);
            return item;
        }

        /// <summary>
        /// Updates the provided instance of <typeparamref name="T"/> in the data context
        /// </summary>
        /// <typeparam name="T">The Entity Type being updated</typeparam>
        /// <param name="item">The <typeparamref name="T"/> you want to update</param>
        /// <returns>The <typeparamref name="T"/> you updated</returns>
        public T Update<T>(T item) where T : class
        {
            _log.DebugFormat("Updating Object {0}", item);
            _session.Update(item);
            _log.TraceFormat("Updated Object {0}", item);
            return item;
        }

        /// <summary>
        /// Attaches the provided instance of <typeparamref name="T"/> to the data context
        /// </summary>
        /// <typeparam name="T">The Entity Type being attached</typeparam>
        /// <param name="item">The <typeparamref name="T"/> you want to attach</param>
        /// <returns>The <typeparamref name="T"/> you attached</returns>
        public T Attach<T>(T item) where T : class
        {
            _log.DebugFormat("Attaching Object {0}", item);
            _session.Persist(item);
            _log.TraceFormat("Attached Object {0}", item);
            return item;
        }

        /// <summary>
        /// Detaches the provided instance of <typeparamref name="T"/> from the data context
        /// </summary>
        /// <typeparam name="T">The Entity Type being detached</typeparam>
        /// <param name="item">The <typeparamref name="T"/> you want to detach</param>
        /// <returns>The <typeparamref name="T"/> you detached</returns>
        public T Detach<T>(T item) where T : class
        {
            _log.DebugFormat("Detaching Object {0}", item);
            _session.Evict(item);            
            _log.TraceFormat("Detached Object {0}", item);
            return item;
        }


        /// <summary>
        /// Reloads the provided instance of <typeparamref name="T"/> from the database
        /// </summary>
        /// <typeparam name="T">The Entity Type being reloaded</typeparam>
        /// <param name="item">The <typeparamref name="T"/> you want to reload</param>
        /// <returns>The <typeparamref name="T"/> you reloaded</returns>
        public T Reload<T>(T item) where T : class
        {
            _log.DebugFormat("Reloading Object {0}", item);
            _session.Refresh(item);
            _log.TraceFormat("Reloaded Object {0}", item);
            return item;
        }

        /// <summary>
        /// Commits all currently tracked entity changes
        /// </summary>
        /// <returns>the number of rows affected</returns>
        public int Commit()
        {
            _log.Trace("\tCommit");
            InvokePreSave();
            _session.Transaction.Commit();
            InvokePostSave();
            _log.DebugFormat("\tCommited Changes");
            return 0;
        }

        private void InvokePostSave()
        {
            if (PostSave != null) PostSave(this, new PostSaveEventArgs());
        }

        private void InvokePreSave()
        {
            if (PreSave != null) PreSave(this, new PreSaveEventArgs() { });
        }

        /// <summary>
        /// Executes a SQL command and tries to map the returned datasets into an <see cref="IEnumerable{T}"/>
        /// The results should have the same column names as the Entity Type has properties
        /// </summary>
        /// <typeparam name="T">The Entity Type that the return should be mapped to</typeparam>
        /// <param name="sql">The Sql Statement</param>
        /// <param name="dbParams">A List of Database Parameters for the Query</param>
        /// <returns>An <see cref="IEnumerable{T}"/> from the query return</returns>
        public IEnumerable<T> ExecuteSqlQuery<T>(string sql, params DbParameter[] dbParams)
        {
            var parameters = dbParams.Select(x => string.Format("{0} : {1} : {2}\t", x.ParameterName, x.Value, x.DbType)).ToArray();
            _log.TraceFormat("Executing SQL {0}, with parameters {1}", sql, string.Join(",", parameters));
            return base.Database.SqlQuery<T>(sql, dbParams);
        }

        /// <summary>
        /// Executes a SQL command and returns the standard int return from the query
        /// </summary>
        /// <param name="sql">The Sql Statement</param>
        /// <param name="dbParams">A List of Database Parameters for the Query</param>
        /// <returns>The rows affected</returns>
        public int ExecuteSqlCommand(string sql, params DbParameter[] dbParams)
        {
            var parameters = dbParams.Select(x => string.Format("{0} : {1} : {2}\t", x.ParameterName, x.Value, x.DbType)).ToArray();
            _log.TraceFormat("Executing SQL {0}, with parameters {1}", sql, string.Join(",", parameters));
            return base.Database.ExecuteSqlCommand(sql, dbParams);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="procedureName"></param>
        /// <param name="dbParams"></param>
        /// <returns></returns>
        public int ExecuteFunction(string procedureName, params ObjectParameter[] dbParams)
        {
            var parameters = dbParams.Select(x => string.Format("{0} : {1} : {2}\t", x.Name, x.Value, x.ParameterType)).ToArray();
            _log.TraceFormat("Executing Procedure {0}, with parameters {1}", procedureName, string.Join(",", parameters));
            return base.Database.SqlQuery<int>(procedureName, dbParams).FirstOrDefault();
        }

        private IEventManager _eventManager;
        private readonly ISession _session;
        /// <summary>
        /// The reference to EventManager that allows for ordered event handling and registration
        /// </summary>
        public IEventManager EventManager
        {
            get { return _eventManager; }
            set
            {
                _eventManager = value;
                _eventManager.Context = this;
            }
        }

        /// <summary>
        /// The event fired just before the commit of the ORM
        /// </summary>
        public event EventHandler<PreSaveEventArgs> PreSave;

        /// <summary>
        /// The event fired just after the commit of the ORM
        /// </summary>
        public event EventHandler<PostSaveEventArgs> PostSave;

        /// <summary>
        /// This method is called when the model for a derived context has been initialized, but
        ///                 before the model has been locked down and used to initialize the context.  The default
        ///                 implementation of this method takes the <see cref="IMappingConfiguration"/> array passed in on construction and applies them. 
        /// If no configuration mappings were passed it it does nothing.
        /// </summary>
        /// <remarks>
        /// Typically, this method is called only once when the first instance of a derived context
        ///                 is created.  The model for that context is then cached and is for all further instances of
        ///                 the context in the app domain.  This caching can be disabled by setting the ModelCaching
        ///                 property on the given ModelBuidler, but note that this can seriously degrade performance.
        ///                 More control over caching is provided through use of the DbModelBuilder and DbContextFactory
        ///                 classes directly.
        /// </remarks>
        /// <param name="modelBuilder">The builder that defines the model for the context being created.</param>
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            _log.Debug("\tOnModelCreating");
            if(_mapping != null)
            {
                _log.TraceFormat("\t\tMapping : {0}", _mapping.GetType().Name);
                _mapping.ConfigureModelBuilder(modelBuilder);
            }
            base.OnModelCreating(modelBuilder);
        }
    }
}