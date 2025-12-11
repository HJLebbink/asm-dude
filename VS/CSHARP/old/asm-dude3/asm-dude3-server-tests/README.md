# AsmDude3 Server Tests

Comprehensive unit tests for the Language Server.

## Quick Start

```bash
# Run all tests
dotnet test

# Run with detailed output
dotnet test --verbosity detailed

# Run specific test class
dotnet test --filter "FullyQualifiedName~DocumentManagerTests"
```

## Test Structure

### DocumentManagerTests.cs (35+ tests)
Tests for document state management:
- Opening documents
- Updating document content
- Closing documents
- Query operations
- Concurrent access safety
- Edge cases and error handling

### LanguageServerTests.cs (25+ tests)
Tests for LSP protocol handling:
- Initialize/Initialized handshake
- Shutdown/Exit lifecycle
- Document notifications (didOpen/didChange/didClose)
- Server capabilities advertisement
- Complete protocol lifecycle

## Technologies Used

- **xUnit 2.9.3** - Test framework
- **Moq 4.20.72** - Mocking framework
- **FluentAssertions 7.0.0** - Readable assertions

## Test Patterns

### Arrange-Act-Assert
```csharp
[Fact]
public void OpenDocument_WithValidDocument_AddsDocument()
{
    // Arrange
    var document = new TextDocumentItem { ... };

    // Act
    _documentManager.OpenDocument(document);

    // Assert
    _documentManager.DocumentCount.Should().Be(1);
}
```

### Fluent Assertions
```csharp
result.Should().NotBeNull();
result.Text.Should().Be("expected");
result.Lines.Should().HaveCount(3);
```

## Code Coverage

Run with coverage:
```bash
dotnet test --collect:"XPlat Code Coverage"
```

Report location: `TestResults/[guid]/coverage.cobertura.xml`

## CI/CD Integration

These tests are designed to run in CI:
- No external dependencies
- No file system access
- Fast execution (< 5 seconds)
- Deterministic results

## Adding New Tests

1. Create test class inheriting test collection
2. Follow existing naming: `[MethodName]_[Scenario]_[ExpectedResult]`
3. Use Arrange-Act-Assert pattern
4. Add to appropriate test class or create new one
5. Ensure async tests use async/await properly

## Test Categories

- **Unit Tests**: Test single method in isolation
- **Integration Tests**: Test multiple components together
- **Concurrent Tests**: Test thread-safety

## Expected Results

All tests should pass:
```
Passed! - Failed:     0, Passed:    60+, Skipped:     0
```

## Troubleshooting

**Tests won't run:**
- Ensure .NET 10 SDK installed: `dotnet --version`
- Restore packages: `dotnet restore`

**Tests fail:**
- Check you're using correct .NET version
- Verify no code changes broke contracts
- Run tests individually to isolate issue

**Slow tests:**
- Check for infinite loops
- Verify no blocking calls
- Look for unnecessary Thread.Sleep

## Related Documentation

- `../PHASE2_COMPLETE.md` - Phase 2 completion summary
- `../MANUAL_TESTING_GUIDE.md` - Manual VSIX testing
- `../README.md` - Project overview
