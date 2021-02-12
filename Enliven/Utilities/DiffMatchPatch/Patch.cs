using System;
using System.Collections.Generic;
using System.Text;

namespace Bot.Utilities.DiffMatchPatch {
    /**
     * Class representing one patch operation.
     */
    public class Patch {
        public readonly List<Diff> Diffs = new List<Diff>();
        public int Start1;
        public int Start2;
        public int Length1;
        public int Length2;

        /**
         * Emulate GNU diff's format.
         * Header: @@ -382,8 +481,9 @@
         * Indices are printed as 1-based, not 0-based.
         * @return The GNU diff string.
         */
        public override string ToString() {
            string coords1, coords2;
            if (Length1 == 0) {
                coords1 = Start1 + ",0";
            }
            else if (Length1 == 1) {
                coords1 = Convert.ToString(Start1 + 1);
            }
            else {
                coords1 = (Start1 + 1) + "," + Length1;
            }

            if (Length2 == 0) {
                coords2 = Start2 + ",0";
            }
            else if (Length2 == 1) {
                coords2 = Convert.ToString(Start2 + 1);
            }
            else {
                coords2 = (Start2 + 1) + "," + Length2;
            }

            var text = new StringBuilder();
            text.Append("@@ -")
                .Append(coords1)
                .Append(" +")
                .Append(coords2)
                .Append(" @@\n");
            // Escape the body of the patch with %xx notation.
            foreach (var aDiff in Diffs) {
                switch (aDiff.Operation) {
                    case Operation.Insert:
                        text.Append('+');
                        break;
                    case Operation.Delete:
                        text.Append('-');
                        break;
                    case Operation.Equal:
                        text.Append(' ');
                        break;
                }

                text.Append(aDiff.Text.Replace("\n", @"!⏎!")).Append("\n");
            }

            return text.ToString();
        }
    }
}