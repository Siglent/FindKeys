using System;
using System.Collections.Generic;
using System.Text;

namespace FindKeys
{
    public static class Extensions
    {
        public static Boolean isMixedCase(this String s)
        {
            return !(s == s.ToUpper());
        }

        public static Boolean isLowerCase(this String s)
        {
            return (s == s.ToLower());
        }

        public static Boolean isUpperCase(this String s)
        {
            return (s == s.ToUpper());
        }

        public static Boolean isNumeric(this String s)
        {
            foreach (Char c in s)
                if (!"0123456789".Contains(c))
                    return false;
            return true;
        }
    }
}
