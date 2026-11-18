using System;
using System.Collections.Generic;
using System.Linq;

namespace NaturplayCalculator.Model
{
    public partial class Element
    {
        public static MathValue Solve(Element e, bool callEvent)
        {
            CalculationContext.Check();
            if (e.Value.Value.Equals("e"))
            {
                e.Value.Value = new InputString(NumericText.Format(MathValue.E));
                if (callEvent & OneStep != null) OneStep();
            }
            else if (e.Value.Value.Equals("π"))
            {
                e.Value.Value = new InputString(NumericText.Format(MathValue.PI));
                if (callEvent & OneStep != null) OneStep();
            }
            else if (e.Value.Value.Equals("☺"))
            {
                e.Value = (CalculationContext.Current?.PreviousAnswer ?? new MathValue(0)).Clone();
                if (callEvent & OneStep != null) OneStep();
            }
            else if (e.Value.Value.Equals("?"))
            {
                throw new Exception(CalculationResources.GetString("error_4"));
            }
            else if (e.Value.Value.Equals("-e"))
            {
                double me = -MathValue.E;
                e.Value.Value = new InputString(NumericText.Format(me));
                if (callEvent & OneStep != null) OneStep();
            }
            else if (e.Value.Value.Equals("-π"))
            {
                double mpi = -MathValue.PI;
                e.Value.Value = new InputString(NumericText.Format(mpi));
                if (callEvent & OneStep != null) OneStep();
            }
            else if (e.Value.Value.Equals("-☺"))
            {
                e.Value = new MathValue(0) - (CalculationContext.Current?.PreviousAnswer ?? new MathValue(0)).Clone();
                if (callEvent & OneStep != null) OneStep();
            }
            else if (e.Value.Value.Equals("-?"))
            {
                throw new Exception(CalculationResources.GetString("error_4"));
            }
            else if (e.Value.Value.Equals("-T"))
                throw new Exception(CalculationResources.GetString("error_3"));

            if (e.children.Count == 0 && e.unitOperator == UnitaryOperator.none)
            {
                if (e.Value.IsMatrix)
                    for (int i = 0; i < e.Value.Mtr.n; i++)
                        for (int j = 0; j < e.Value.Mtr.m; j++)
                            e.Value.Mtr[i][j] = new Element(Solve(e.Value.Mtr[i][j], callEvent));
                if (e.isMinus)
                {
                    e.Value = new MathValue(0) - e.Value;
                    e.isMinus = false;
                }
                return e.Value;
            }

            MathValue ans;
            bool ooflag = false;
            bool? shouldcall = null;
            try
            {
                if (e.children.Count > 0)
                {
                    //前瞻（提前转换应转换的元素）
                    var plusOrMinus = e.binOperators.Count > 0 && (e.binOperators[0] == BinaryOperator.Plus || e.binOperators[0] == BinaryOperator.Minus);
                    List<int> matrixN = new List<int>();
                    List<int> matrixM = new List<int>();
                    var matrixes = 0;
                    for (int i = 0; i < e.children.Count; i++)
                    {
                        Solve(e.children[i], callEvent);
                        if (e.children[i].Value.IsMatrix)
                        {
                            matrixes++;
                            if (matrixN.Count == 0 || matrixN[matrixN.Count - 1] != e.children[i].Value.Mtr.m || matrixM[matrixM.Count - 1] != e.children[i].Value.Mtr.n)
                            {
                                matrixN.Add(e.children[i].Value.Mtr.m);
                                matrixM.Add(e.children[i].Value.Mtr.n);
                            }
                        }
                    }
                    if (plusOrMinus)
                    {
                        if (matrixes > 0 && matrixN.Count > 1)
                            throw new MathException(CalculationResources.GetString("error_31"));
                        if (matrixes > 0 && matrixes < e.children.Count)
                        {
                            if (matrixN[0] != matrixM[0])
                                throw new MathException(CalculationResources.GetString("error_34"));
                            for (int i = 0; i < e.children.Count; i++)
                            {
                                if (!e.children[i].Value.IsMatrix)
                                    e.children[i].value = new MathValue(Matrix.qiuE(matrixN[0], e.children[i].value));
                            }
                            if (callEvent & OneStep != null) OneStep();
                        }
                    }

                    //计算
                    ans = e.children[0].Value;
                    for (int i = 1; i < e.children.Count; i++)
                    {
                        if (ans.Value.Equals("T") || (e.children[i].Value.Value.Equals("T") && e.binOperators[i - 1] != BinaryOperator.Power))
                            throw new MathException(CalculationResources.GetString("error_3"));
                        if (e.binOperators[i - 1] == BinaryOperator.Plus)
                        {
                            MathValue b = e.children[i].Value;
                            bool should = !(!ans.IsMatrix && !b.IsMatrix && ((ans.Cmx.Im == 0 && b.Cmx.Re == 0 && b.Cmx.Im != 0) || (b.Cmx.Im == 0 && ans.Cmx.Re == 0 && ans.Cmx.Im != 0)));
                            if (shouldcall == null)
                                shouldcall = should;
                            else
                                shouldcall |= should;
                            ans += b;
                        }
                        else if (e.binOperators[i - 1] == BinaryOperator.Minus)
                        {
                            MathValue b = e.children[i].Value;
                            if (ans.IsZero)
                            {
                                shouldcall = false;
                            }
                            else
                            {
                                bool should = !(!ans.IsMatrix && !b.IsMatrix && ((ans.Cmx.Im == 0 && b.Cmx.Re == 0 && b.Cmx.Im != 0) || (b.Cmx.Im == 0 && ans.Cmx.Re == 0 && ans.Cmx.Im != 0)));
                                if (shouldcall == null)
                                    shouldcall = should;
                                else
                                    shouldcall |= should;
                            }
                            ans -= b;
                        }
                        else if (e.binOperators[i - 1] == BinaryOperator.Multiply)
                        {
                            MathValue b = e.children[i].Value;
                            bool should = !(!ans.IsMatrix && !b.IsMatrix && ((ans.Cmx.Im == 0 && b.Cmx.Re == 0 && b.Cmx.Im == 1) || (b.Cmx.Im == 0 && ans.Cmx.Re == 0 && ans.Cmx.Im == 1)));
                            if (shouldcall == null)
                                shouldcall = should;
                            else
                                shouldcall |= should;
                            ans *= b;
                        }
                        else if (e.binOperators[i - 1] == BinaryOperator.Divide)
                            ans /= e.children[i].Value;
                        else if (e.binOperators[i - 1] == BinaryOperator.Power)
                            ans = MathValue.Pow(ans, e.children[i].Value);
                        else if (e.binOperators[i - 1] == BinaryOperator.Ncr)
                        {
                            MathValue b = e.children[i].Value;
                            if (ans.IsMatrix || b.IsMatrix)
                                throw new MathException(CalculationResources.GetString("error_16"));
                            if (ans.Cmx.Im != 0 || b.Cmx.Im != 0)
                                throw new MathException(CalculationResources.GetString("error_17"));
                            double n = ans.Cmx.Re;
                            double r = b.Cmx.Re;
                            ans = new MathValue(Combinatorics.Permutations(n, r, true));
                        }
                        else if (e.binOperators[i - 1] == BinaryOperator.Npr)
                        {
                            MathValue b = e.children[i].Value;
                            if (ans.IsMatrix || b.IsMatrix)
                                throw new MathException(CalculationResources.GetString("error_16"));
                            if (ans.Cmx.Im != 0 || b.Cmx.Im != 0)
                                throw new MathException(CalculationResources.GetString("error_17"));
                            double n = ans.Cmx.Re;
                            double r = b.Cmx.Re;
                            ans = new MathValue(Combinatorics.Permutations(n, r, false));
                        }
                        else if (e.binOperators[i - 1] == BinaryOperator.Mod)
                        {
                            MathValue b = e.children[i].Value;
                            if (ans.IsMatrix || b.IsMatrix)
                                throw new MathException(CalculationResources.GetString("error_16"));
                            if (ans.Cmx.Im != 0 || b.Cmx.Im != 0)
                                throw new MathException(CalculationResources.GetString("error_17"));
                            ans = new MathValue(ans.Cmx.Re % b.Cmx.Re);
                        }
                        else if (e.binOperators[i - 1] == BinaryOperator.Yroot)
                        {
                            MathValue b = e.children[i].Value;
                            if (ans.IsMatrix || b.IsMatrix)
                                throw new MathException(CalculationResources.GetString("error_16"));
                            if (ans.Cmx.Im != 0 || b.Cmx.Im != 0)
                                throw new MathException(CalculationResources.GetString("error_17"));
                            if (b.Cmx.Re == 0) throw new MathException(CalculationResources.GetString("division_by_zero"));
                            if (ans.Cmx.Re < 0 && b.Cmx.Re == Math.Truncate(b.Cmx.Re) && Math.Abs(b.Cmx.Re % 2) == 1)
                                ans = new MathValue(-Math.Pow(-ans.Cmx.Re, 1 / b.Cmx.Re));
                            else ans = MathValue.Pow(ans, new MathValue(1 / b.Cmx.Re));
                        }
                    }

                    e.value = ans;
                    if (callEvent && OneStep != null && shouldcall != false) OneStep();
                }
                else
                {
                    ans = e.Value;
                }

                //前瞻
                if (e.UnitOperator == UnitaryOperator.rank && ans.IsMatrix)
                {
                    for (int i = 0; i < ans.Mtr.n; i++)
                        for (int j = 0; j < ans.Mtr.m; j++)
                            ans.Mtr[i][j] = new Element(Solve(ans.Mtr[i][j], callEvent));
                    ans = new MathValue(Matrix.getStairs(ans.Mtr));
                    e.value = ans;
                    if (callEvent & OneStep != null) OneStep();
                }
                bool specialOneEleOperator = false;  // 假的、不算步数的运算符
                //计算
                if (e.unitOperator != UnitaryOperator.none)
                {
                    if (e.children.Count > 0 && !ans.IsMatrix && ans.Cmx.Im == 0)
                    {
                        if (ans.IsZero)
                            ans = new MathValue(0);
                        e.value = ans;
                        if (callEvent && OneStep != null) OneStep();
                    }

                    if (ans.Value.Equals("T"))
                        throw new MathException(CalculationResources.GetString("error_3"));

                    if (e.unitOperator == UnitaryOperator.sqrt)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        double d = ans.Cmx.Re;
                        if (d >= 0)
                            ans = new MathValue(System.Math.Sqrt(Convert.ToDouble(d)));
                        else
                            ans = new MathValue(new Complex(0, System.Math.Sqrt(Convert.ToDouble(-d))));
                    }
                    else if (e.unitOperator == UnitaryOperator.sin)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(RealFunctions.Trig(ans.Cmx.Re, UnitaryOperator.sin, CalculationContext.Current?.UseRadians ?? true));
                    }
                    else if (e.unitOperator == UnitaryOperator.cos)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(RealFunctions.Trig(ans.Cmx.Re, UnitaryOperator.cos, CalculationContext.Current?.UseRadians ?? true));
                    }
                    else if (e.unitOperator == UnitaryOperator.tan)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(RealFunctions.Trig(ans.Cmx.Re, UnitaryOperator.tan, CalculationContext.Current?.UseRadians ?? true));
                    }
                    else if (e.unitOperator == UnitaryOperator.arcsin)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Asin(ans.Cmx.Re));
                        if (!(CalculationContext.Current?.UseRadians ?? true)) ans = new MathValue(ans.Cmx.Re / MathValue.PI * 180);
                    }
                    else if (e.unitOperator == UnitaryOperator.arccos)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Acos(ans.Cmx.Re));
                        if (!(CalculationContext.Current?.UseRadians ?? true)) ans = new MathValue(ans.Cmx.Re / MathValue.PI * 180);
                    }
                    else if (e.unitOperator == UnitaryOperator.arctan)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Atan(ans.Cmx.Re));
                        if (!(CalculationContext.Current?.UseRadians ?? true)) ans = new MathValue(ans.Cmx.Re / MathValue.PI * 180);
                    }
                    else if (e.unitOperator == UnitaryOperator.fact)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        if (ans.Cmx.Re < 0) throw new MathException(CalculationResources.GetString("error_18"));
                        ans = new MathValue(Combinatorics.Factorial(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.dfact)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(Combinatorics.DoubleFactorial(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.ln)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Log(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.lg)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Log10(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.abs)
                    {
                        if (ans.IsMatrix)
                            ans = Determinant.solve(ans.Mtr, callEvent);
                        else
                        {
                            if (ans.Cmx.Im != 0)
                            {
                                ans = new MathValue(ans.Cmx.Mod);
                                shouldcall = true;
                            }
                            else ans = new MathValue(System.Math.Abs(ans.Cmx.Re));
                        }
                    }
                    else if (e.unitOperator == UnitaryOperator.e)
                    {
                        ans = MathValue.Ex(ans);
                    }
                    else if (e.unitOperator == UnitaryOperator.d)
                    {
                        ans = MathValue.Dx(ans);
                    }
                    else if (e.unitOperator == UnitaryOperator.cov)
                    {
                        ans = MathValue.Cov(ans);
                    }
                    else if (e.unitOperator == UnitaryOperator.rho)
                    {
                        ans = MathValue.Rho(ans);
                    }
                    else if (e.unitOperator == UnitaryOperator.sinh)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Sinh(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.cosh)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Cosh(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.tanh)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        ans = new MathValue(System.Math.Tanh(ans.Cmx.Re));
                    }
                    else if (e.unitOperator == UnitaryOperator.arsh)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        double ta = ans.Cmx.Re;
                        ans = new MathValue(RealFunctions.Asinh(ta));
                    }
                    else if (e.unitOperator == UnitaryOperator.arch)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        double ta = ans.Cmx.Re;
                        ans = new MathValue(RealFunctions.Acosh(ta));
                    }
                    else if (e.unitOperator == UnitaryOperator.arth)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (ans.Cmx.Im != 0) throw new MathException(CalculationResources.GetString("error_17"));
                        double ta = ans.Cmx.Re;
                        ans = new MathValue(RealFunctions.Atanh(ta));
                    }
                    else if (e.unitOperator == UnitaryOperator.rank)
                    {
                        if (!ans.IsMatrix)
                        {
                            ans = ans.IsZero ? ans : new MathValue(1);
                        }
                        else
                        {
                            var count = 0;
                            for (int i = 0; i < ans.Mtr.n; i++)
                            {
                                bool flag = false;
                                for (int j = 0; j < ans.Mtr.m; j++)
                                    if (!ans.Mtr[i][j].Value.IsZero)
                                    {
                                        flag = true;
                                        break;
                                    }
                                if (flag)
                                    count++;
                                else
                                    break;
                            }
                            ans = new MathValue(count);
                        }
                    }
                    else if (e.unitOperator == UnitaryOperator.p)
                    {
                        if (ans.IsMatrix) throw new MathException(CalculationResources.GetString("error_16"));
                        if (!(ans.Cmx.Im == 1 && ans.Cmx.Re == 0) && !(ans.Cmx.Im == 0 && ans.Cmx.Re == MathValue.PI))
                            throw new MathException(CalculationResources.GetString("error_13"));
                        ans = new MathValue(MathValue.PI);
                        specialOneEleOperator = true;
                    }
                    e.unitOperator = UnitaryOperator.none;
                    ooflag = true;
                }
                if (e.isMinus)
                {
                    ans = new MathValue(0) - ans;
                    e.isMinus = false;
                }

                if (ans.IsZero)
                    ans = new MathValue(0);
                e.value = ans;
                if (callEvent && shouldcall != false && OneStep != null && !(e.Children.Count == 1 && !e.Children[0].Value.IsNull && !ooflag) && !specialOneEleOperator)
                    OneStep();
                return ans;
            }
            catch (FormatException ex)
            {
                throw new MathException(CalculationResources.GetString("error_19"), ex);
            }
        }
    }
}
