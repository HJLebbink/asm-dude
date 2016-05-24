
namespace AsmTools {
    public enum AssemblerEnum {
        NASM,
        MASM,
        UNKNOWN
    }

    public static partial class AsmSourceTools {
        public static AssemblerEnum parseAssembler(string str) {
            switch (str.ToUpper()) {
                case "MASM": return AssemblerEnum.MASM;
                case "NASM": return AssemblerEnum.NASM;
            }
            return AssemblerEnum.UNKNOWN;
        }
    }


}
