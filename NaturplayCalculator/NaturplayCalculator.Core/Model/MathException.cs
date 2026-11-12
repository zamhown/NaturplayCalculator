using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturplayCalculator.Model
{
    public class MathException : Exception
    {
        public MathException(string msg) : base(msg) { }
        public MathException(string msg, Exception innerException) : base(msg, innerException) { }
    }
}
