using AsmDude3.Server.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AsmDude3.Server.Tests;

[Trait("LongRunning", "true")]
public class SymbolicAnalysisProviderTests
{
    private readonly Mock<ILogger> _loggerMock;
    private readonly SymbolicAnalysisProvider _provider;

    public SymbolicAnalysisProviderTests()
    {
        _loggerMock = new Mock<ILogger>();
        _provider = new SymbolicAnalysisProvider(_loggerMock.Object);
    }

    #region Constructor Tests

    [DebugSkippableFact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        var act = () => new SymbolicAnalysisProvider(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion

    #region Basic Symbolic Execution Tests

    [DebugSkippableFact]
    public void AnalyzeDocument_EmptyDocument_ReturnsNull()
    {
        var lines = Array.Empty<string>();
        var result = _provider.AnalyzeDocument(lines);
        result.Should().BeNull();
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_SimpleMovInstruction_InfersRegisterValue()
    {
        var lines = new[] { "mov rax, 10" };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates.Should().ContainKey(0);
        result.RegisterStates[0].RegisterValues.Should().ContainKey("RAX");
        result.RegisterStates[0].RegisterValues["RAX"].Should().Be("0x000000000000000A");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_MovSequence_TracksMultipleRegisters()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "mov rbx, 5",
            "mov rcx, 20"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[2].RegisterValues["RAX"].Should().Be("0x000000000000000A");
        result.RegisterStates[2].RegisterValues["RBX"].Should().Be("0x0000000000000005");
        result.RegisterStates[2].RegisterValues["RCX"].Should().Be("0x0000000000000014");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_AddInstruction_InfersResult()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "mov rbx, 5",
            "add rax, rbx"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[2].RegisterValues["RAX"].Should().Be("0x000000000000000F");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_SubInstruction_InfersResult()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "sub rax, 3"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[1].RegisterValues["RAX"].Should().Be("0x0000000000000007");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_ArithmeticSequence_TracksCorrectly()
    {
        var lines = new[]
        {
            "mov rax, 100",
            "add rax, 50",
            "sub rax, 25",
            "mov rbx, rax"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[3].RegisterValues["RAX"].Should().Be("0x000000000000007D"); // 125
        result.RegisterStates[3].RegisterValues["RBX"].Should().Be("0x000000000000007D");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_FlagTracking_ZeroFlag()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "sub rax, 10" // Result is 0, ZF should be set
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[1].FlagValues.Should().ContainKey("ZF");
        result.RegisterStates[1].FlagValues["ZF"].Should().BeTrue();
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_FlagTracking_CarryFlag()
    {
        var lines = new[]
        {
            "mov rax, 5",
            "sub rax, 10" // 5 - 10 = -5 (underflow), CF should be set
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[1].FlagValues.Should().ContainKey("CF");
        result.RegisterStates[1].FlagValues["CF"].Should().BeTrue();
    }

    #endregion

    #region Register State Tests

    [DebugSkippableFact]
    public void GetRegisterStateAtLine_ValidLine_ReturnsState()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "add rax, 5"
        };
        var state = _provider.GetRegisterStateAtLine(lines, 1);

        state.Should().NotBeNull();
        state!.LineNumber.Should().Be(1);
        state.RegisterValues["RAX"].Should().Be("0x000000000000000F");
    }

    [DebugSkippableFact]
    public void GetRegisterStateAtLine_InvalidLine_ReturnsNull()
    {
        var lines = new[] { "mov rax, 10" };
        var state = _provider.GetRegisterStateAtLine(lines, 10);

        state.Should().BeNull();
    }

    [DebugSkippableFact]
    public void GetRegisterStateAtLine_NegativeLine_ReturnsNull()
    {
        var lines = new[] { "mov rax, 10" };
        var state = _provider.GetRegisterStateAtLine(lines, -1);

        state.Should().BeNull();
    }

    #endregion

    #region Diagnostic Tests - Undefined Values

    [DebugSkippableFact]
    public void FindIssues_UndefinedRegisterUse_ReportsWarning()
    {
        var lines = new[]
        {
            "add rax, rbx" // RBX is undefined
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().ContainSingle(d =>
            d.Severity == DiagnosticSeverity.Warning &&
            d.Message.Contains("undefined", StringComparison.OrdinalIgnoreCase) &&
            d.Message.Contains("RBX", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void FindIssues_DefinedRegisterUse_NoWarning()
    {
        var lines = new[]
        {
            "mov rbx, 10",
            "add rax, rbx"
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().NotContain(d =>
            d.Message.Contains("undefined", StringComparison.OrdinalIgnoreCase) &&
            d.Message.Contains("RBX", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void FindIssues_MultipleUndefinedRegisters_ReportsMultipleWarnings()
    {
        var lines = new[]
        {
            "add rax, rbx", // RBX undefined
            "add rcx, rdx"  // RDX undefined
        };
        var issues = _provider.FindIssues(lines);

        issues.Where(d =>
            d.Severity == DiagnosticSeverity.Warning &&
            d.Message.Contains("undefined", StringComparison.OrdinalIgnoreCase)).Should().HaveCountGreaterOrEqualTo(2);
    }

    #endregion

    #region Diagnostic Tests - Redundant Operations

    [DebugSkippableFact]
    public void FindIssues_RedundantMov_ReportsDiagnostic()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "mov rax, 10" // Redundant - RAX already has value 10
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().ContainSingle(d =>
            d.Line == 1 &&
            d.Severity == DiagnosticSeverity.Information &&
            d.Message.Contains("redundant", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void FindIssues_RedundantAdd_ReportsDiagnostic()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "add rax, 0" // Redundant - adding 0
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().ContainSingle(d =>
            d.Line == 1 &&
            d.Severity == DiagnosticSeverity.Information &&
            d.Message.Contains("redundant", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void FindIssues_RedundantSub_ReportsDiagnostic()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "sub rax, 0" // Redundant - subtracting 0
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().ContainSingle(d =>
            d.Line == 1 &&
            d.Severity == DiagnosticSeverity.Information &&
            d.Message.Contains("redundant", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void FindIssues_NonRedundantMov_NoDiagnostic()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "mov rax, 20" // Not redundant - different value
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().NotContain(d =>
            d.Message.Contains("redundant", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Diagnostic Tests - Dead Code

    [DebugSkippableFact]
    public void FindIssues_UnreachableCode_ReportsWarning()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "jmp label1",
            "mov rbx, 5",  // Dead code - never reached
            "label1:",
            "ret"
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().ContainSingle(d =>
            d.Line == 2 &&
            d.Severity == DiagnosticSeverity.Warning &&
            d.Message.Contains("unreachable", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void FindIssues_AlwaysFalseBranch_ReportsInfo()
    {
        var lines = new[]
        {
            "xor rax, rax",  // Sets RAX to 0
            "test rax, rax", // Tests RAX (ZF will be set)
            "jnz label1",    // Jump if not zero - will never jump
            "mov rbx, 1",
            "label1:",
            "ret"
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().ContainSingle(d =>
            d.Line == 2 &&
            d.Severity == DiagnosticSeverity.Information &&
            d.Message.Contains("never", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Optimization Hint Tests

    [DebugSkippableFact]
    public void AnalyzeDocument_RedundantMov_SuggestsRemoval()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "mov rax, 10"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.Optimizations.Should().ContainSingle(o =>
            o.Line == 1 &&
            o.Title.Contains("Remove redundant", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_AddZero_SuggestsRemoval()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "add rax, 0"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.Optimizations.Should().ContainSingle(o =>
            o.Line == 1 &&
            o.Title.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_MovToSelf_SuggestsRemoval()
    {
        var lines = new[]
        {
            "mov rax, rax" // Moving register to itself
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.Optimizations.Should().ContainSingle(o =>
            o.Line == 0 &&
            o.Title.Contains("Remove", StringComparison.OrdinalIgnoreCase));
    }

    #endregion

    #region Comment and Label Handling

    [DebugSkippableFact]
    public void AnalyzeDocument_WithComments_IgnoresComments()
    {
        var lines = new[]
        {
            "; This is a comment",
            "mov rax, 10",
            "add rax, 5 ; inline comment"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[2].RegisterValues["RAX"].Should().Be("0x000000000000000F");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_WithLabels_HandlesCorrectly()
    {
        var lines = new[]
        {
            "main:",
            "    mov rax, 10",
            "    add rax, 5",
            "    ret"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates.Should().ContainKey(2);
        result.RegisterStates[2].RegisterValues["RAX"].Should().Be("0x000000000000000F");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_WithEmptyLines_HandlesCorrectly()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "",
            "add rax, 5"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[2].RegisterValues["RAX"].Should().Be("0x000000000000000F");
    }

    #endregion

    #region Edge Cases

    [DebugSkippableFact]
    public void AnalyzeDocument_NullLines_ThrowsArgumentNullException()
    {
        var act = () => _provider.AnalyzeDocument(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_InvalidSyntax_ReturnsPartialResults()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "invalid instruction here",
            "add rax, 5"
        };
        var result = _provider.AnalyzeDocument(lines);

        // Should still return results for valid lines
        result.Should().NotBeNull();
        result!.RegisterStates.Should().ContainKey(0); // First valid line
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_HexValues_ParsesCorrectly()
    {
        var lines = new[]
        {
            "mov rax, 0x10",
            "add rax, 0x05"
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[1].RegisterValues["RAX"].Should().Be("0x0000000000000015");
    }

    [DebugSkippableFact]
    public void AnalyzeDocument_LargeValues_HandlesCorrectly()
    {
        var lines = new[]
        {
            "mov rax, 0xFFFFFFFFFFFFFFFF" // Max 64-bit value
        };
        var result = _provider.AnalyzeDocument(lines);

        result.Should().NotBeNull();
        result!.RegisterStates[0].RegisterValues["RAX"].Should().Be("0xFFFFFFFFFFFFFFFF");
    }

    #endregion

    #region Cache Behavior Tests

    [DebugSkippableFact]
    public void AnalyzeDocument_SameDocumentTwice_UsesCache()
    {
        var lines = new[] { "mov rax, 10" };

        var result1 = _provider.AnalyzeDocument(lines);
        var result2 = _provider.AnalyzeDocument(lines);

        result1.Should().BeSameAs(result2, "should use cached result");
    }

    [DebugSkippableFact]
    public void InvalidateCache_AfterInvalidation_ReanalyzesDocument()
    {
        var lines = new[] { "mov rax, 10" };
        var uri = "file:///test.asm";

        var result1 = _provider.AnalyzeDocument(lines);
        _provider.InvalidateCache(uri);
        var result2 = _provider.AnalyzeDocument(lines);

        result1.Should().NotBeSameAs(result2, "should re-analyze after cache invalidation");
    }

    #endregion

    #region Performance Tests

    [DebugSkippableFact]
    public void AnalyzeDocument_LargeDocument_CompletesInReasonableTime()
    {
        // Generate 100 instructions
        var lines = Enumerable.Range(0, 100)
            .Select(i => $"mov rax, {i}")
            .ToArray();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = _provider.AnalyzeDocument(lines);
        sw.Stop();

        result.Should().NotBeNull();
        sw.ElapsedMilliseconds.Should().BeLessThan(5000, "analysis should complete in under 5 seconds");
    }

    #endregion

    #region Helper Method Tests

    [DebugSkippableFact]
    public void GetRegisterStateAtLine_FirstLine_ReturnsCorrectState()
    {
        var lines = new[] { "mov rax, 10", "add rax, 5" };
        var state = _provider.GetRegisterStateAtLine(lines, 0);

        state.Should().NotBeNull();
        state!.LineNumber.Should().Be(0);
        state.RegisterValues["RAX"].Should().Be("0x000000000000000A");
    }

    [DebugSkippableFact]
    public void GetRegisterStateAtLine_LastLine_ReturnsCorrectState()
    {
        var lines = new[] { "mov rax, 10", "add rax, 5" };
        var state = _provider.GetRegisterStateAtLine(lines, 1);

        state.Should().NotBeNull();
        state!.LineNumber.Should().Be(1);
        state.RegisterValues["RAX"].Should().Be("0x000000000000000F");
    }

    [DebugSkippableFact]
    public void FindIssues_NoIssues_ReturnsEmptyList()
    {
        var lines = new[]
        {
            "mov rax, 10",
            "mov rbx, 5",
            "add rax, rbx"
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().BeEmpty();
    }

    [DebugSkippableFact]
    public void FindIssues_MultipleIssues_ReturnsAll()
    {
        var lines = new[]
        {
            "add rax, rbx",  // RBX undefined
            "mov rax, 10",
            "mov rax, 10",   // Redundant
            "add rcx, 0"     // Redundant
        };
        var issues = _provider.FindIssues(lines);

        issues.Should().HaveCountGreaterOrEqualTo(3);
    }

    #endregion
}
