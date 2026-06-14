using Microsoft.VisualStudio.LanguageServer.Protocol;

namespace AsmDude2LS;

public enum AsmDiagnosticTag
{
    Unnecessary = DiagnosticTag.Unnecessary,
    Deprecated = DiagnosticTag.Deprecated,
    BuildError = (int)VSDiagnosticTags.BuildError,
    IntellisenseError = (int)VSDiagnosticTags.IntellisenseError,
    // The Show/Decorate split: HiddenInErrorList = editor squiggle only (Decorate, not Show);
    // HiddenInEditor = Error List row only (Show, not Decorate). See REDUNDANT_DIAGNOSTICS_PLAN.md.
    HiddenInErrorList = (int)VSDiagnosticTags.HiddenInErrorList,
    HiddenInEditor = (int)VSDiagnosticTags.HiddenInEditor,
    AsmDudeSimulatorError = -9,
}
