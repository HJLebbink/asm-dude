using System;

namespace AsmTools {

    public class Operand {

        private readonly string _str;
        private readonly Ot _type;
        private readonly Rn _rn;
        private readonly ulong _imm;
        private int _nBits;
        private readonly Tuple<Rn, Rn, int, long> _mem;

        public Operand(string token) {
            this._str = token;

            Tuple<bool, Rn, int> t0 = RegisterTools.toRn(token);
            if (t0.Item1) {
                this._type = Ot.reg;
                this._rn = t0.Item2;
                this._nBits = t0.Item3;
            } else {
                Tuple<bool, ulong, int> t1 = AsmSourceTools.toConstant(token);
                if (t1.Item1) {
                    this._type = Ot.imm;
                    this._imm = t1.Item2;
                    this._nBits = t1.Item3;
                } else {
                    Tuple<bool, Rn, Rn, int, long, int> t2 = AsmSourceTools.parseMemOperand(token);
                    if (t2.Item1) {
                        this._type = Ot.mem;
                        this._mem = new Tuple<Rn, Rn, int, long>(t2.Item2, t2.Item3, t2.Item4, t2.Item5);
                        this._nBits = t2.Item6;
                    } else {
                        this._type = Ot.UNKNOWN;
                        this._nBits = -1;
                    }
                }
            }
        }

        public Ot type { get { return _type; } }
        public bool isReg { get { return _type == Ot.reg; } }
        public bool isMem { get { return _type == Ot.mem; } }
        public bool isImm { get { return _type == Ot.imm; } }

        public Rn rn { get { return _rn; } }
        public ulong imm { get { return _imm; } }
        /// <summary>
        /// Return tuple with BaseReg, IndexReg, Scale and Displacement. Offset = Base + (Index * Scale) + Displacement
        /// </summary>
        public Tuple<Rn, Rn, int, long> mem { get { return _mem; } }
        public int nBits { get { return _nBits; } set { _nBits = value; } }

        public override string ToString() {
            return this._str;
        }
    }
}
