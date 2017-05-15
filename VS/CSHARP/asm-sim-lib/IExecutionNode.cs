using System.Collections.Generic;

namespace AsmSim
{
    public interface IExecutionNode
    {
        IList<IExecutionNode> Backward { get; }
        IExecutionNode Forward_Branch { get; set; }
        IExecutionNode Forward_Continue { get; set; }
        bool Has_Backward { get; }
        bool Has_Forward_Branch { get; }
        bool Has_Forward_Continue { get; }
        bool HasParent { get; }
        IEnumerable<State> Leafs_Backward { get; }
        IEnumerable<State> Leafs_Forward { get; }
        IExecutionNode Parent { get; }
        State State { get; }
        int Step { get; set; }

        void Add_Backward(IExecutionNode node);
        IEnumerable<State> GetFromLine(int lineNumber);
        string ToString();
        string ToString(CFlow flow);
        string ToStringOverview(CFlow flow, int depth, bool showRegisterValues = false);
    }
}