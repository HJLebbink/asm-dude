using Microsoft.VisualStudio.Text.Tagging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Text;
using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;
using System.Diagnostics;

namespace AsmDude {

    [Export(typeof(ITaggerProvider))]
    [ContentType("asm!")]
    [TagType(typeof(AsmTokenTag))]
    internal sealed class AsmTokenTagProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            //Debug.WriteLine("INFO: AsmTokenTagProvider:CreateTagger: entering");
            return new AsmTokenTagger(buffer) as ITagger<T>;
        }
    }

    [Export(typeof(ITaggerProvider))]
    [ContentType("text")]
    [TagType(typeof(IOutliningRegionTag))]
    internal sealed class TaggerProvider : ITaggerProvider
    {
        public ITagger<T> CreateTagger<T>(ITextBuffer buffer) where T : ITag
        {
            //Debug.WriteLine("INFO: TaggerProvider:CreateTagger: entering");
            Func<ITagger<T>> sc = delegate () {
                return new OutliningTagger(buffer) as ITagger<T>;
            };
            return buffer.Properties.GetOrCreateSingletonProperty<ITagger<T>>(sc);
        }
    }
}
