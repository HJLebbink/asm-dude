using Microsoft.VisualStudio.LanguageServer.Protocol;
using System.Runtime.Serialization;

namespace LanguageServer
{
    [DataContract]
    class MockServerCapabilities : VSServerCapabilities
    {
        [DataMember(Name = "mock")]
        public bool Mock
        {
            get;
            set;
        }
    }
}
