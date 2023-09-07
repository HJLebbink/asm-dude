using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace LanguageServerLibrary
{
    public enum AsmDiagnosticTag
    {
        Unnecessary = DiagnosticTag.Unnecessary,
        Deprecated = DiagnosticTag.Deprecated,
        BuildError = VSDiagnosticTags.BuildError,
        IntellisenseError = VSDiagnosticTags.IntellisenseError,
        AsmDudeSimulatorError = -9,
    }
}