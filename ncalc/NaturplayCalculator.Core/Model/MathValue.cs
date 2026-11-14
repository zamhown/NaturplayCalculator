using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturplayCalculator.Model
{
    public class MathValue
    {
        private Matrix mtr;
        private Complex cmx;
        private InputString val = new InputString("");
        public Matrix Mtr
        {
            get { return mtr; }
            set { mtr = value ?? throw new ArgumentNullException(nameof(value)); cmx = null; isNull = false; isMatrix = true; }
        }
        public Complex Cmx
        {
            get
            {
                if (cmx == null) throw new MathException(CalculationResources.GetString("error_3"));
                return cmx;
            }
        }
        // Source tokens retain indices for natural notation; arithmetic uses numeric fields.
        public InputString Value
        {
            get { return isMatrix ? new InputString(mtr.ToString()) : val; }
            set
            {
                if (value == null || value.Count == 0) throw new FormatException();
                string text = value.ToString();
                bool symbol = text == "e" || text == "π" || text == "☺" || text == "?" || text == "T"
                    || text == "-e" || text == "-π" || text == "-☺" || text == "-?" || text == "-T";
                cmx = symbol ? null : Complex.Parse(text);
                if (cmx != null) { NumericText.RequireFinite(cmx.Re); NumericText.RequireFinite(cmx.Im); }
                val = value; isNull = false; isMatrix = false; mtr = null;
            }
        }
        private bool isMatrix;
        public bool IsMatrix => isMatrix;
        private bool isNull = true;
        public bool IsNull => isNull;
        public bool IsZero => !isNull && !isMatrix && cmx != null && cmx.Re == 0 && cmx.Im == 0;
        public static readonly double PI = Math.PI;
        public static readonly double E = Math.E;
        public MathValue(InputString value) { Value = value; }
        public MathValue(double value) : this(new Complex(value)) { }
        public MathValue(Complex value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            NumericText.RequireFinite(value.Re); NumericText.RequireFinite(value.Im);
            cmx = new Complex(value); val = new InputString(cmx.ToString()); isNull = false;
        }
        public MathValue(Matrix value) { Mtr = value; }
        public MathValue() { }
        public MathValue Clone()
        {
            if (IsNull) return new MathValue();
            if (IsMatrix) return new MathValue(new Matrix(Mtr));
            if (cmx != null) { var copy = new MathValue(cmx); copy.val.Copy(val); return copy; }
            var token = new InputString(""); token.Copy(val); return new MathValue(token);
        }

        public static MathValue operator +(MathValue a, MathValue b)
        {
            if (a.IsNull && b.IsNull) throw new MathException(CalculationResources.GetString("error_22"));
            if (a.IsNull && !b.IsNull)
                return b;
            else if (!a.IsNull && b.IsNull)
                return a;

            if (a.IsMatrix && b.IsMatrix)
            {
                var mtx = Matrix.plus(a.Mtr, b.Mtr, false);
                return new MathValue(mtx);
            }
            else if ((!a.IsMatrix) && (!b.IsMatrix))
            {
                return new MathValue(a.cmx + b.cmx);
            }
            else if (a.IsMatrix && a.Mtr.n == a.Mtr.m)
            {
                var mtx = Matrix.plus(a.Mtr, Matrix.qiuE(a.Mtr.m, b), false);
                return new MathValue(mtx);
            }
            else if (b.IsMatrix && b.Mtr.n == b.Mtr.m)
            {
                var mtx = Matrix.plus(b.Mtr, Matrix.qiuE(b.Mtr.m, a), false);
                return new MathValue(mtx);
            }
            else
                throw new MathException(CalculationResources.GetString("error_34"));
            //throw new MathException(CalculationResources.GetString("error_23"));
        }

        public static MathValue operator -(MathValue a, MathValue b)
        {
            if (a.IsNull && b.IsNull) throw new MathException(CalculationResources.GetString("error_22"));
            if (a.IsNull && !b.IsNull)
            {
                if (b.IsMatrix)
                    return new MathValue(Matrix.numMultiply(b.Mtr, new Complex(-1), false));
                else
                    return new MathValue(0) - b;
            }
            else if (!a.IsNull && b.IsNull)
                return a;

            if (a.IsMatrix && b.IsMatrix)
            {
                var mtx = Matrix.plus(a.Mtr, Matrix.numMultiply(b.Mtr, new Complex(-1), false), false);
                return new MathValue(mtx);
            }
            else if ((!a.IsMatrix) && (!b.IsMatrix))
            {
                return new MathValue(a.cmx - b.cmx);
            }
            else if (a.IsMatrix && a.Mtr.n == a.Mtr.m)
            {
                var mtx = Matrix.plus(a.Mtr, Matrix.numMultiply(Matrix.qiuE(a.Mtr.m, b), new Complex(-1), false), false);
                return new MathValue(mtx);
            }
            else if (b.IsMatrix && b.Mtr.n == b.Mtr.m)
            {
                var mtx = Matrix.plus(Matrix.numMultiply(b.Mtr, new Complex(-1), false), Matrix.qiuE(b.Mtr.m, a), false);
                return new MathValue(mtx);
            }
            else
                throw new MathException(CalculationResources.GetString("error_34"));
            //throw new MathException(CalculationResources.GetString("error_24"));
        }

        public static MathValue operator *(MathValue a, MathValue b)
        {
            if (a.IsNull && b.IsNull) throw new MathException(CalculationResources.GetString("error_22"));
            if (a.IsNull && !b.IsNull)
                return b;
            else if (!a.IsNull && b.IsNull)
                return a;

            if (a.IsMatrix && b.IsMatrix)
            {
                var mtx = Matrix.multiply(a.Mtr, b.Mtr, false);
                if (mtx.m == 1 && mtx.n == 1)
                    return mtx[0][0].Value;
                else
                    return new MathValue(mtx);
            }
            else if ((!a.IsMatrix) && (!b.IsMatrix))
            {
                return new MathValue(a.cmx * b.cmx);
            }
            else
            {
                if (a.IsMatrix)
                {
                    return new MathValue(Matrix.numMultiply(a.Mtr, b.cmx, false));
                }
                else
                {
                    return new MathValue(Matrix.numMultiply(b.Mtr, a.cmx, false));
                }
            }
        }

        public static MathValue operator /(MathValue a, MathValue b)
        {
            if (a.IsNull || b.IsNull) throw new MathException(CalculationResources.GetString("error_22"));

            if (a.IsMatrix && b.IsMatrix)
            {
                throw new MathException(CalculationResources.GetString("error_25"));
            }
            else if ((!a.IsMatrix) && (!b.IsMatrix))
            {
                return new MathValue(a.cmx / b.cmx);
            }
            else
            {
                if (a.IsMatrix)
                {
                    return new MathValue(Matrix.numMultiply(a.Mtr, new Complex(1) / b.cmx, false));
                }
                else
                    throw new MathException(CalculationResources.GetString("error_26"));
            }
        }

        public static MathValue Pow(MathValue a, MathValue b)
        {
            if (a.IsNull || b.IsNull || b.IsMatrix) throw new MathException(CalculationResources.GetString("error_27"));
            if (b.Value.Equals("T")) return a.IsMatrix ? new MathValue(Matrix.qiuT(a.Mtr, false)) : a.Clone();
            if (a.IsMatrix)
            {
                if (b.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_29"));
                int exponent = Combinatorics.Integer(b.Cmx.Re, -1000000, 1000000);
                if (a.Mtr.n != a.Mtr.m) throw new MathException(CalculationResources.GetString("error_34"));
                var factor = exponent < 0 ? Matrix.qiuNi(a.Mtr, false) : new Matrix(a.Mtr);
                var result = Matrix.qiuE(a.Mtr.n);
                int count = Math.Abs(exponent);
                while (count > 0)
                {
                    CalculationContext.Check();
                    if ((count & 1) != 0) result = Matrix.multiply(result, factor, false);
                    count >>= 1;
                    if (count > 0) factor = Matrix.multiply(factor, factor, false);
                }
                return new MathValue(result);
            }
            if (b.Cmx.Im == 0)
            {
                if (a.Cmx.Im == 0 && (a.Cmx.Re >= 0 || b.Cmx.Re == Math.Truncate(b.Cmx.Re)))
                    return new MathValue(Math.Pow(a.Cmx.Re, b.Cmx.Re));
                return new MathValue(Complex.powcc(a.Cmx, b.Cmx.Re));
            }
            if (a.IsZero) throw new MathException(CalculationResources.GetString("error_28"));
            return new MathValue(Complex.From(System.Numerics.Complex.Pow(a.Cmx.Number, b.Cmx.Number)));
        }

        public static MathValue Ex(MathValue value, bool callEvent = true) => Statistics.Mean(value, callEvent);
        public static MathValue Dx(MathValue value, bool callEvent = true) => Statistics.Variance(value, callEvent);
        public static MathValue Cov(MathValue value, bool callEvent = true) => Statistics.Covariance(value, callEvent, false);
        public static MathValue Rho(MathValue value, bool callEvent = true) => Statistics.Covariance(value, callEvent, true);

        public static string DoubleToString(double value) => NumericText.Format(value);
        public override string ToString() => IsMatrix ? mtr.ToString() : val.ToString();
    }
}
