// Visual Studio specific LSP types and extensions
// These extend the standard LSP types from Microsoft.CodeAnalysis.LanguageServer.Protocol (Roslyn)
// Used for VS-specific functionality like diagnostics with project context

using Roslyn.LanguageServer.Protocol;
using System;

namespace AsmDude2LS
{
    /// <summary>
    /// Visual Studio trace settings for LSP logging
    /// </summary>
    public enum TraceSetting
    {
        Off,
        Messages,
        Verbose
    }

    /// <summary>
    /// VS-specific diagnostic tags
    /// </summary>
    public static class VSDiagnosticTags
    {
        public const int BuildError = -1;
        public const int IntellisenseError = -2;
    }

    /// <summary>
    /// VS-specific diagnostic with additional properties
    /// </summary>
    public class VSDiagnostic : Diagnostic
    {
        public VSDiagnosticProjectInformation[] Projects { get; set; }
        public string Identifier { get; set; }
    }

    /// <summary>
    /// VS-specific project information for diagnostics
    /// </summary>
    public class VSDiagnosticProjectInformation
    {
        public string ProjectName { get; set; }
        public string ProjectIdentifier { get; set; }
        public string Context { get; set; }
    }

    /// <summary>
    /// VS-specific text document identifier with project context
    /// </summary>
    public class VSTextDocumentIdentifier : TextDocumentIdentifier
    {
        public VSProjectContext ProjectContext { get; set; }
    }

    /// <summary>
    /// VS-specific project context
    /// </summary>
    public class VSProjectContext
    {
        public string Label { get; set; }
        public string Id { get; set; }
        public string Kind { get; set; }
    }

    /// <summary>
    /// VS-specific project context list
    /// </summary>
    public class VSProjectContextList
    {
        public VSProjectContext[] ProjectContexts { get; set; }
        public int DefaultIndex { get; set; }
    }

