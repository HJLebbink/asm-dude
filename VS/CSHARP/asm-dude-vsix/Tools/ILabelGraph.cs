using System.Collections.Generic;

namespace AsmDude.Tools {

    public interface ILabelGraph {
        SortedSet<int> getLabelDefLineNumbers(string label);
        HashSet<int> getAllRelatedLineNumber();

        /// <summary>
        /// Return whether this label graph is enabled
        /// </summary>
        bool isEnabled { get; }

        bool hasLabel(string label);
        bool hasLabelClash(string label);
        SortedSet<int> labelUsedAtInfo(string label);

        /// <summary>
        /// Return dictionary of line numbers with label clash descriptions
        /// </summary>
        SortedDictionary<int, string> labelClashes { get; }

        /// <summary>
        /// Return dictionary of line numbers with undefined label descriptions
        /// </summary>
        SortedDictionary<int, string> undefinedLabels { get; }

        void reset_Async();
        void reset_Sync();
        bool tryGetLineNumber(string label, out int lineNumber);
    }
}