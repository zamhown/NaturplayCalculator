using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturplayCalculator.Model
{
    public class MathException : Exception
    {
        public int SourceStart { get; private set; } = -1;
        public int SourceLength { get; private set; }
        public MathException(string msg) : base(msg) { }
        public MathException(string msg, Exception innerException) : base(msg, innerException) { }
        internal MathException AtSource(int start, int length)
        {
            if (SourceStart < 0 && start >= 0) { SourceStart = start; SourceLength = Math.Max(0, length); }
            return this;
        }
    }
}
