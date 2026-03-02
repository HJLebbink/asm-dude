using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmDude2LS
{
    public enum AsmDiagnosticTag
    {
        Unnecessary = DiagnosticTag.Unnecessary,
        Deprecated = DiagnosticTag.Deprecated,
        BuildError = (int)VSDiagnosticTags.BuildError,
        IntellisenseError = (int)VSDiagnosticTags.IntellisenseError,
        AsmDudeSimulatorError = -9,
    }
}
