using System;

namespace AsmDude.ErrorSquiggles {

    [Flags]
    public enum AsmErrorEnum {
        NONE = 0,
        LABEL_UNDEFINED = 1 << 1,
        LABEL_CLASH = 1 << 2,
        OTHER = 1 << 3,

        LABEL = LABEL_UNDEFINED | LABEL_CLASH
    }
}
