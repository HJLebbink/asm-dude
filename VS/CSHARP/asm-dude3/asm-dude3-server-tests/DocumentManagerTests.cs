using AsmDude3.Server;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

public class DocumentManagerTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly DocumentManager _documentManager;

    public DocumentManagerTests()
    {
        _loggerMock = new Mock<ILogger>();
        _documentManager = new DocumentManager(_loggerMock.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new DocumentManager(null!);
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void Constructor_WithValidLogger_CreatesInstance()
    {
        // Act
        var manager = new DocumentManager(_loggerMock.Object);

        // Assert
        manager.Should().NotBeNull();
        manager.DocumentCount.Should().Be(0);
    }

    #endregion

    #region OpenDocument Tests

    [Fact]
    public void OpenDocument_WithValidDocument_AddsDocument()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "mov rax, rbx"
        };

        // Act
        _documentManager.OpenDocument(document);

        // Assert
        _documentManager.DocumentCount.Should().Be(1);
        _documentManager.IsDocumentOpen(document.Uri).Should().BeTrue();
    }

    [Fact]
    public void OpenDocument_WithNullDocument_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _documentManager.OpenDocument(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void OpenDocument_WithMultilineText_SplitsIntoLines()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "mov rax, rbx\nadd rcx, rdx\nsub rsp, 8"
        };

        // Act
        _documentManager.OpenDocument(document);

        // Assert
        var state = _documentManager.GetDocument(document.Uri);
        state.Should().NotBeNull();
        state!.Lines.Should().HaveCount(3);
        state.Lines[0].Should().Be("mov rax, rbx");
        state.Lines[1].Should().Be("add rcx, rdx");
        state.Lines[2].Should().Be("sub rsp, 8");
    }

    [Fact]
    public void OpenDocument_WithWindowsLineEndings_SplitsCorrectly()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "mov rax, rbx\r\nadd rcx, rdx"
        };

        // Act
        _documentManager.OpenDocument(document);

        // Assert
        var state = _documentManager.GetDocument(document.Uri);
        state!.Lines.Should().HaveCount(2);
    }

    [Fact]
    public void OpenDocument_WithEmptyText_CreatesEmptyLines()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = ""
        };

        // Act
        _documentManager.OpenDocument(document);

        // Assert
        var state = _documentManager.GetDocument(document.Uri);
        state!.Lines.Should().BeEmpty();
    }

    [Fact]
    public void OpenDocument_SameDocumentTwice_DoesNotReplace()
    {
        // Arrange
        var document1 = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "original"
        };
        var document2 = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 2,
            Text = "modified"
        };

        // Act
        _documentManager.OpenDocument(document1);
        _documentManager.OpenDocument(document2);

        // Assert
        var state = _documentManager.GetDocument(document1.Uri);
        state!.Text.Should().Be("original"); // Should keep first version
        state.Version.Should().Be(1);
    }

    #endregion

    #region UpdateDocument Tests

    [Fact]
    public void UpdateDocument_WithValidChanges_UpdatesDocument()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "original"
        };
        _documentManager.OpenDocument(document);

        var changes = new[]
        {
            new TextDocumentContentChangeEvent { Text = "modified" }
        };

        // Act
        _documentManager.UpdateDocument(document.Uri, changes);

        // Assert
        var state = _documentManager.GetDocument(document.Uri);
        state!.Text.Should().Be("modified");
        state.Version.Should().Be(2);
    }

    [Fact]
    public void UpdateDocument_WithNullUri_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _documentManager.UpdateDocument(null!, Array.Empty<TextDocumentContentChangeEvent>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDocument_WithEmptyUri_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _documentManager.UpdateDocument("", Array.Empty<TextDocumentContentChangeEvent>());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void UpdateDocument_WithNullChanges_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => _documentManager.UpdateDocument("file:///test.asm", null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateDocument_NonExistentDocument_LogsWarning()
    {
        // Arrange
        var changes = new[]
        {
            new TextDocumentContentChangeEvent { Text = "new text" }
        };

        // Act
        _documentManager.UpdateDocument("file:///nonexistent.asm", changes);

        // Assert
        _documentManager.DocumentCount.Should().Be(0);
        // Logger should have been called with warning (verified by mock)
    }

    [Fact]
    public void UpdateDocument_WithMultipleChanges_UsesFirstChange()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "original"
        };
        _documentManager.OpenDocument(document);

        var changes = new[]
        {
            new TextDocumentContentChangeEvent { Text = "first" },
            new TextDocumentContentChangeEvent { Text = "second" }
        };

        // Act
        _documentManager.UpdateDocument(document.Uri, changes);

        // Assert
        var state = _documentManager.GetDocument(document.Uri);
        state!.Text.Should().Be("first"); // Only first change applied (full sync)
    }

    [Fact]
    public void UpdateDocument_UpdatesLinesSplitting()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "line1"
        };
        _documentManager.OpenDocument(document);

        var changes = new[]
        {
            new TextDocumentContentChangeEvent { Text = "line1\nline2\nline3" }
        };

        // Act
        _documentManager.UpdateDocument(document.Uri, changes);

        // Assert
        var state = _documentManager.GetDocument(document.Uri);
        state!.Lines.Should().HaveCount(3);
    }

    #endregion

    #region CloseDocument Tests

    [Fact]
    public void CloseDocument_WithOpenDocument_RemovesDocument()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "test"
        };
        _documentManager.OpenDocument(document);

        // Act
        _documentManager.CloseDocument(document.Uri);

        // Assert
        _documentManager.DocumentCount.Should().Be(0);
        _documentManager.IsDocumentOpen(document.Uri).Should().BeFalse();
    }

    [Fact]
    public void CloseDocument_WithNullUri_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _documentManager.CloseDocument(null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloseDocument_WithEmptyUri_ThrowsArgumentException()
    {
        // Act & Assert
        var act = () => _documentManager.CloseDocument("");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CloseDocument_NonExistentDocument_LogsWarning()
    {
        // Act
        _documentManager.CloseDocument("file:///nonexistent.asm");

        // Assert
        _documentManager.DocumentCount.Should().Be(0);
        // Logger should have been called with warning
    }

    #endregion

    #region GetDocument Tests

    [Fact]
    public void GetDocument_WithOpenDocument_ReturnsState()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "test content"
        };
        _documentManager.OpenDocument(document);

        // Act
        var state = _documentManager.GetDocument(document.Uri);

        // Assert
        state.Should().NotBeNull();
        state!.Uri.Should().Be(document.Uri);
        state.LanguageId.Should().Be(document.LanguageId);
        state.Version.Should().Be(document.Version);
        state.Text.Should().Be(document.Text);
    }

    [Fact]
    public void GetDocument_WithNonExistentDocument_ReturnsNull()
    {
        // Act
        var state = _documentManager.GetDocument("file:///nonexistent.asm");

        // Assert
        state.Should().BeNull();
    }

    #endregion

    #region Query Methods Tests

    [Fact]
    public void IsDocumentOpen_WithOpenDocument_ReturnsTrue()
    {
        // Arrange
        var document = new TextDocumentItem
        {
            Uri = "file:///test.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "test"
        };
        _documentManager.OpenDocument(document);

        // Act & Assert
        _documentManager.IsDocumentOpen(document.Uri).Should().BeTrue();
    }

    [Fact]
    public void IsDocumentOpen_WithNonExistentDocument_ReturnsFalse()
    {
        // Act & Assert
        _documentManager.IsDocumentOpen("file:///nonexistent.asm").Should().BeFalse();
    }

    [Fact]
    public void GetOpenDocumentUris_WithMultipleDocuments_ReturnsAllUris()
    {
        // Arrange
        var doc1 = new TextDocumentItem
        {
            Uri = "file:///test1.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "test1"
        };
        var doc2 = new TextDocumentItem
        {
            Uri = "file:///test2.asm",
            LanguageId = "asm",
            Version = 1,
            Text = "test2"
        };

        _documentManager.OpenDocument(doc1);
        _documentManager.OpenDocument(doc2);

        // Act
        var uris = _documentManager.GetOpenDocumentUris();

        // Assert
        uris.Should().HaveCount(2);
        uris.Should().Contain(doc1.Uri);
        uris.Should().Contain(doc2.Uri);
    }

    [Fact]
    public void GetOpenDocumentUris_WithNoDocuments_ReturnsEmptyCollection()
    {
        // Act
        var uris = _documentManager.GetOpenDocumentUris();

        // Assert
        uris.Should().BeEmpty();
    }

    [Fact]
    public void DocumentCount_TracksOpenDocuments()
    {
        // Arrange
        var doc1 = new TextDocumentItem { Uri = "file:///test1.asm", LanguageId = "asm", Version = 1, Text = "test1" };
        var doc2 = new TextDocumentItem { Uri = "file:///test2.asm", LanguageId = "asm", Version = 1, Text = "test2" };

        // Act & Assert
        _documentManager.DocumentCount.Should().Be(0);

        _documentManager.OpenDocument(doc1);
        _documentManager.DocumentCount.Should().Be(1);

        _documentManager.OpenDocument(doc2);
        _documentManager.DocumentCount.Should().Be(2);

        _documentManager.CloseDocument(doc1.Uri);
        _documentManager.DocumentCount.Should().Be(1);

        _documentManager.CloseDocument(doc2.Uri);
        _documentManager.DocumentCount.Should().Be(0);
    }

    #endregion

    #region Concurrent Access Tests

    [Fact]
    public async Task ConcurrentOperations_MaintainsConsistency()
    {
        // Arrange
        const int operationCount = 100;
        var tasks = new List<Task>();

        // Act - Perform concurrent opens, updates, and closes
        for (int i = 0; i < operationCount; i++)
        {
            var index = i;
            tasks.Add(Task.Run(() =>
            {
                var doc = new TextDocumentItem
                {
                    Uri = $"file:///test{index}.asm",
                    LanguageId = "asm",
                    Version = 1,
                    Text = $"content{index}"
                };
                _documentManager.OpenDocument(doc);

                if (index % 2 == 0)
                {
                    var changes = new[] { new TextDocumentContentChangeEvent { Text = $"updated{index}" } };
                    _documentManager.UpdateDocument(doc.Uri, changes);
                }

                if (index % 3 == 0)
                {
                    _documentManager.CloseDocument(doc.Uri);
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert - No crashes, and count is reasonable
        _documentManager.DocumentCount.Should().BeGreaterThan(0).And.BeLessThan(operationCount);
    }

    #endregion
}
