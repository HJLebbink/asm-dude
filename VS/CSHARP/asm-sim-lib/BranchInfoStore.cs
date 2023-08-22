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

namespace AsmSim
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Contracts;
    using System.Text;
    using Microsoft.Z3;

    public class BranchInfoStore
    {
        #region Fields
        private readonly Context ctx_;
        private IDictionary<string, BranchInfo> branchInfo_;
        #endregion

        #region Constructors
        public BranchInfoStore(Context ctx)
        {
            this.ctx_ = ctx;
        }
        #endregion

        #region Getters
        public int Count { get { return (this.branchInfo_ == null) ? 0 : this.branchInfo_.Count; } }

        public IEnumerable<BranchInfo> Values
        {
            get
            {
                if (this.branchInfo_ == null)
                {
                    yield break;
                }
                else
                {
                    foreach (BranchInfo e in this.branchInfo_.Values)
                    {
                        yield return e;
                    }
                }
            }
        }

        #endregion

        #region Setters
        public void Clear()
        {
            this.branchInfo_?.Clear();
        }
        #endregion

        public static BranchInfoStore RetrieveSharedBranchInfo(
            BranchInfoStore store1, BranchInfoStore store2, Context ctx)
        {
            if (store1 == null)
            {
                return store2;
            }

            if (store2 == null)
            {
                return store1;
            }

            IList<string> sharedKeys = new List<string>();

            if ((store1.branchInfo_ != null) && (store2.branchInfo_ != null))
            {
                ICollection<string> keys1 = store1.branchInfo_.Keys;
                ICollection<string> keys2 = store2.branchInfo_.Keys;

                foreach (string key in keys1)
                {
                    if (keys2.Contains(key))
                    {
                        if (store1.branchInfo_[key].BranchTaken != store2.branchInfo_[key].BranchTaken)
                        {
                            sharedKeys.Add(key);
                        }
                        else
                        {
                            //Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: key " + key + " is shared but has equal branchTaken " + state1.BranchInfo[key].branchTaken);
                        }
                    }
                }
                Debug.Assert(sharedKeys.Count <= 1);

                if (false)
                {
                    foreach (string key in keys1)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: Keys of state1 " + key);
                    }
                    foreach (string key in keys2)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: Keys of state2 " + key);
                    }
                    foreach (string key in sharedKeys)
                    {
                        Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: sharedKey " + key);
                    }
                }
            }

            BranchInfoStore mergeBranchStore = new BranchInfoStore(ctx);
            if (sharedKeys.Count == 0)
            {
                if (store1.branchInfo_ != null)
                {
                    foreach (KeyValuePair<string, BranchInfo> element in store1.branchInfo_)
                    {
                        mergeBranchStore.Add(element.Value, true);
                    }
                }
                if (store2.branchInfo_ != null)
                {
                    foreach (KeyValuePair<string, BranchInfo> element in store2.branchInfo_)
                    {
                        if (!mergeBranchStore.ContainsKey(element.Key))
                        {
                            mergeBranchStore.Add(element.Value, true);
                        }
                    }
                }
                //if (!tools.Quiet) Console.WriteLine("INFO: State:RetrieveSharedBranchInfo: the two provided states do not share a branching point. This would happen in loops.");
            }
            else
            {
                //only use the first sharedKey

                string key = sharedKeys[0];
                foreach (KeyValuePair<string, BranchInfo> element in store1.branchInfo_)
                {
                    if (!element.Key.Equals(key, StringComparison.Ordinal))
                    {
                        mergeBranchStore.Add(element.Value, true);
                    }
                }
                foreach (KeyValuePair<string, BranchInfo> element in store2.branchInfo_)
                {
                    if (!element.Key.Equals(key, StringComparison.Ordinal))
                    {
                        if (!mergeBranchStore.ContainsKey(element.Key))
                        {
                            mergeBranchStore.Add(element.Value, true);
                        }
                    }
                }
            }

            return mergeBranchStore;
        }

        public bool ContainsKey(string key)
        {
            return (this.branchInfo_ == null) ? false : this.branchInfo_.ContainsKey(key);
        }

        public void RemoveKey(string branchInfoKey)
        {
            this.branchInfo_.Remove(branchInfoKey);
        }

        public void Remove(BranchInfo branchInfo)
        {
            Contract.Requires(branchInfo != null);
            this.branchInfo_.Remove(branchInfo.Key);
        }

        public void Add(BranchInfo branchInfo, bool translate)
        {
            if (branchInfo == null)
            {
                return;
            }

            if (this.branchInfo_ == null)
            {
                this.branchInfo_ = new Dictionary<string, BranchInfo>();
            }

            if (!this.branchInfo_.ContainsKey(branchInfo.Key))
            {
                //Console.WriteLine("INFO: AddBranchInfo: key=" + branchInfo.key);
                if (translate)
                {
                    this.branchInfo_.Add(branchInfo.Key, branchInfo.Translate(this.ctx_));
                }
                else
                {
                    this.branchInfo_.Add(branchInfo.Key, branchInfo);
                }
            }
        }

        public void Add(IDictionary<string, BranchInfo> branchInfo, bool translate)
        {
            if (branchInfo == null)
            {
                return;
            }

            foreach (KeyValuePair<string, BranchInfo> b in branchInfo)
            {
                this.Add(b.Value, translate);
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();
            if ((this.branchInfo_ != null) && (this.branchInfo_.Count > 0))
            {
                sb.AppendLine("Branch control flow constraints:");
                int i = 0;
                foreach (KeyValuePair<string, BranchInfo> entry in this.branchInfo_)
                {
                    BoolExpr e = entry.Value.GetData(this.ctx_);
                    sb.AppendLine(string.Format("   {0}: {1}", i, ToolsZ3.ToString(e)));
                    i++;
                }
            }
            return sb.ToString();
        }
    }
}
