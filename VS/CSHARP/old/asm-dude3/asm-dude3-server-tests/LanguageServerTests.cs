using AsmDude3.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

public class LanguageServerTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly LanguageServer _server;

    public LanguageServerTests()
    {
        _loggerMock = new Mock<ILogger>();
        _server = new LanguageServer(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new LanguageServer(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var server = new LanguageServer(_loggerMock.Object);

        // Assert
        server.Should().NotBeNull();
        server.IsInitialized.Should().BeFalse();
    }

    #endregion

    #region Initialize Tests

    [Fact]
    public void Initialize_WithValidParams_ReturnsCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams
        {
            ProcessId = 1234,
            ClientInfo = new ClientInfo { Name = "TestClient", Version = "1.0.0" },
            RootUri = "file:///test/workspace"
        };

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        result.Should().NotBeNull();
        result.Capabilities.Should().NotBeNull();
        result.ServerInfo.Should().NotBeNull();
        result.ServerInfo!.Name.Should().Be("AsmDude3 Language Server");
        result.ServerInfo.Version.Should().Be("3.0.0");
    }

    [Fact]
    public void Initialize_SetsTextDocumentSyncCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams
        {
            ProcessId = 1234,
            ClientInfo = new ClientInfo { Name = "TestClient" }
        };

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        var textDocSync = result.Capabilities.TextDocumentSync;
        textDocSync.Should().NotBeNull();
        textDocSync!.OpenClose.Should().BeTrue();
        textDocSync.Change.Should().Be(TextDocumentSyncKind.Full);
        textDocSync.Save.Should().NotBeNull();
        textDocSync.Save!.IncludeText.Should().BeFalse();
    }

    [Fact]
    public void Initialize_SetsCompletionCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams();

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        var completion = result.Capabilities.CompletionProvider;
        completion.Should().NotBeNull();
        completion!.TriggerCharacters.Should().Contain(".", "[", " ");
        completion.ResolveProvider.Should().BeFalse();
    }

    [Fact]
    public void Initialize_SetsHoverCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams();

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        result.Capabilities.HoverProvider.Should().BeTrue();
    }

    [Fact]
    public void Initialize_SetsSignatureHelpCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams();

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        var sigHelp = result.Capabilities.SignatureHelpProvider;
        sigHelp.Should().NotBeNull();
        sigHelp!.TriggerCharacters.Should().Contain(" ", ",");
    }

    [Fact]
    public void Initialize_SetsSemanticTokensCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams();

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        var semanticTokens = result.Capabilities.SemanticTokensProvider;
        semanticTokens.Should().NotBeNull();
        semanticTokens!.Full.Should().BeTrue();
        semanticTokens.Range.Should().BeFalse();

        var legend = semanticTokens.Legend;
        legend.TokenTypes.Should().Contain("keyword", "operator", "variable", "register", "number", "comment");
        legend.TokenModifiers.Should().Contain("readonly", "documentation");
    }

    [Fact]
    public void Initialize_SetsFoldingRangeCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams();

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        result.Capabilities.FoldingRangeProvider.Should().BeTrue();
    }

    [Fact]
    public void Initialize_SetsDocumentSymbolCapabilities()
    {
        // Arrange
        var initParams = new InitializeParams();

        // Act
        var result = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);

        // Assert
        // DocumentSymbolProvider is temporarily disabled to avoid VS crash
        result.Capabilities.DocumentSymbolProvider.Should().BeFalse();
    }

    #endregion

    #region Initialized Tests

    [Fact]
    public void Initialized_SetsServerAsInitialized()
    {
        // Arrange
        var initParams = new InitializeParams();
        _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);
        _server.IsInitialized.Should().BeFalse();

        // Act
        _server.Initialized();

        // Assert
        _server.IsInitialized.Should().BeTrue();
    }

    #endregion

    #region Shutdown Tests

    [Fact]
    public void Shutdown_SetsServerAsNotInitialized()
    {
        // Arrange
        var initParams = new InitializeParams();
        _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);
        _server.Initialized();
        _server.IsInitialized.Should().BeTrue();

        // Act
        _server.Shutdown();

        // Assert
        _server.IsInitialized.Should().BeFalse();
    }

    [Fact]
    public void Shutdown_ReturnsNull()
    {
        // Act
        var result = _server.Shutdown();

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Exit Tests

    [Fact]
    public async Task ExitNotification_CompletesWaitForExitTask()
    {
        // Arrange
        var waitTask = _server.WaitForExitAsync();
        waitTask.IsCompleted.Should().BeFalse();

        // Act
        _server.ExitNotification();

        // Assert
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        waitTask.IsCompleted.Should().BeTrue();
    }

    [Fact]
    public async Task Exit_CompletesWaitForExitTask()
    {
        // Arrange
        var waitTask = _server.WaitForExitAsync();

        // Act
        _server.Exit();

        // Assert
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        waitTask.IsCompleted.Should().BeTrue();
    }

    #endregion

    #region Document Lifecycle Tests

    [Fact]
    public void DidOpenTextDocument_AddsDocumentToManager()
    {
        // Arrange
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = "file:///test.asm",
                LanguageId = "asm",
                Version = 1,
                Text = "mov rax, rbx"
            }
        };

        // Act
        _server.DidOpenTextDocument(openParams.TextDocument);

        // Assert
        _server.DocumentManager.DocumentCount.Should().Be(1);
        _server.DocumentManager.IsDocumentOpen(openParams.TextDocument.Uri).Should().BeTrue();
    }

    [Fact]
    public void DidChangeTextDocument_UpdatesDocument()
    {
        // Arrange
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = "file:///test.asm",
                LanguageId = "asm",
                Version = 1,
                Text = "original"
            }
        };
        _server.DidOpenTextDocument(openParams.TextDocument);

        var changeParams = new DidChangeTextDocumentParams
        {
            TextDocument = new VersionedTextDocumentIdentifier
            {
                Uri = openParams.TextDocument.Uri,
                Version = 2
            },
            ContentChanges = new[]
            {
                new TextDocumentContentChangeEvent { Text = "modified" }
            }
        };

        // Act
        _server.DidChangeTextDocument(changeParams.TextDocument, changeParams.ContentChanges);

        // Assert
        var state = _server.DocumentManager.GetDocument(openParams.TextDocument.Uri);
        state!.Text.Should().Be("modified");
    }

    [Fact]
    public void DidCloseTextDocument_RemovesDocument()
    {
        // Arrange
        var openParams = new DidOpenTextDocumentParams
        {
            TextDocument = new TextDocumentItem
            {
                Uri = "file:///test.asm",
                LanguageId = "asm",
                Version = 1,
                Text = "test"
            }
        };
        _server.DidOpenTextDocument(openParams.TextDocument);

        var closeParams = new DidCloseTextDocumentParams
        {
            TextDocument = new TextDocumentIdentifier
            {
                Uri = openParams.TextDocument.Uri
            }
        };

        // Act
        _server.DidCloseTextDocument(closeParams.TextDocument);

        // Assert
        _server.DocumentManager.DocumentCount.Should().Be(0);
        _server.DocumentManager.IsDocumentOpen(openParams.TextDocument.Uri).Should().BeFalse();
    }

    #endregion

    #region Complete Lifecycle Test

    [Fact]
    public async Task CompleteLifecycle_InitializeToExit_WorksCorrectly()
    {
        // Arrange
        var initParams = new InitializeParams
        {
            ProcessId = 1234,
            ClientInfo = new ClientInfo { Name = "TestClient", Version = "1.0.0" }
        };

        // Act & Assert - Initialize
        var initResult = _server.Initialize(initParams.ProcessId, initParams.RootUri, initParams.Capabilities);
        initResult.Should().NotBeNull();
        _server.IsInitialized.Should().BeFalse();

        // Initialized
        _server.Initialized();
        _server.IsInitialized.Should().BeTrue();

        // Open document
        _server.DidOpenTextDocument(new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "mov rax, rbx"
        });
        _server.DocumentManager.DocumentCount.Should().Be(1);

        // Shutdown
        _server.Shutdown();
        _server.IsInitialized.Should().BeFalse();

        // Exit
        var waitTask = _server.WaitForExitAsync();
        _server.ExitNotification();
        await waitTask.WaitAsync(TimeSpan.FromSeconds(1));
        waitTask.IsCompleted.Should().BeTrue();
    }

    #endregion
}
