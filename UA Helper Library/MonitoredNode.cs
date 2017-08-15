﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Opc.Ua;
using Opc.Ua.Server;

namespace Opc.Ua.Hsl
{
    /// <summary>
    /// Stores the current set of MonitoredItems for a Node.
    /// 存储节点当前的监视项目集
    /// </summary>
    /// <remarks>
    /// An instance of this object is created the first time a MonitoredItem is
    /// created for any attribute of a Node. The object is deleted when the last
    /// MonitoredItem is deleted.
    /// 此对象的实例是在首次为节点的任何属性创建MonitoredItem时创建的。 当最后一个MonitoredItem被删除时，该对象被删除。
    /// </remarks>
    public class MonitoredNode
    {
        #region Public Interface
        /// <summary>
        /// Initializes a new instance of the <see cref="MonitoredNode2"/> class.
        /// </summary>
        /// <param name="nodeManager">The node manager.</param>
        /// <param name="node">The node.</param>
        public MonitoredNode(QuickstartNodeManager nodeManager, NodeState node)
        {
            NodeManager = nodeManager;
            Node = node;
        }

        /// <summary>
        /// Gets or sets the NodeManager which the MonitoredNode belongs to.
        /// </summary>
        public QuickstartNodeManager NodeManager
        {
            get { return m_nodeManager; }
            set { m_nodeManager = value; }
        }

        /// <summary>
        /// Gets or sets the Node being monitored.
        /// </summary>
        public NodeState Node
        {
            get { return m_node; }
            set { m_node = value; }
        }

        /// <summary>
        /// Gets the current list of data change MonitoredItems.
        /// </summary>
        public List<MonitoredItem> DataChangeMonitoredItems
        {
            get { return m_dataChangeMonitoredItems; }
            private set { m_dataChangeMonitoredItems = value; }
        }

        /// <summary>
        /// Gets the current list of event MonitoredItems.
        /// </summary>
        public List<IEventMonitoredItem> EventMonitoredItems
        {
            get { return m_eventMonitoredItems; }
            private set { m_eventMonitoredItems = value; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance has monitored items.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance has monitored items; otherwise, <c>false</c>.
        /// </value>
        public bool HasMonitoredItems
        {
            get
            {
                if (DataChangeMonitoredItems != null && DataChangeMonitoredItems.Count > 0)
                {
                    return true;
                }

                if (EventMonitoredItems != null && EventMonitoredItems.Count > 0)
                {
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Adds the specified data change monitored item.
        /// </summary>
        /// <param name="datachangeItem">The monitored item.</param>
        public void Add(MonitoredItem datachangeItem)
        {
            if (DataChangeMonitoredItems == null)
            {
                DataChangeMonitoredItems = new List<MonitoredItem>();
                Node.OnStateChanged = OnMonitoredNodeChanged;
            }

            DataChangeMonitoredItems.Add(datachangeItem);
        }

        /// <summary>
        /// Removes the specified data change monitored item.
        /// </summary>
        /// <param name="datachangeItem">The monitored item.</param>
        public void Remove(MonitoredItem datachangeItem)
        {
            for (int ii = 0; ii < DataChangeMonitoredItems.Count; ii++)
            {
                if (Object.ReferenceEquals(DataChangeMonitoredItems[ii], datachangeItem))
                {
                    DataChangeMonitoredItems.RemoveAt(ii);
                    break;
                }
            }

            if (DataChangeMonitoredItems.Count == 0)
            {
                DataChangeMonitoredItems = null;
                Node.OnStateChanged = null;
            }
        }

        /// <summary>
        /// Adds the specified event monitored item.
        /// </summary>
        /// <param name="eventItem">The monitored item.</param>
        public void Add(IEventMonitoredItem eventItem)
        {
            if (EventMonitoredItems == null)
            {
                EventMonitoredItems = new List<IEventMonitoredItem>();
                Node.OnReportEvent = OnReportEvent;
            }

            EventMonitoredItems.Add(eventItem);
        }

        /// <summary>
        /// Removes the specified event monitored item.
        /// </summary>
        /// <param name="eventItem">The monitored item.</param>
        public void Remove(IEventMonitoredItem eventItem)
        {
            for (int ii = 0; ii < EventMonitoredItems.Count; ii++)
            {
                if (Object.ReferenceEquals(EventMonitoredItems[ii], eventItem))
                {
                    EventMonitoredItems.RemoveAt(ii);
                    break;
                }
            }

            if (EventMonitoredItems.Count == 0)
            {
                EventMonitoredItems = null;
                Node.OnReportEvent = null;
            }
        }

        /// <summary>
        /// Called when a Node produces an event.
        /// </summary>
        /// <param name="context">The system context.</param>
        /// <param name="node">The affected node.</param>
        /// <param name="e">The event.</param>
        public void OnReportEvent(ISystemContext context, NodeState node, IFilterTarget e)
        {
            lock (NodeManager.Lock)
            {
                for (int ii = 0; ii < EventMonitoredItems.Count; ii++)
                {
                    IEventMonitoredItem monitoredItem = EventMonitoredItems[ii];
                    monitoredItem.QueueEvent(e);
                }
            }
        }

        /// <summary>
        /// Called when the state of a Node changes.
        /// </summary>
        /// <param name="context">The system context.</param>
        /// <param name="node">The affected node.</param>
        /// <param name="changes">The mask indicating what changes have occurred.</param>
        public void OnMonitoredNodeChanged(ISystemContext context, NodeState node, NodeStateChangeMasks changes)
        {
            lock (NodeManager.Lock)
            {
                if (DataChangeMonitoredItems == null)
                {
                    return;
                }

                for (int ii = 0; ii < DataChangeMonitoredItems.Count; ii++)
                {
                    MonitoredItem monitoredItem = DataChangeMonitoredItems[ii];

                    if (monitoredItem.AttributeId == Attributes.Value && (changes & NodeStateChangeMasks.Value) != 0)
                    {
                        QueueValue(context, node, monitoredItem);
                        continue;
                    }

                    if (monitoredItem.AttributeId != Attributes.Value && (changes & NodeStateChangeMasks.NonValue) != 0)
                    {
                        QueueValue(context, node, monitoredItem);
                        continue;
                    }
                }
            }
        }

        /// <summary>
        /// Reads the value of an attribute and reports it to the MonitoredItem.
        /// </summary>
        public void QueueValue(
            ISystemContext context,
            NodeState node,
            MonitoredItem monitoredItem)
        {
            DataValue value = new DataValue();

            value.Value = null;
            value.ServerTimestamp = DateTime.UtcNow;
            value.SourceTimestamp = DateTime.MinValue;
            value.StatusCode = StatusCodes.Good;

            ServiceResult error = node.ReadAttribute(
                context,
                monitoredItem.AttributeId,
                monitoredItem.IndexRange,
                monitoredItem.DataEncoding,
                value);

            if (ServiceResult.IsBad(error))
            {
                value = null;
            }

            monitoredItem.QueueValue(value, error);
        }
        #endregion

        #region Private Fields
        private QuickstartNodeManager m_nodeManager;
        private NodeState m_node;
        private List<MonitoredItem> m_dataChangeMonitoredItems;
        private List<IEventMonitoredItem> m_eventMonitoredItems;
        #endregion
    }
}