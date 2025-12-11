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
    using System.Windows.Controls;
    using Xunit;
    using AsmDude3;

    /// <summary>
    /// Integration tests for AsmDudeOptionsPageUI - tests UI element access patterns.
    /// These tests verify that GetPropValue/SetPropValue work correctly with XAML elements.
    /// </summary>
    public class OptionsPageUITests
    {
        /// <summary>
        /// Test that UI can be created without errors.
        /// </summary>
        [Fact]
        public void TestUIInitialization()
        {
            // Arrange & Act
            var ui = new AsmDudeOptionsPageUI();

            // Assert
            Assert.NotNull(ui);
        }

        /// <summary>
        /// Test that FindName can locate XAML elements by name.
        /// </summary>
        [Fact]
        public void TestFindNameLocatesXAMLElements()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act - Try to find known elements by name
            object? versionLabel = ui.FindName("version_UI");
            object? globalMaxLines = ui.FindName("Global_MaxFileLines_UI");
            object? syntaxHighlightingCheckBox = ui.FindName("SyntaxHighlighting_On_UI");

            // Assert - All elements should be found
            Assert.NotNull(versionLabel);
            Assert.NotNull(globalMaxLines);
            Assert.NotNull(syntaxHighlightingCheckBox);
        }

        /// <summary>
        /// Test that GetPropValue correctly retrieves CheckBox values using FindName.
        /// </summary>
        [Fact]
        public void TestGetPropValue_CheckBox()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Get the actual CheckBox element to set its state
            var checkBox = ui.FindName("SyntaxHighlighting_On_UI") as CheckBox;
            Assert.NotNull(checkBox);

            // Act - Get value when CheckBox is checked
            checkBox.IsChecked = true;
            var valueChecked = ui.GetPropValue("SyntaxHighlighting_On");

            // Assert
            Assert.Equal(true, valueChecked);

            // Act - Get value when CheckBox is unchecked
            checkBox.IsChecked = false;
            var valueUnchecked = ui.GetPropValue("SyntaxHighlighting_On");

            // Assert
            Assert.Equal(false, valueUnchecked);
        }

        /// <summary>
        /// Test that SetPropValue correctly sets CheckBox values using FindName.
        /// </summary>
        [Fact]
        public void TestSetPropValue_CheckBox()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            var checkBox = ui.FindName("SyntaxHighlighting_On_UI") as CheckBox;
            Assert.NotNull(checkBox);

            // Act - Set to true
            ui.SetPropValue("SyntaxHighlighting_On", true);

            // Assert
            Assert.True(checkBox.IsChecked);

            // Act - Set to false
            ui.SetPropValue("SyntaxHighlighting_On", false);

            // Assert
            Assert.False(checkBox.IsChecked);
        }

        /// <summary>
        /// Test that GetPropValue correctly retrieves TextBox values.
        /// </summary>
        [Fact]
        public void TestGetPropValue_TextBox()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            var textBox = ui.FindName("AsmDoc_Url_UI") as TextBox;
            Assert.NotNull(textBox);

            // Act - Set text and retrieve
            const string testUrl = "https://test.example.com";
            textBox.Text = testUrl;
            var retrievedValue = ui.GetPropValue("AsmDoc_Url");

            // Assert
            Assert.Equal(testUrl, retrievedValue);
        }

        /// <summary>
        /// Test that SetPropValue correctly sets TextBox values.
        /// </summary>
        [Fact]
        public void TestSetPropValue_TextBox()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            var textBox = ui.FindName("AsmDoc_Url_UI") as TextBox;
            Assert.NotNull(textBox);
            const string testUrl = "https://example.com";

            // Act
            ui.SetPropValue("AsmDoc_Url", testUrl);

            // Assert
            Assert.Equal(testUrl, textBox.Text);
        }

        /// <summary>
        /// Test that multiple property get/set operations work correctly.
        /// </summary>
        [Fact]
        public void TestMultiplePropertyOperations()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert - Set multiple properties
            ui.SetPropValue("SyntaxHighlighting_On", true);
            ui.SetPropValue("CodeFolding_On", true);
            ui.SetPropValue("AsmDoc_On", true);

            Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_On"));
            Assert.True((bool)ui.GetPropValue("CodeFolding_On"));
            Assert.True((bool)ui.GetPropValue("AsmDoc_On"));

            // Act & Assert - Change them
            ui.SetPropValue("SyntaxHighlighting_On", false);
            ui.SetPropValue("CodeFolding_On", false);

            Assert.False((bool)ui.GetPropValue("SyntaxHighlighting_On"));
            Assert.False((bool)ui.GetPropValue("CodeFolding_On"));
            Assert.True((bool)ui.GetPropValue("AsmDoc_On")); // Should remain unchanged
        }

        /// <summary>
        /// Test that getting undefined properties returns appropriate defaults.
        /// </summary>
        [Fact]
        public void TestGetPropValue_UndefinedProperty_ReturnsDefault()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act - Try to get a property that doesn't exist in UI
            var result = ui.GetPropValue("NonExistentProperty_On");

            // Assert - Should return false (default for _On properties)
            Assert.Equal(false, result);
        }

        /// <summary>
        /// Test that setting undefined properties gracefully fails silently.
        /// </summary>
        [Fact]
        public void TestSetPropValue_UndefinedProperty_DoesNotThrow()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert - Should not throw
            var ex = Record.Exception(() => ui.SetPropValue("NonExistentProperty_On", true));
            Assert.Null(ex);
        }

        /// <summary>
        /// Test that all syntax highlighting toggle checkboxes work.
        /// </summary>
        [Fact]
        public void TestSyntaxHighlightingCheckboxes()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Test the main syntax highlighting toggle
            ui.SetPropValue("SyntaxHighlighting_On", true);
            Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_On"));

            // Test italic toggles
            ui.SetPropValue("SyntaxHighlighting_Opcode_Italic", true);
            Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_Opcode_Italic"));

            ui.SetPropValue("SyntaxHighlighting_Register_Italic", false);
            Assert.False((bool)ui.GetPropValue("SyntaxHighlighting_Register_Italic"));

            ui.SetPropValue("SyntaxHighlighting_Remark_Italic", true);
            Assert.True((bool)ui.GetPropValue("SyntaxHighlighting_Remark_Italic"));
        }

        /// <summary>
        /// Test that performance info toggles work correctly.
        /// </summary>
        [Fact]
        public void TestPerformanceInfoToggles()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert
            ui.SetPropValue("PerformanceInfo_On", true);
            Assert.True((bool)ui.GetPropValue("PerformanceInfo_On"));

            ui.SetPropValue("PerformanceInfo_SkylakeX_On", true);
            Assert.True((bool)ui.GetPropValue("PerformanceInfo_SkylakeX_On"));

            ui.SetPropValue("PerformanceInfo_On", false);
            Assert.False((bool)ui.GetPropValue("PerformanceInfo_On"));
        }

        /// <summary>
        /// Test that IntelliSense feature toggles work correctly.
        /// </summary>
        [Fact]
        public void TestIntelliSenseToggles()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert
            ui.SetPropValue("IntelliSense_Label_Analysis_On", true);
            Assert.True((bool)ui.GetPropValue("IntelliSense_Label_Analysis_On"));

            ui.SetPropValue("IntelliSense_Show_Undefined_Labels", true);
            Assert.True((bool)ui.GetPropValue("IntelliSense_Show_Undefined_Labels"));

            ui.SetPropValue("IntelliSense_Show_Undefined_Includes", false);
            Assert.False((bool)ui.GetPropValue("IntelliSense_Show_Undefined_Includes"));
        }

        /// <summary>
        /// Test that code folding settings work correctly.
        /// </summary>
        [Fact]
        public void TestCodeFoldingSettings()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act & Assert - Toggle code folding
            ui.SetPropValue("CodeFolding_On", true);
            Assert.True((bool)ui.GetPropValue("CodeFolding_On"));

            // Set begin/end tags
            ui.SetPropValue("CodeFolding_BeginTag", "#region");
            Assert.Equal("#region", ui.GetPropValue("CodeFolding_BeginTag"));

            ui.SetPropValue("CodeFolding_EndTag", "#endregion");
            Assert.Equal("#endregion", ui.GetPropValue("CodeFolding_EndTag"));
        }

        /// <summary>
        /// Test that assembler selection radio buttons work.
        /// </summary>
        [Fact]
        public void TestAssemblerSelection()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act - Set assembler to AUTO_DETECT
            ui.UsedAssembler = AssemblerEnum.AUTO_DETECT;

            // Assert
            Assert.Equal(AssemblerEnum.AUTO_DETECT, ui.UsedAssembler);

            // Act - Set assembler to MASM
            ui.UsedAssembler = AssemblerEnum.MASM;

            // Assert
            Assert.Equal(AssemblerEnum.MASM, ui.UsedAssembler);

            // Act - Set assembler to NASM_INTEL
            ui.UsedAssembler = AssemblerEnum.NASM_INTEL;

            // Assert
            Assert.Equal(AssemblerEnum.NASM_INTEL, ui.UsedAssembler);
        }

        /// <summary>
        /// Test that color settings work correctly.
        /// </summary>
        [Fact]
        public void TestColorSettings()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            var colorMnemonic = System.Drawing.Color.FromArgb(255, 230, 230, 250); // Lavender

            // Act
            ui.SetPropValue("SyntaxHighlighting_Opcode", colorMnemonic);
            var retrievedColor = ui.GetPropValue("SyntaxHighlighting_Opcode");

            // Assert
            Assert.NotNull(retrievedColor);
            Assert.IsType<System.Drawing.Color>(retrievedColor);
            var retrievedDrawingColor = (System.Drawing.Color)retrievedColor;
            Assert.Equal(colorMnemonic.R, retrievedDrawingColor.R);
            Assert.Equal(colorMnemonic.G, retrievedDrawingColor.G);
            Assert.Equal(colorMnemonic.B, retrievedDrawingColor.B);
        }

        /// <summary>
        /// Test rapid property changes (stress test).
        /// </summary>
        [Fact]
        public void TestRapidPropertyChanges()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();
            bool expectedValue = true;

            // Act & Assert - Toggle 100 times
            for (int i = 0; i < 100; i++)
            {
                ui.SetPropValue("SyntaxHighlighting_On", expectedValue);
                var actualValue = (bool)ui.GetPropValue("SyntaxHighlighting_On");
                Assert.Equal(expectedValue, actualValue);
                expectedValue = !expectedValue;
            }
        }

        /// <summary>
        /// Test that default values are sensible when UI not fully initialized.
        /// </summary>
        [Fact]
        public void TestDefaultValues()
        {
            // Arrange
            var ui = new AsmDudeOptionsPageUI();

            // Act - Get values for properties that should have defined defaults
            var maxFileLines = ui.GetPropValue("Global_MaxFileLines");
            var architectureDefault = ui.GetPropValue("ARCH_8086");
            var italicDefault = ui.GetPropValue("SyntaxHighlighting_Opcode_Italic");

            // Assert - Defaults should be sensible
            Assert.IsType<int>(maxFileLines);
            Assert.Equal(50000, (int)maxFileLines);

            Assert.IsType<bool>(architectureDefault);
            Assert.Equal(false, architectureDefault);

            Assert.IsType<bool>(italicDefault);
            Assert.Equal(false, italicDefault);
        }
    }
}
