using System;
using System.Collections.Generic;
using System.Linq;

namespace NaturplayCalculator.Model
{
    public partial class Element
    {
        public string ToCode()
        {
            if (!Value.IsNull)
            {
                string mc = value.Value.ToString();
                if (Value.IsMatrix)
                {
                    mc = "(";
                    for (int i = 0; i < Value.Mtr.n; i++)
                    {
                        for (int j = 0; j < Value.Mtr.m; j++)
                        {
                            mc += Value.Mtr[i][j].ToCode();
                            if (j < Value.Mtr.m - 1)
                                mc += ",";
                        }
                        if (i < Value.Mtr.n - 1)
                            mc += ",,";
                    }
                    mc += ")";
                }
                if (unitOperator == UnitaryOperator.sqrt)
                    codeString = "√(" + mc + ")";
                else if (unitOperator == UnitaryOperator.sin)
                    codeString = "sin(" + mc + ")";
                else if (unitOperator == UnitaryOperator.cos)
                    codeString = "cos(" + mc + ")";
                else if (unitOperator == UnitaryOperator.tan)
                    codeString = "tan(" + mc + ")";
                else if (unitOperator == UnitaryOperator.arcsin)
                    codeString = "arcsin(" + mc + ")";
                else if (unitOperator == UnitaryOperator.arccos)
                    codeString = "arccos(" + mc + ")";
                else if (unitOperator == UnitaryOperator.arctan)
                    codeString = "arctan(" + mc + ")";
                else if (unitOperator == UnitaryOperator.fact)
                    codeString = mc + "!";
                else if (unitOperator == UnitaryOperator.dfact)
                    codeString = mc + "!!";
                else if (unitOperator == UnitaryOperator.ln)
                    codeString = "ln(" + mc + ")";
                else if (unitOperator == UnitaryOperator.lg)
                    codeString = "lg(" + mc + ")";
                else if (unitOperator == UnitaryOperator.abs)
                    codeString = "Abs(" + mc + ")";
                else if (unitOperator == UnitaryOperator.e)
                    codeString = "E(" + mc + ")";
                else if (unitOperator == UnitaryOperator.d)
                    codeString = "D(" + mc + ")";
                else if (unitOperator == UnitaryOperator.sinh)
                    codeString = "sinh(" + mc + ")";
                else if (unitOperator == UnitaryOperator.cosh)
                    codeString = "cosh(" + mc + ")";
                else if (unitOperator == UnitaryOperator.tanh)
                    codeString = "tanh(" + mc + ")";
                else if (unitOperator == UnitaryOperator.arsh)
                    codeString = "arsh(" + mc + ")";
                else if (unitOperator == UnitaryOperator.arch)
                    codeString = "arch(" + mc + ")";
                else if (unitOperator == UnitaryOperator.arth)
                    codeString = "arth(" + mc + ")";
                else if (unitOperator == UnitaryOperator.cov)
                    codeString = "Cov(" + mc + ")";
                else if (unitOperator == UnitaryOperator.rho)
                    codeString = "rho(" + mc + ")";
                else if (unitOperator == UnitaryOperator.rank)
                    codeString = "rank(" + mc + ")";
                else if (unitOperator == UnitaryOperator.p)
                {
                    if (mc == "i" || mc == "π")
                        codeString = "π";
                    else if (mc == NumericText.Format(MathValue.PI))
                        codeString = mc;
                    else
                        throw new Exception(CalculationResources.GetString("error_13"));
                }
                else
                    codeString = mc;
                if (isMinus)
                {
                    codeString = "-" + codeString;
                }
                return codeString;
            }

            try
            {
                string s, t;
                if (children[0].children.Count <= 1)
                    s = children[0].ToCode();
                else
                    s = "(" + children[0].ToCode() + ")";
                for (int i = 0; i < binOperators.Count; i++)
                {
                    if (children[i + 1].children.Count <= 1)
                        t = children[i + 1].ToCode();
                    else
                        t = "(" + children[i + 1].ToCode() + ")";
                    s = s + " " + oToS(binOperators[i]) + " " + t;
                }
                if (unitOperator == UnitaryOperator.sqrt)
                    codeString = "√(" + s + ")";
                else if (unitOperator == UnitaryOperator.sin)
                    codeString = "sin(" + s + ")";
                else if (unitOperator == UnitaryOperator.cos)
                    codeString = "cos(" + s + ")";
                else if (unitOperator == UnitaryOperator.tan)
                    codeString = "tan(" + s + ")";
                else if (unitOperator == UnitaryOperator.arcsin)
                    codeString = "arcsin(" + s + ")";
                else if (unitOperator == UnitaryOperator.arccos)
                    codeString = "arccos(" + s + ")";
                else if (unitOperator == UnitaryOperator.arctan)
                    codeString = "arctan(" + s + ")";
                else if (unitOperator == UnitaryOperator.fact)
                    codeString = "(" + s + ")!";
                else if (unitOperator == UnitaryOperator.dfact)
                    codeString = "(" + s + ")!!";
                else if (unitOperator == UnitaryOperator.ln)
                    codeString = "ln(" + s + ")";
                else if (unitOperator == UnitaryOperator.lg)
                    codeString = "lg(" + s + ")";
                else if (unitOperator == UnitaryOperator.abs)
                    codeString = "Abs(" + s + ")";
                else if (unitOperator == UnitaryOperator.e)
                    codeString = "E(" + s + ")";
                else if (unitOperator == UnitaryOperator.d)
                    codeString = "D(" + s + ")";
                else if (unitOperator == UnitaryOperator.sinh)
                    codeString = "sinh(" + s + ")";
                else if (unitOperator == UnitaryOperator.cosh)
                    codeString = "cosh(" + s + ")";
                else if (unitOperator == UnitaryOperator.tanh)
                    codeString = "tanh(" + s + ")";
                else if (unitOperator == UnitaryOperator.arsh)
                    codeString = "arsh(" + s + ")";
                else if (unitOperator == UnitaryOperator.arch)
                    codeString = "arch(" + s + ")";
                else if (unitOperator == UnitaryOperator.arth)
                    codeString = "arth(" + s + ")";
                else if (unitOperator == UnitaryOperator.cov)
                    codeString = "Cov(" + s + ")";
                else if (unitOperator == UnitaryOperator.rho)
                    codeString = "rho(" + s + ")";
                else if (unitOperator == UnitaryOperator.rank)
                    codeString = "rank(" + s + ")";
                else if (unitOperator == UnitaryOperator.p)
                {
                    if (s == "i")
                        codeString = "π";
                    else
                        throw new Exception(CalculationResources.GetString("error_13"));
                }
                else
                    codeString = s;

                if (isMinus)
                {
                    codeString = "-(" + codeString + ")";
                }

                return codeString; //removeUselessKHPairs(new InputString(myCode)).ToString();
            }
            catch
            {
                throw new Exception(CalculationResources.GetString("error_13"));
            }
        }

