using Microsoft.VisualStudio.Shell;
using System.Collections.Generic;

namespace AsmDude.Tools {

    public interface ILabelGraph {
        SortedSet<uint> getLabelDefLineNumbers(string label);
        IList<int> getAllRelatedLineNumber();

        /// <summary>
        /// Return whether this label graph is enabled
        /// </summary>
        bool isEnabled { get; }

        int getLinenumber(uint id);
        string getFilename(uint id);
        bool isFromMainFile(uint id);


        bool hasLabel(string label);
        bool hasLabelClash(string label);
        SortedSet<uint> labelUsedAtInfo(string label);

        /// <summary>
        /// Return dictionary of line numbers with label clash descriptions
        /// </summary>
        SortedDictionary<uint, string> labelClashes { get; }

        /// <summary>
        /// Return dictionary of line numbers with undefined label descriptions
        /// </summary>
        SortedDictionary<uint, string> undefinedLabels { get; }

        void reset_Async();
        void reset_Sync();

        /// <summary>
        /// Get the error list provider that is used by this LabelGraph
        /// </summary>
        ErrorListProvider errorListProvider { get; }

        SortedDictionary<string, string> getLabelDescriptions { get; }
    }
}