    /// <summary>
    /// VS-specific symbol information with additional properties
    /// </summary>
    public class VSSymbolInformation : SymbolInformation
    {
        public string HintText { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// VS-specific server capabilities
    /// </summary>
    public class VSServerCapabilities : ServerCapabilities
    {
        // VS-specific capabilities can be added here
    }

    /// <summary>
    /// VS-specific methods not in standard LSP
    /// </summary>
    public static class VSMethods
    {
        public const string QueryWorkspaceFoldersMethodName = "_vs/queryWorkspaceFolders";
        public const string GetProjectContextsName = "textDocument/_vs_getProjectContexts";
    }

    /// <summary>
    /// LSP method name constants - LspTypes uses different naming, this provides compatibility
    /// </summary>
    public static class Methods
    {
        // Lifecycle
        public const string InitializeName = "initialize";
        public const string InitializedName = "initialized";
        public const string ShutdownName = "shutdown";
        public const string ExitName = "exit";

        // Text Document
        public const string TextDocumentDidOpenName = "textDocument/didOpen";
        public const string TextDocumentDidCloseName = "textDocument/didClose";
        public const string TextDocumentDidChangeName = "textDocument/didChange";
        public const string TextDocumentDidSaveName = "textDocument/didSave";
        public const string TextDocumentWillSaveName = "textDocument/willSave";
        public const string TextDocumentWillSaveWaitUntilName = "textDocument/willSaveWaitUntil";
        public const string TextDocumentCompletionName = "textDocument/completion";
        public const string TextDocumentCompletionResolveName = "completionItem/resolve";
        public const string TextDocumentHoverName = "textDocument/hover";
        public const string TextDocumentSignatureHelpName = "textDocument/signatureHelp";
        public const string TextDocumentDefinitionName = "textDocument/definition";
        public const string TextDocumentTypeDefinitionName = "textDocument/typeDefinition";
        public const string TextDocumentImplementationName = "textDocument/implementation";
        public const string TextDocumentReferencesName = "textDocument/references";
        public const string TextDocumentDocumentHighlightName = "textDocument/documentHighlight";
        public const string TextDocumentDocumentSymbolName = "textDocument/documentSymbol";
        public const string TextDocumentCodeActionName = "textDocument/codeAction";
        public const string TextDocumentCodeLensName = "textDocument/codeLens";
        public const string CodeLensResolveName = "codeLens/resolve";
        public const string TextDocumentDocumentLinkName = "textDocument/documentLink";
        public const string DocumentLinkResolveName = "documentLink/resolve";
        public const string TextDocumentFormattingName = "textDocument/formatting";
        public const string TextDocumentRangeFormattingName = "textDocument/rangeFormatting";
        public const string TextDocumentOnTypeFormattingName = "textDocument/onTypeFormatting";
        public const string TextDocumentRenameName = "textDocument/rename";
        public const string TextDocumentFoldingRangeName = "textDocument/foldingRange";
        public const string TextDocumentPublishDiagnosticsName = "textDocument/publishDiagnostics";

        // Window
        public const string WindowLogMessageName = "window/logMessage";
        public const string WindowShowMessageName = "window/showMessage";
        public const string WindowShowMessageRequestName = "window/showMessageRequest";

        // Workspace
        public const string WorkspaceApplyEditName = "workspace/applyEdit";
        public const string WorkspaceDidChangeConfigurationName = "workspace/didChangeConfiguration";
        public const string WorkspaceDidChangeWatchedFilesName = "workspace/didChangeWatchedFiles";
        public const string WorkspaceSymbolName = "workspace/symbol";
        public const string WorkspaceExecuteCommandName = "workspace/executeCommand";

        // Client
        public const string ClientUnregisterCapabilityName = "client/unregisterCapability";

        // Telemetry
        public const string TelemetryEventName = "telemetry/event";

        // Progress
        public const string ProgressNotificationName = "$/progress";
        public const string PartialResultTokenName = "partialResultToken";
        public const string PartialResultTokenPropertyName = "partialResultToken";
        public const string ProgressNotificationTokenName = "progressNotificationToken";
    }

    /// <summary>
    /// Additional LSP method names not covered in Methods
    /// </summary>
    public static class MethodsExtensions
    {
        // Work done progress
        public const string WorkDoneTokenName = "$/progress";

        // Code action resolve
        public const string CodeActionResolveName = "codeAction/resolve";

        // Document color
        public const string TextDocumentDocumentColorName = "textDocument/documentColor";

        // Semantic tokens
        public const string TextDocumentSemanticTokensFullName = "textDocument/semanticTokens/full";
        public const string TextDocumentSemanticTokensRangeName = "textDocument/semanticTokens/range";
        public const string TextDocumentSemanticTokensFullDeltaName = "textDocument/semanticTokens/full/delta";

        // Linked editing
        public const string TextDocumentLinkedEditingRangeName = "textDocument/linkedEditingRange";

        // Inlay hints (LSP 3.17)
        public const string TextDocumentInlayHintName = "textDocument/inlayHint";
        public const string InlayHintResolveName = "inlayHint/resolve";

        // Workspace configuration
        public const string WorkspaceConfigurationName = "workspace/configuration";

        // Diagnostics - LSP 3.16 uses textDocument/publishDiagnostics
        public const string TextDocumentPublishDiagnosticsName = "textDocument/publishDiagnostics";
    }

    /// <summary>
    /// Extension methods to help with LspTypes type conversions
    /// </summary>
    public static class LspTypeExtensions
    {
        /// <summary>
        /// Convert int to uint for Position parameters
        /// </summary>
        public static uint ToUInt(this int value) => (uint)value;

        /// <summary>
        /// Convert uint to int for internal use
        /// </summary>
        public static int ToInt(this uint value) => (int)value;

        /// <summary>
        /// Get local file path from URI string
        /// </summary>
        public static string GetLocalPath(this string uriString)
        {
            if (string.IsNullOrEmpty(uriString)) return string.Empty;
            try
            {
                var uri = new System.Uri(uriString);
                return uri.LocalPath;
            }
            catch
            {
                return uriString;
            }
        }

        /// <summary>
        /// Get absolute path from URI string
        /// </summary>
        public static string GetAbsolutePath(this string uriString)
        {
            if (string.IsNullOrEmpty(uriString)) return string.Empty;
            try
            {
                var uri = new System.Uri(uriString);
                return uri.AbsolutePath;
            }
            catch
            {
                return uriString;
            }
        }

        /// <summary>
        /// Get absolute URI from URI string
        /// </summary>
        public static string GetAbsoluteUri(this string uriString)
        {
            if (string.IsNullOrEmpty(uriString)) return string.Empty;
            try
            {
                var uri = new System.Uri(uriString);
                return uri.AbsoluteUri;
            }
            catch
            {
                return uriString;
            }
        }
    }

    /// <summary>
    /// Additional CompletionItemKind values
    /// </summary>
    public static class CompletionItemKindExtensions
    {
        // Macro is not in LSP 3.16 spec, use Snippet as equivalent
        public const CompletionItemKind Macro = CompletionItemKind.Snippet;

        // None is not in LSP 3.16 spec, use Text as a fallback
        public const CompletionItemKind None = CompletionItemKind.Text;
    }

    /// <summary>
    /// LSP notification type for methods that don't return a result
    /// </summary>
    public class LspNotification<TParams>
    {
        public string Name { get; }

        public LspNotification(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// LSP request type for methods that return a result
    /// </summary>
    public class LspRequest<TParams, TResult>
    {
        public string Name { get; }

        public LspRequest(string name)
        {
            Name = name;
        }
    }

    /// <summary>
    /// Standard LSP method definitions missing from LspTypes package
    /// </summary>
    public static class LspMethods
    {
        /// <summary>
        /// window/logMessage notification
        /// </summary>
        public static readonly LspNotification<LogMessageParams> WindowLogMessage =
            new LspNotification<LogMessageParams>("window/logMessage");

        /// <summary>
        /// window/showMessage notification
        /// </summary>
        public static readonly LspNotification<ShowMessageParams> WindowShowMessage =
            new LspNotification<ShowMessageParams>("window/showMessage");

        /// <summary>
        /// window/showMessageRequest request
        /// </summary>
        public static readonly LspRequest<ShowMessageRequestParams, MessageActionItem> WindowShowMessageRequest =
            new LspRequest<ShowMessageRequestParams, MessageActionItem>("window/showMessageRequest");

        /// <summary>
        /// textDocument/publishDiagnostics notification
        /// </summary>
        public static readonly LspNotification<PublishDiagnosticParams> TextDocumentPublishDiagnostics =
            new LspNotification<PublishDiagnosticParams>("textDocument/publishDiagnostics");
    }

}