        public InputString ToInputString()
        {
            if (!Value.IsNull)
            {
                InputString mc = value.Value;
                if (Value.IsMatrix)
                {
                    mc = new InputString("[") + new InputString(Value.Mtr.n.ToString()) + new InputString(";") + new InputString(Value.Mtr.m.ToString());
                    for (int i = 0; i < Value.Mtr.n; i++)
                        for (int j = 0; j < Value.Mtr.m; j++)
                            mc += new InputString(";") + Value.Mtr[i][j].ToInputString();
                    mc += new InputString("]");
                }
                if (unitOperator == UnitaryOperator.sqrt)
                    valueString = "(√(" + mc + "))";
                else if (unitOperator == UnitaryOperator.sin)
                    valueString = "(sin(" + mc + "))";
                else if (unitOperator == UnitaryOperator.cos)
                    valueString = "(cos(" + mc + "))";
                else if (unitOperator == UnitaryOperator.tan)
                    valueString = "(tan(" + mc + "))";
                else if (unitOperator == UnitaryOperator.arcsin)
                    valueString = "(arcsin(" + mc + "))";
                else if (unitOperator == UnitaryOperator.arccos)
                    valueString = "(arccos(" + mc + "))";
                else if (unitOperator == UnitaryOperator.arctan)
                    valueString = "(arctan(" + mc + "))";
                else if (unitOperator == UnitaryOperator.fact)
                    valueString = "(fact(" + mc + "))";
                else if (unitOperator == UnitaryOperator.dfact)
                    valueString = "(dfact(" + mc + "))";
                else if (unitOperator == UnitaryOperator.ln)
                    valueString = "(ln(" + mc + "))";
                else if (unitOperator == UnitaryOperator.lg)
                    valueString = "(lg(" + mc + "))";
                else if (unitOperator == UnitaryOperator.abs)
                    valueString = "(Abs(" + mc + "))";
                else if (unitOperator == UnitaryOperator.e)
                    valueString = "(Ex(" + mc + "))";
                else if (unitOperator == UnitaryOperator.d)
                    valueString = "(Dx(" + mc + "))";
                else if (unitOperator == UnitaryOperator.sinh)
                    valueString = "(sinh(" + mc + "))";
                else if (unitOperator == UnitaryOperator.cosh)
                    valueString = "(cosh(" + mc + "))";
                else if (unitOperator == UnitaryOperator.tanh)
                    valueString = "(tanh(" + mc + "))";
                else if (unitOperator == UnitaryOperator.arsh)
                    valueString = "(arsh(" + mc + "))";
                else if (unitOperator == UnitaryOperator.arch)
                    valueString = "(arch(" + mc + "))";
                else if (unitOperator == UnitaryOperator.arth)
                    valueString = "(arth(" + mc + "))";
                else if (unitOperator == UnitaryOperator.cov)
                    valueString = "(cov(" + mc + "))";
                else if (unitOperator == UnitaryOperator.rho)
                    valueString = "(rho(" + mc + "))";
                else if (unitOperator == UnitaryOperator.rank)
                    valueString = "(rank(" + mc + "))";
                else if (unitOperator == UnitaryOperator.p)
                {
                    if (mc.ToString() == "i")
                    {
                        var tmp = "" + mc;  // 复制
                        tmp[0].C = 'π';
                        tmp[0].I--;
                        valueString = "(" + tmp + ")";
                    }
                    else if(mc.ToString() == NumericText.Format(MathValue.PI))
                        valueString = "(" + mc + ")";
                    else
                        throw new Exception(CalculationResources.GetString("error_13"));
                }
                else
                    valueString = "(" + mc + ")";
                if(isMinus)
                {
                    valueString = "(-{0," + valueString + "})";
                }
                return valueString;
            }

            try
            {
                InputString s, t;
                if (children[0].children.Count <= 1)
                    s = children[0].ToInputString();
                else
                    s = "(" + children[0].ToInputString() + ")";
                for (int i = 0; i < binOperators.Count; i++)
                {
                    if (children[i + 1].children.Count <= 1)
                        t = children[i + 1].ToInputString();
                    else
                        t = "(" + children[i + 1].ToInputString() + ")";
                    s = "(" + new InputString(oToS(binOperators[i])) + "{" + s + "," + t + "}" + ")";
                }
                if (unitOperator == UnitaryOperator.sqrt)
                    valueString = "(√(" + s + "))";
                else if (unitOperator == UnitaryOperator.sin)
                    valueString = "(sin(" + s + "))";
                else if (unitOperator == UnitaryOperator.cos)
                    valueString = "(cos(" + s + "))";
                else if (unitOperator == UnitaryOperator.tan)
                    valueString = "(tan(" + s + "))";
                else if (unitOperator == UnitaryOperator.arcsin)
                    valueString = "(arcsin(" + s + "))";
                else if (unitOperator == UnitaryOperator.arccos)
                    valueString = "(arccos(" + s + "))";
                else if (unitOperator == UnitaryOperator.arctan)
                    valueString = "(arctan(" + s + "))";
                else if (unitOperator == UnitaryOperator.fact)
                    valueString = "(fact(" + s + "))";
                else if (unitOperator == UnitaryOperator.dfact)
                    valueString = "(dfact(" + s + "))";
                else if (unitOperator == UnitaryOperator.ln)
                    valueString = "(ln(" + s + "))";
                else if (unitOperator == UnitaryOperator.lg)
                    valueString = "(lg(" + s + "))";
                else if (unitOperator == UnitaryOperator.abs)
                    valueString = "(Abs(" + s + "))";
                else if (unitOperator == UnitaryOperator.e)
                    valueString = "(Ex(" + s + "))";
                else if (unitOperator == UnitaryOperator.d)
                    valueString = "(Dx(" + s + "))";
                else if (unitOperator == UnitaryOperator.sinh)
                    valueString = "(sinh(" + s + "))";
                else if (unitOperator == UnitaryOperator.cosh)
                    valueString = "(cosh(" + s + "))";
                else if (unitOperator == UnitaryOperator.tanh)
                    valueString = "(tanh(" + s + "))";
                else if (unitOperator == UnitaryOperator.arsh)
                    valueString = "(arsh(" + s + "))";
                else if (unitOperator == UnitaryOperator.arch)
                    valueString = "(arch(" + s + "))";
                else if (unitOperator == UnitaryOperator.arth)
                    valueString = "(arth(" + s + "))";
                else if (unitOperator == UnitaryOperator.cov)
                    valueString = "(cov(" + s + "))";
                else if (unitOperator == UnitaryOperator.rho)
                    valueString = "(rho(" + s + "))";
                else if (unitOperator == UnitaryOperator.rank)
                    valueString = "(rank(" + s + "))";
                else if (unitOperator == UnitaryOperator.p)
                {
                    if (s.ToString() == "i")
                    {
                        var tmp = "" + s;  // 复制
                        tmp[0].C = 'π';
                        tmp[0].I--;
                        valueString = "(" + tmp + ")";
                    }
                    else if (s.ToString() == NumericText.Format(MathValue.PI))
                        valueString = "(" + s + ")";
                    else
                        throw new Exception(CalculationResources.GetString("error_13"));
                }
                else valueString = s;

                if (isMinus)
                {
                    valueString = "(-{0," + valueString + "})";
                }

                return removeUselessKHPairs(valueString);
            }
            catch
            {
                throw new Exception(CalculationResources.GetString("error_13"));
            }
        }

