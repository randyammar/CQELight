﻿using CQELight.Abstractions.Events.Interfaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace CQELight.Abstractions.Events
{
    /// <summary>
    /// Base class for domain events.
    /// </summary>
    public abstract class BaseDomainEvent : IDomainEvent
    {

        #region IDomainEvent properties

        /// <summary>
        /// Unique id of the event.
        /// </summary>
        public Guid Id { get; protected set; }

        /// <summary>
        /// Time when event happens.
        /// </summary>
        public DateTime EventTime { get; protected set; }

        /// <summary>
        /// Linked aggregate Id if any.
        /// </summary>
        public Guid? AggregateId { get; protected set; }

        /// <summary>
        /// Linked saga Id if any.
        /// </summary>
        public Guid? SagaId { get; protected set; }

        #endregion

        #region Ctor

        /// <summary>
        /// Default constructor.s
        /// </summary>
        public BaseDomainEvent()
        {
            EventTime = DateTime.Now;
        }

        #endregion

    }
}