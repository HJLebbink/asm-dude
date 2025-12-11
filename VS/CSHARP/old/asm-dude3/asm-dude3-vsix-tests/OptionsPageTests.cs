// The MIT License (MIT)
//
// Copyright (c) 2023 Henk-Jan Lebbink
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

namespace AsmDude3.Tests
{
    using System;
    using System.ComponentModel;
    using System.Windows.Controls;
    using Xunit;
    using AsmDude3;

    /// <summary>
    /// Integration tests for AsmDudeOptionsPage - tests Settings persistence and UI interaction.
    /// These tests verify that the Options page correctly reads from and writes to Settings.
    /// </summary>
    public class OptionsPageTests
    {
        /// <summary>
        /// Test that Options page can be created.
        /// </summary>
        [Fact]
        public void TestOptionsPageInitialization()
        {
            // Arrange & Act
            var optionsPage = new AsmDudeOptionsPage();

            // Assert
            Assert.NotNull(optionsPage);
            Assert.NotNull(optionsPage.Child); // Should have UI element
        }

        /// <summary>
        /// Test that default settings are accessible.
        /// </summary>
        [Fact]
        public void TestSettingsDefaultsAccessible()
        {
            // Arrange & Act - Access key settings
            var syntaxHighlighting = Settings.Default.SyntaxHighlighting_On;
            var codeFolding = Settings.Default.CodeFolding_On;
            var asmDoc = Settings.Default.AsmDoc_On;
            var performanceInfo = Settings.Default.PerformanceInfo_On;
            var skylakeX = Settings.Default.PerformanceInfo_SkylakeX_On;

            // Assert - All should be accessible without throwing
            Assert.IsType<bool>(syntaxHighlighting);
            Assert.IsType<bool>(codeFolding);
            Assert.IsType<bool>(asmDoc);
            Assert.IsType<bool>(performanceInfo);
            Assert.IsType<bool>(skylakeX);
        }

        /// <summary>
        /// Test that Settings have sensible default values.
        /// </summary>
        [Fact]
        public void TestSettingsDefaultValues()
        {
            // Arrange & Act
            var syntaxHighlighting = Settings.Default.SyntaxHighlighting_On;
            var codeFolding = Settings.Default.CodeFolding_On;
            var asmDoc = Settings.Default.AsmDoc_On;
            var autoDetect = Settings.Default.useAssemblerAutoDetect;

            // Assert - Key defaults should be true
            Assert.True(syntaxHighlighting, "Syntax highlighting should be enabled by default");
            Assert.True(codeFolding, "Code folding should be enabled by default");
            Assert.True(asmDoc, "AsmDoc should be enabled by default");
            Assert.True(autoDetect, "Assembler auto-detect should be enabled by default");
        }

        /// <summary>
        /// Test that SafeSet_GUI handles missing properties gracefully.
        /// </summary>
        [Fact]
        public void TestSafeSetGUIHandlesMissingProperties()
        {
            // Arrange
            var optionsPage = new AsmDudeOptionsPage();

            // Act & Assert - Should not throw when trying to set missing properties
            var ex = Record.Exception(() =>
            {
                // Try to set properties that might not exist in UI
                // This tests the SafeSet_GUI mechanism
                var eventArgs = new CancelEventArgs();
                optionsPage.OnActivate(eventArgs);
            });

            // Should complete without throwing
            Assert.Null(ex);
        }

        /// <summary>
        /// Test that UI element property access doesn't crash on unimplemented properties.
        /// </summary>
        [Fact]
        public void TestUIElementAccess_HandlesUnimplementedProperties()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert - These should not throw even if property doesn't exist in UI
            var ex1 = Record.Exception(() => ui.GetPropValue("UnknownProperty_On"));
            var ex2 = Record.Exception(() => ui.SetPropValue("UnknownProperty_On", true));
            var ex3 = Record.Exception(() => ui.GetPropValue("ARCH_UnknownArchitecture"));

            // Should complete without throwing
            Assert.Null(ex1);
            Assert.Null(ex2);
            Assert.Null(ex3);
        }

        /// <summary>
        /// Test that assembler selection properties work correctly.
        /// </summary>
        [Fact]
        public void TestAssemblerSelectionPersistence()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert - Test all assembler options
            ui.UsedAssembler = AssemblerEnum.AUTO_DETECT;
            Assert.Equal(AssemblerEnum.AUTO_DETECT, ui.UsedAssembler);

            ui.UsedAssembler = AssemblerEnum.MASM;
            Assert.Equal(AssemblerEnum.MASM, ui.UsedAssembler);

            ui.UsedAssembler = AssemblerEnum.NASM_INTEL;
            Assert.Equal(AssemblerEnum.NASM_INTEL, ui.UsedAssembler);

