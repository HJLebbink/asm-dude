using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmDude2LS
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