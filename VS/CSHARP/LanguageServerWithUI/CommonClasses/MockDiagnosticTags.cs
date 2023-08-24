using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace LanguageServerWithUI
{
    public enum MockDiagnosticTags
    {
        Unnecessary = DiagnosticTag.Unnecessary,
        Deprecated = DiagnosticTag.Deprecated,
        BuildError = VSDiagnosticTags.BuildError,
        IntellisenseError = VSDiagnosticTags.IntellisenseError,
    }
}