        private InputString removeUselessKHPairs(InputString s)
        {
            List<int> tdelete = new List<int>();
            Dictionary<int, int> kh = new Dictionary<int, int>();
            int stack = 0;
            int maxstack = 0;
            for (int i = 0; i < s.Count; i++)
            {
                if (s[i].Equals('('))
                {
                    stack++;
                    maxstack++;
                    kh.Add(i, stack);
                }
                else if (s[i].Equals(')'))
                {
                    kh.Add(i, stack);
                    stack--;
                }
            }
            int oldk = -2;
            int olds = 0;
            Dictionary<int, int> kh2 = new Dictionary<int, int>();
            foreach (int k in kh.Keys)
            {
                if (kh[k] == olds + 1 && k == oldk + 1)
                {
                    kh2.Add(k, olds);
                }
                else if (kh[k] == olds - 1 && k == oldk + 1)
                {
                    if (kh2.Values.Contains(kh[k]))
                        foreach (int got in kh2.Keys)
                        {
                            if (kh2[got] == kh[k])
                            {
                                tdelete.Add(got);
                                tdelete.Add(k);
                                kh2.Remove(got);
                                break;
                            }
                        }
                }
                oldk = k;
                olds = kh[k];
            }
            tdelete.Sort();
            for (int i = tdelete.Count - 1; i >= 0; i--)
            {
                s.RemoveAt(tdelete[i]);
            }
            return s;
        }

        public static string oToS(BinaryOperator o)
        {
            if (o == BinaryOperator.Plus)
                return "+";
            else if (o == BinaryOperator.Minus)
                return "-";
            else if (o == BinaryOperator.Multiply)
                return "*";
            else if (o == BinaryOperator.Divide)
                return "/";
            else if (o == BinaryOperator.Power)
                return "^";
            else if (o == BinaryOperator.Ncr)
                return "nCr";
            else if (o == BinaryOperator.Npr)
                return "nPr";
            else if (o == BinaryOperator.Mod)
                return "mod";
            else if (o == BinaryOperator.Yroot)
                return "yroot";
            return "";
        }

    }
}
