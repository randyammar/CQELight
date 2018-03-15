﻿using CQELight.DAL.Attributes;
using CQELight.DAL.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CQELight.DAL.EFCore
{
    /// <summary>
    /// Base classe for implementing Entity Framework DbContext.
    /// </summary>
    public abstract class BaseDbContext : DbContext
    {

        #region Members

        protected internal IDatabaseContextConfigurator _configuration;
        private readonly ILoggerFactory _loggerFactory;
        private bool _useSchema;

        #endregion

        #region Ctor

        /// <summary>
        /// Create a new BaseDbContext with the specified connection to the database.
        /// </summary>
        /// <param name="configurator">Database configuration.</param>
        protected BaseDbContext(IDatabaseContextConfigurator configurator)
        {
            _configuration = configurator;
        }

        /// <summary>
        /// Create a new BaseDbContext with the specified connection to the database, and a logger factory fo all 
        /// EF logs.
        /// </summary>
        /// <param name="configurator">Database configuration.</param>
        /// <param name="loggerFactory">Logger factory.</param>
        protected BaseDbContext(IDatabaseContextConfigurator configurator, ILoggerFactory loggerFactory = null)
            : this(configurator)
        {
            _loggerFactory = loggerFactory;
        }

        #endregion

        #region Overriden methods

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var entities = this.GetType().Assembly.GetTypes().AsParallel()
                 .Where(t =>
                 (t.IsSubclassOf(typeof(DbEntity)) || t.IsSubclassOf(typeof(ComposedKeyDbEntity)) || t.IsSubclassOf(typeof(CustomKeyDbEntity)))
                 && t.IsDefined(typeof(TableAttribute))
                 && !t.IsDefined(typeof(IgnoreAttribute)));

            EFCoreAutoMapper.CleanAlreadyTreatedTypes();

            foreach (var item in entities)
            {
                EFCoreAutoMapper.AutoMap(modelBuilder, item, _useSchema, _loggerFactory);
            }
            base.OnModelCreating(modelBuilder);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            base.OnConfiguring(optionsBuilder);
            if (_configuration != null)
            {
                _configuration.ConfigureConnectionString(optionsBuilder);
            }
            else
            {
                optionsBuilder.UseSqlServer(
                    "Server=(localdb)\\mssqllocaldb;Database=Dev_Base;Trusted_Connection=True;MultipleActiveResultSets=true");
            }
            _useSchema = optionsBuilder.Options.Extensions.Any(e => e.GetType().Name.Contains("SqlServer"));
            if (System.Diagnostics.Debugger.IsAttached)
            {
                optionsBuilder.EnableSensitiveDataLogging();
            }
            if (_loggerFactory != null)
            {
                optionsBuilder.UseLoggerFactory(_loggerFactory);
            }
        }

        /// <summary>
        /// Cleaning up.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
        }

        #endregion

    }

}