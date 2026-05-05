using System;
using System.Collections.Generic;
using System.Linq;
using TankLib.STU;
using TankLib.STU.Types;

namespace TankLib.Statescript
{
    /// <summary>
    /// Loads and manages Statescript graphs for ability interpretation.
    /// </summary>
    public class GraphLoader
    {
        private readonly Dictionary<teResourceGUID, LoadedGraph> _graphCache = new Dictionary<teResourceGUID, LoadedGraph>();
        
        /// <summary>
        /// Load a Statescript graph from a GUID reference
        /// </summary>
        public LoadedGraph LoadGraph(teResourceGUID graphGuid)
        {
            if (_graphCache.TryGetValue(graphGuid, out var cached))
                return cached;
            
            // Note: Actual loading requires CASC/game file access
            // This is a placeholder for the structure
            var graph = new LoadedGraph
            {
                Guid = graphGuid,
                Nodes = new List<GraphNode>(),
                States = new List<GraphState>(),
                Entries = new List<GraphEntry>(),
                SyncVars = new List<SyncVariable>()
            };
            
            _graphCache[graphGuid] = graph;
            return graph;
        }
        
        /// <summary>
        /// Load ability graph from STUStatescriptGraph instance
        /// </summary>
        public LoadedGraph LoadFromSTU(STUStatescriptGraph stu, teResourceGUID guid)
        {
            if (stu == null)
                return null;
            
            var graph = new LoadedGraph
            {
                Guid = guid,
                Nodes = new List<GraphNode>(),
                States = new List<GraphState>(),
                Entries = new List<GraphEntry>(),
                SyncVars = new List<SyncVariable>()
            };
            
            // Parse nodes
            if (stu.m_nodes != null)
            {
                for (int i = 0; i < stu.m_nodes.Length; i++)
                {
                    var node = ParseNode(stu.m_nodes[i], i);
                    if (node != null)
                        graph.Nodes.Add(node);
                }
            }
            
            // Parse states
            if (stu.m_states != null)
            {
                for (int i = 0; i < stu.m_states.Length; i++)
                {
                    var state = ParseState(stu.m_states[i], i);
                    if (state != null)
                        graph.States.Add(state);
                }
            }
            
            // Parse entries
            if (stu.m_entries != null)
            {
                foreach (var entry in stu.m_entries)
                {
                    graph.Entries.Add(new GraphEntry
                    {
                        // Entry parsing - structure TBD
                    });
                }
            }
            
            // Parse sync variables
            if (stu.m_syncVars != null)
            {
                foreach (var syncVar in stu.m_syncVars)
                {
                    graph.SyncVars.Add(ParseSyncVar(syncVar));
                }
            }
            
            _graphCache[guid] = graph;
            return graph;
        }
        
        private GraphNode ParseNode(STUStatescriptBase stuNode, int index)
        {
            if (stuNode == null)
                return null;
            
            var node = new GraphNode
            {
                Index = index,
                Type = GetNodeType(stuNode),
                STUInstance = stuNode
            };
            
            // Parse specific node types
            switch (stuNode)
            {
                case STUStatescriptAction action:
                    node.Type = GraphNodeType.Action;
                    break;
                    
                case STUStatescriptCondition condition:
                    node.Type = GraphNodeType.Condition;
                    break;
                    
                // Add more specific type handling as needed
            }
            
            return node;
        }
        
        private GraphState ParseState(STUStatescriptState stuState, int index)
        {
            if (stuState == null)
                return null;
            
            var state = new GraphState
            {
                Index = index,
                STUInstance = stuState
            };
            
            // Handle ability states specifically
            if (stuState is STUStatescriptStateAbility abilityState)
            {
                state.Type = GraphStateType.Ability;
                state.IsAbilityState = true;
                // Parse cooldown variable output if present
                // state.CooldownVarOutput = abilityState.m_out_CooldownVar;
            }
            else if (stuState is STUStatescriptStateModifyHealth healthState)
            {
                state.Type = GraphStateType.ModifyHealth;
                // Parse health modification data
                if (healthState.m_modifyHealth != null)
                {
                    state.HealthModification = new HealthModificationData
                    {
                        // Amount would be resolved from ConfigVar
                    };
                }
            }
            
            return state;
        }
        
        private SyncVariable ParseSyncVar(STUStatescriptSyncVar syncVar)
        {
            return new SyncVariable
            {
                // Parse sync variable properties
                // Identifier and type from STU
            };
        }
        
        private GraphNodeType GetNodeType(STUStatescriptBase node)
        {
            var typeName = node.GetType().Name;
            
            if (typeName.Contains("Action"))
                return GraphNodeType.Action;
            if (typeName.Contains("Condition"))
                return GraphNodeType.Condition;
            if (typeName.Contains("Effect"))
                return GraphNodeType.Effect;
            if (typeName.Contains("State"))
                return GraphNodeType.State;
            
            return GraphNodeType.Unknown;
        }
        
        /// <summary>
        /// Clear the graph cache
        /// </summary>
        public void ClearCache()
        {
            _graphCache.Clear();
        }
    }
    
    /// <summary>
    /// Represents a loaded and parsed Statescript graph
    /// </summary>
    public class LoadedGraph
    {
        public teResourceGUID Guid { get; set; }
        public List<GraphNode> Nodes { get; set; }
        public List<GraphState> States { get; set; }
        public List<GraphEntry> Entries { get; set; }
        public List<SyncVariable> SyncVars { get; set; }
        
        /// <summary>
        /// Find ability states in this graph
        /// </summary>
        public IEnumerable<GraphState> GetAbilityStates()
        {
            return States.Where(s => s.IsAbilityState);
        }
        
        /// <summary>
        /// Find health modification states
        /// </summary>
        public IEnumerable<GraphState> GetHealthModificationStates()
        {
            return States.Where(s => s.Type == GraphStateType.ModifyHealth);
        }
    }
    
    public class GraphNode
    {
        public int Index { get; set; }
        public GraphNodeType Type { get; set; }
        public STUStatescriptBase STUInstance { get; set; }
        public List<int> Connections { get; set; } = new List<int>();
    }
    
    public enum GraphNodeType
    {
        Unknown,
        Action,
        Condition,
        Effect,
        State,
        Entry,
        DataFlow
    }
    
    public class GraphState
    {
        public int Index { get; set; }
        public GraphStateType Type { get; set; }
        public bool IsAbilityState { get; set; }
        public STUStatescriptState STUInstance { get; set; }
        public HealthModificationData HealthModification { get; set; }
    }
    
    public enum GraphStateType
    {
        Unknown,
        Ability,
        Weapon,
        ModifyHealth,
        HealthPool,
        Generic
    }
    
    public class GraphEntry
    {
        public int Index { get; set; }
        public string Name { get; set; }
    }
    
    public class SyncVariable
    {
        public int Index { get; set; }
        public string Identifier { get; set; }
        public SyncVarType Type { get; set; }
        public object DefaultValue { get; set; }
    }
    
    public enum SyncVarType
    {
        Unknown,
        Float,
        Int,
        Bool,
        Vector,
        Entity
    }
    
    public class HealthModificationData
    {
        public float Amount { get; set; }
        public bool IsHealing => Amount > 0;
        public bool IsDamage => Amount < 0;
        public bool HasKnockback { get; set; }
        public float KnockbackForce { get; set; }
    }
}