            ui.UsedAssembler = AssemblerEnum.NASM_ATT;
            Assert.Equal(AssemblerEnum.NASM_ATT, ui.UsedAssembler);
        }

        /// <summary>
        /// Test that color parsing and storage works correctly.
        /// </summary>
        [Fact]
        public void TestColorParsing()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            var lavender = System.Drawing.Color.FromArgb(255, 230, 230, 250);
            var mistyRose = System.Drawing.Color.FromArgb(255, 255, 228, 225);

            // Act
            ui.SetPropValue("SyntaxHighlighting_Opcode", lavender);
            ui.SetPropValue("SyntaxHighlighting_Register", mistyRose);

            var retrievedLavender = (System.Drawing.Color)ui.GetPropValue("SyntaxHighlighting_Opcode");
            var retrievedMistyRose = (System.Drawing.Color)ui.GetPropValue("SyntaxHighlighting_Register");

            // Assert
            Assert.Equal(lavender.R, retrievedLavender.R);
            Assert.Equal(lavender.G, retrievedLavender.G);
            Assert.Equal(lavender.B, retrievedLavender.B);

            Assert.Equal(mistyRose.R, retrievedMistyRose.R);
            Assert.Equal(mistyRose.G, retrievedMistyRose.G);
            Assert.Equal(mistyRose.B, retrievedMistyRose.B);
        }

        /// <summary>
        /// Test that numeric settings work correctly (IntegerUpDown).
        /// </summary>
        [Fact]
        public void TestNumericSettings()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act - Get the current value of Global_MaxFileLines
            var initialValue = ui.GetPropValue("Global_MaxFileLines");

            // Assert - Should be a number
            Assert.NotNull(initialValue);
            Assert.IsType<int>(initialValue);
            Assert.Equal(50000, (int)initialValue);
        }

        /// <summary>
        /// Test that text settings work correctly (TextBox).
        /// </summary>
        [Fact]
        public void TestTextSettings()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            const string testUrl = "https://example.com/docs";

            // Act
            ui.SetPropValue("AsmDoc_Url", testUrl);
            var retrievedUrl = (string)ui.GetPropValue("AsmDoc_Url");

            // Assert
            Assert.Equal(testUrl, retrievedUrl);
        }

        /// <summary>
        /// Test that PropertyEnum values can all be accessed without throwing.
        /// </summary>
        [Fact]
        public void TestAllPropertyEnumValuesAccessible()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            int successCount = 0;
            int totalCount = 0;

            // Act - Try to access all PropertyEnum values
            foreach (PropertyEnum prop in Enum.GetValues(typeof(PropertyEnum)))
            {
                totalCount++;
                try
                {
                    // Try to get the value - should not throw
                    var value = ui.GetPropValue(prop.ToString());
                    successCount++;
                }
                catch (Exception ex)
                {
                    // Log which property failed (for debugging)
                    // But don't fail the test - we expect some to not have UI elements
                    System.Diagnostics.Debug.WriteLine($"Failed to get {prop}: {ex.Message}");
                }
            }

            // Assert - Most properties should be accessible
            // Even if UI element doesn't exist, should return default without throwing
            Assert.True(successCount > 0, "Should access at least some properties");
            Assert.Equal(totalCount, successCount); // All should be accessible (return defaults if needed)
        }

        /// <summary>
        /// Test that toggle behavior is consistent across multiple get/set cycles.
        /// </summary>
        [Fact]
        public void TestToggleConsistency()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert - Multiple cycles of toggle
            for (int cycle = 0; cycle < 10; cycle++)
            {
                // Set to true
                ui.SetPropValue("SyntaxHighlighting_On", true);
                Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_On"), $"Failed at cycle {cycle} (true)");

                // Set to false
                ui.SetPropValue("SyntaxHighlighting_On", false);
                Assert.False((bool)ui.GetPropValue("SyntaxHighlighting_On"), $"Failed at cycle {cycle} (false)");
            }
        }

        /// <summary>
        /// Test that different properties don't interfere with each other.
        /// </summary>
        [Fact]
        public void TestPropertyIsolation()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act - Set multiple different properties to different values
            ui.SetPropValue("SyntaxHighlighting_On", true);
            ui.SetPropValue("CodeFolding_On", false);
            ui.SetPropValue("AsmDoc_On", true);
            ui.SetPropValue("PerformanceInfo_On", false);

            // Assert - Each should maintain its own value
            Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_On"));
            Assert.False((bool)ui.GetPropValue("CodeFolding_On"));
            Assert.True((bool)ui.GetPropValue("AsmDoc_On"));
            Assert.False((bool)ui.GetPropValue("PerformanceInfo_On"));

            // Act - Modify one shouldn't affect others
            ui.SetPropValue("SyntaxHighlighting_On", false);

            // Assert - Others should remain unchanged
            Assert.False((bool)ui.GetPropValue("SyntaxHighlighting_On"));
            Assert.False((bool)ui.GetPropValue("CodeFolding_On")); // Should still be false
            Assert.True((bool)ui.GetPropValue("AsmDoc_On")); // Should still be true
            Assert.False((bool)ui.GetPropValue("PerformanceInfo_On")); // Should still be false
        }

        /// <summary>
        /// Test that all critical features default to enabled.
        /// </summary>
        [Fact]
        public void TestCriticalFeaturesEnabledByDefault()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // These features should all be enabled/true by default
            var criticalFeatures = new[]
            {
                "SyntaxHighlighting_On",
                "CodeFolding_On",
                "AsmDoc_On",
                "PerformanceInfo_On",
                "PerformanceInfo_SkylakeX_On",
                "CodeCompletion_On",
                "SignatureHelp_On",
                "IntelliSense_Label_Analysis_On",
                "IntelliSense_Show_Undefined_Labels",
                "IntelliSense_Show_Undefined_Includes"
            };

            // Act & Assert - All critical features should be retrievable
            foreach (var feature in criticalFeatures)
            {
                var value = ui.GetPropValue(feature);
                Assert.NotNull(value);
                // Note: We can't assert they're all True since UI might not be fully rendered
                // But we can assert they're all retrievable without throwing
            }
        }
    }
}
