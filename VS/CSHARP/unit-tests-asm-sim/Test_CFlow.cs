using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using AsmSim;
using System.Collections.Generic;

namespace unit_tests_asm_z3
{
	[TestClass]
	public class Test_StaticFlow
	{
        const bool logToDisplay = TestTools.LOG_TO_DISPLAY;

        [TestMethod]
		public void Test_StaticFlow_Forward_1() {

            string programStr =
                "           jz      label1                      " + Environment.NewLine +
                "           mov     rax,        10              " + Environment.NewLine +
                "           jmp     label2                      " + Environment.NewLine +
                "label1:                                        " + Environment.NewLine +
                "           mov     rax,        20              " + Environment.NewLine +
                "label2:                                        " + Environment.NewLine +
                "           mov     rbx,        rax             " + Environment.NewLine +
                "           jz      label3                      " + Environment.NewLine +
                "label3:                                        ";

            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            Assert.AreEqual(10, flow.NLines);
            Assert.AreEqual((1, 3), flow.Get_Next_LineNumber(0));
            Assert.AreEqual((2, -1), flow.Get_Next_LineNumber(1));
            Assert.AreEqual((-1, 5), flow.Get_Next_LineNumber(2));
            Assert.AreEqual((4, -1), flow.Get_Next_LineNumber(3));
            Assert.AreEqual((5, -1), flow.Get_Next_LineNumber(4));
            Assert.AreEqual((6, -1), flow.Get_Next_LineNumber(5));
            Assert.AreEqual((7, -1), flow.Get_Next_LineNumber(6));
            Assert.AreEqual((8, 8), flow.Get_Next_LineNumber(7));
            Assert.AreEqual((9, -1), flow.Get_Next_LineNumber(8));

            var p0 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(0));
            var p1 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(1));
            var p2 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(2));
            var p3 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(3));
            var p4 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(4));
            var p5 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(5));
            var p6 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(6));
            var p7 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(7));
            var p8 = new List<(int LineNumber, bool IsBranch)>(flow.Get_Prev_LineNumber(8));

            Assert.AreEqual(0, p0.Count);
            Assert.AreEqual(1, p1.Count);
            Assert.AreEqual(1, p2.Count);
            Assert.AreEqual(1, p3.Count);
            Assert.AreEqual(1, p4.Count);
            Assert.AreEqual(2, p5.Count);
            Assert.AreEqual(1, p6.Count);
            Assert.AreEqual(1, p7.Count);
            Assert.AreEqual(2, p8.Count);

            Assert.AreEqual((0, false), p1[0]);
            Assert.AreEqual((1, false), p2[0]);
            Assert.AreEqual((0, true), p3[0]);
            Assert.AreEqual((3, false), p4[0]);
            Assert.AreEqual((2, true), p5[0]);
            Assert.AreEqual((4, false), p5[1]);
            Assert.AreEqual((5, false), p6[0]);
            Assert.AreEqual((6, false), p7[0]);
            Assert.AreEqual((7, false), p8[0]);
            Assert.AreEqual((7, true), p8[1]);
        }

        [TestMethod]
        public void Test_StaticFlow_IsBranchPoint_1()
        {
            string programStr =
                "           mov     rax,        10     ;line 0         " + Environment.NewLine +
                "label1:                               ;line 1         " + Environment.NewLine +
                "           mov     rbx,        1      ;line 2         " + Environment.NewLine +
                "           dec     rax                ;line 3         " + Environment.NewLine +
                "           jnz     label1             ;line 4         " + Environment.NewLine +
                "           mov     rcx,        1      ;line 5         ";
            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            Assert.IsFalse(flow.Is_Branch_Point(0));
            Assert.IsFalse(flow.Is_Branch_Point(1));
            Assert.IsFalse(flow.Is_Branch_Point(2));
            Assert.IsFalse(flow.Is_Branch_Point(3));
            Assert.IsTrue(flow.Is_Branch_Point(4));
            Assert.IsFalse(flow.Is_Branch_Point(5));
        }

        [TestMethod]
        public void Test_StaticFlow_IsMergePoint_1()
        {
            string programStr =
               "           mov     rax,        10     ;line 0         " + Environment.NewLine +
               "label1:                               ;line 1         " + Environment.NewLine +
               "           mov     rbx,        1      ;line 2         " + Environment.NewLine +
               "           dec     rax                ;line 3         " + Environment.NewLine +
               "           jnz     label1             ;line 4         " + Environment.NewLine +
               "           mov     rcx,        1      ;line 5         ";
            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            Assert.IsFalse(flow.Is_Merge_Point(0));
            Assert.IsTrue(flow.Is_Merge_Point(1));
            Assert.IsFalse(flow.Is_Merge_Point(2));
            Assert.IsFalse(flow.Is_Merge_Point(3));
            Assert.IsFalse(flow.Is_Merge_Point(4));
            Assert.IsFalse(flow.Is_Merge_Point(5));
        }

        [TestMethod]
        public void Test_StaticFlow_FutureLineNumbers_1()
        {
            string programStr =
                "           mov     rax,        10     ;line 0         " + Environment.NewLine +
                "label1:                               ;line 1         " + Environment.NewLine +
                "           mov     rbx,        1      ;line 2         " + Environment.NewLine +
                "           dec     rax                ;line 3         " + Environment.NewLine +
                "           jnz     label1             ;line 4         " + Environment.NewLine +
                "           mov     rcx,        1      ;line 5         ";

            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            var v = flow.FutureLineNumbers(1);
            Console.WriteLine("Number of elements: " + v.Count + ": " + String.Join(",", v));

            Assert.IsFalse(v.Contains(0));
            Assert.IsTrue(v.Contains(1));
            Assert.IsTrue(v.Contains(2));
            Assert.IsTrue(v.Contains(3));
            Assert.IsTrue(v.Contains(4));
            Assert.IsTrue(v.Contains(5));
        }

        [TestMethod]
        public void Test_StaticFlow_HasCodePath_1()
        {
            string programStr =
                "           mov     rax,        10     ;line 0         " + Environment.NewLine +
                "label1:                               ;line 1         " + Environment.NewLine +
                "           mov     rbx,        1      ;line 2         " + Environment.NewLine +
                "           dec     rax                ;line 3         " + Environment.NewLine +
                "           jnz     label1             ;line 4         " + Environment.NewLine +
                "           mov     rcx,        1      ;line 5         ";

            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            Assert.IsTrue(flow.HasCodePath(1, 4));
            Assert.IsFalse(flow.HasCodePath(5, 4));

            Assert.IsFalse(flow.HasCodePath(1, 0));
        }

        [TestMethod]
        public void Test_StaticFlow_IsLoopBranchPoint_1()
        {
            string programStr =
                "           mov     rax,        10     ;line 0         " + Environment.NewLine +
                "label1:                               ;line 1         " + Environment.NewLine +
                "           mov     rbx,        1      ;line 2         " + Environment.NewLine +
                "           dec     rax                ;line 3         " + Environment.NewLine +
                "           jnz     label1             ;line 4         " + Environment.NewLine +
                "           mov     rcx,        1      ;line 5         ";

            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            {
                var v = flow.Is_Loop_Branch_Point(0);
                Assert.IsFalse(v.IsLoopBranchPoint);
            }
            {
                var v = flow.Is_Loop_Branch_Point(1);
                Assert.IsFalse(v.IsLoopBranchPoint);
            }
            {
                var v = flow.Is_Loop_Branch_Point(2);
                Assert.IsFalse(v.IsLoopBranchPoint);
            }
            {
                var v = flow.Is_Loop_Branch_Point(3);
                Assert.IsFalse(v.IsLoopBranchPoint);
            }
            {
                var v = flow.Is_Loop_Branch_Point(4);
                Assert.IsTrue(v.IsLoopBranchPoint);
                Assert.IsFalse(v.BranchToExitLoop);
            }
            {
                var v = flow.Is_Loop_Branch_Point(5);
                Assert.IsFalse(v.IsLoopBranchPoint);
            }
        }

        [TestMethod]
        public void Test_StaticFlow_IsLoopMergePoint_1()
        {
            string programStr =
                "           mov     rax,        10     ;line 0         " + Environment.NewLine +
                "label1:                               ;line 1         " + Environment.NewLine +
                "           mov     rbx,        1      ;line 2         " + Environment.NewLine +
                "           dec     rax                ;line 3         " + Environment.NewLine +
                "           jnz     label1             ;line 4         " + Environment.NewLine +
                "           mov     rcx,        1      ;line 5         ";

            StaticFlow flow = new StaticFlow(programStr, new Tools());
            if (logToDisplay) Console.WriteLine(flow);

            {
                var v = flow.Is_Loop_Merge_Point(0);
                Assert.IsFalse(v.IsLoopMergePoint);
            }
            {
                var v = flow.Is_Loop_Merge_Point(1);
                Assert.IsTrue(v.IsLoopMergePoint);
                //Assert.IsFalse(v.BranchToExitLoop);
            }
            {
                var v = flow.Is_Loop_Merge_Point(2);
                Assert.IsFalse(v.IsLoopMergePoint);
            }
            {
                var v = flow.Is_Loop_Merge_Point(3);
                Assert.IsFalse(v.IsLoopMergePoint);
            }
            {
                var v = flow.Is_Loop_Merge_Point(4);
                Assert.IsFalse(v.IsLoopMergePoint);
            }
            {
                var v = flow.Is_Loop_Merge_Point(5);
                Assert.IsFalse(v.IsLoopMergePoint);
            }
        }
    }
}
