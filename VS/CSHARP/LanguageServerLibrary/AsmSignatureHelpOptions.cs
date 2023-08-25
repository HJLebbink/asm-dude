using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.Runtime.Serialization;

namespace LanguageServer
{
    [DataContract]
    class AsmSignatureHelpOptions : SignatureHelpOptions
    {
        [DataMember(Name = "asmSignatureHelp")]
        public bool AsmSignatureHelp
        {
            get;
            set;
        }
    }
}
