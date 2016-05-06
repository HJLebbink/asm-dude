using System.Collections.Generic;

namespace AsmDude.Tools {

    public interface ILabelGraph {
        SortedSet<int> getLabelDefLineNumbers(string label);
        HashSet<int> getRelatedLineNumber(int lineNumber);

        bool isEnabled { get; }
        bool hasLabel(string label);
        bool hasLabelClash(string label);
        SortedSet<int> labelUsedAtInfo(string label);

        void reset_Async();
        void reset_Sync();
        bool tryGetLineNumber(string label, out int lineNumber);
    }
}