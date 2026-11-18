using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturplayCalculator.Model
{

    //二元运算符枚举
    public enum BinaryOperator
    {
        Plus,         //加法
        Minus,        //减法
        Multiply,     //乘法
        Divide,       //除法
        Power,        //乘方
        Ncr,          //组合数
        Npr,          //排列数
        MatrixEle,    //矩阵元素分隔符
        MatrixRow,    //矩阵行分隔符
        Mod,          //取余
        Yroot         //开次方
    }

    public enum UnitaryOperator
    {
        none,
        sqrt,
        sin,
        cos,
        tan,
        arcsin,
        arccos,
        arctan,
        fact,
        dfact,
        ln,
        lg,
        abs,
        e,
        d,
        sinh,
        cosh,
        tanh,
        arsh,
        arch,
        arth,
        cov,
        rho,
        rank,
        p  // 用法：p直接和i相连变成pi，也就是π……
    }

    public partial class Element
    {
        List<Element> children = new List<Element>();
        List<BinaryOperator> binOperators = new List<BinaryOperator>();
        UnitaryOperator unitOperator = UnitaryOperator.none;
        MathValue value = new MathValue();
        InputString valueString = new InputString("");
        string codeString = "";
        bool isMinus = false;

        public List<string> Comments = new List<string>();

        int deep = 0;
        InputString num = new InputString("");
        string opr = "";
        List<Element> deepEle = new List<Element>();
        bool minus = false;

        private static Action OneStep => CalculationContext.Current?.OnStep;

        public MathValue Value
        {
            get { return value; }
            private set { this.value = value; }
        }

        public InputString ValueString
        {
            get { return valueString; }
            private set { valueString = value; }
        }

        public List<Element> Children
        {
            get { return children; }
            private set { children = value; }
        }

        public List<BinaryOperator> BinOperators
        {
            get { return binOperators; }
            private set { binOperators = value; }
        }

        public UnitaryOperator UnitOperator
        {
            get { return unitOperator; }
            private set { unitOperator = value; }
        }

        public Element()
        {
        }

        public Element(List<Element> e, List<BinaryOperator> o, UnitaryOperator oo)
        {
            for (int i = 0; i < e.Count; i++)
                this.children.Add(e[i]);
            for (int i = 0; i < o.Count; i++)
                this.binOperators.Add(o[i]);
            this.unitOperator = oo;
        }

        public Element(MathValue mv)
        {
            value = mv;
        }

        private void getLetterOperator()
        {
            if (opr != "")
            {
                var lOpr = opr.ToLowerInvariant();
                if (lOpr == "√")
                    deepEle[deep].UnitOperator = UnitaryOperator.sqrt;
                else if (lOpr == "sqrt")
                    deepEle[deep].UnitOperator = UnitaryOperator.sqrt;
                else if (lOpr == "sin")
                    deepEle[deep].UnitOperator = UnitaryOperator.sin;
                else if (lOpr == "cos")
                    deepEle[deep].UnitOperator = UnitaryOperator.cos;
                else if (lOpr == "tan")
                    deepEle[deep].UnitOperator = UnitaryOperator.tan;
                else if (lOpr == "arcsin")
                    deepEle[deep].UnitOperator = UnitaryOperator.arcsin;
                else if (lOpr == "arccos")
                    deepEle[deep].UnitOperator = UnitaryOperator.arccos;
                else if (lOpr == "arctan")
                    deepEle[deep].UnitOperator = UnitaryOperator.arctan;
                else if (lOpr == "asin")
                    deepEle[deep].UnitOperator = UnitaryOperator.arcsin;
                else if (lOpr == "acos")
                    deepEle[deep].UnitOperator = UnitaryOperator.arccos;
                else if (lOpr == "atan")
                    deepEle[deep].UnitOperator = UnitaryOperator.arctan;
                else if (lOpr == "fact")
                    deepEle[deep].UnitOperator = UnitaryOperator.fact;
                else if (lOpr == "dfact")
                    deepEle[deep].UnitOperator = UnitaryOperator.dfact;
                else if (lOpr == "ln")
                    deepEle[deep].UnitOperator = UnitaryOperator.ln;
                else if (lOpr == "lg")
                    deepEle[deep].UnitOperator = UnitaryOperator.lg;
                else if (lOpr == "abs")
                    deepEle[deep].UnitOperator = UnitaryOperator.abs;
                else if (lOpr == "e")
                    deepEle[deep].UnitOperator = UnitaryOperator.e;
                else if (lOpr == "d")
                    deepEle[deep].UnitOperator = UnitaryOperator.d;
                else if (lOpr == "sinh")
                    deepEle[deep].UnitOperator = UnitaryOperator.sinh;
                else if (lOpr == "cosh")
                    deepEle[deep].UnitOperator = UnitaryOperator.cosh;
                else if (lOpr == "tanh")
                    deepEle[deep].UnitOperator = UnitaryOperator.tanh;
                else if (lOpr == "arsh")
                    deepEle[deep].UnitOperator = UnitaryOperator.arsh;
                else if (lOpr == "arch")
                    deepEle[deep].UnitOperator = UnitaryOperator.arch;
                else if (lOpr == "arth")
                    deepEle[deep].UnitOperator = UnitaryOperator.arth;
                else if (lOpr == "cov")
                    deepEle[deep].UnitOperator = UnitaryOperator.cov;
                else if (lOpr == "ρ")
                    deepEle[deep].UnitOperator = UnitaryOperator.rho;
                else if (lOpr == "rho")
                    deepEle[deep].UnitOperator = UnitaryOperator.rho;
                else if (lOpr == "r")
                    deepEle[deep].UnitOperator = UnitaryOperator.rank;
                else if (lOpr == "rank")
                    deepEle[deep].UnitOperator = UnitaryOperator.rank;
                else if (lOpr == "p")
                    deepEle[deep].UnitOperator = UnitaryOperator.p;
                else
                    checkOpr();
                opr = "";
            }
        }

        private void getLetterTwoEleOperator()
        {
            if (opr != "")
            {
                var lOpr = opr.ToLowerInvariant();
                if (lOpr == "ncr")
                {
                    completeAEle();
                    deepEle[deep].AddOperator(BinaryOperator.Ncr);
                    opr = "";
                }
                else if (lOpr == "npr")
                {
                    completeAEle();
                    deepEle[deep].AddOperator(BinaryOperator.Npr);
                    opr = "";
                }
                else if (lOpr == "mod")
                {
                    completeAEle();
                    deepEle[deep].AddOperator(BinaryOperator.Mod);
                    opr = "";
                }
                else if (lOpr == "yroot")
                {
                    completeAEle();
                    deepEle[deep].AddOperator(BinaryOperator.Yroot);
                    opr = "";
                }
                //其他情况不可将opr清空
            }
        }

        private void checkOpr()
        {
            if (opr != "")
                throw new Exception(CalculationResources.GetString("error_1"));
        }

        public Element(InputString istring)
        {
            ExpressionLimits.Validate(istring.ToString());
            var s = new InputString("");
            s.Copy(istring);
            RemoveWhitespaceOutsideComments(s);
            if (s.Equals(""))
                throw new Exception(CalculationResources.GetString("error_0"));
            //if (s[0] == '-')
            //    s = '0' + s;
            //s = s.Replace("(-", "(0-");
            if (s.Count>1 && s[0].C == '-' && !s[1].IsConst)
                s = new InputChar('0') + s;
            s.Replace("Ans", "☺").Replace("ans", "☺");

            deepEle.Add(this);
            bool isComment = false;
            string comment = "";
            //上一个字符是否是转义符号'\'
            bool lastZhuanYi = false;
            for (int i = 0; i < s.Count; i++)
            {
                CalculationContext.Check();
                if (!isComment)
                {
                    if ((s[i].Equals('E') || s[i].Equals('e')) && num.Count > 0 && i + 1 < s.Count
                        && (char.IsDigit(s[i + 1].C) || ((s[i + 1].C == '+' || s[i + 1].C == '-') && i + 2 < s.Count && char.IsDigit(s[i + 2].C))))
                    {
                        num += new InputChar('E', s[i].I);
                        if (s[i + 1].C == '+' || s[i + 1].C == '-') num += s[++i];
                    }
                    else if (s[i].Equals('('))
                    {
                        getLetterTwoEleOperator();
                        if (num.Count > 0 || (i > 0 && (s[i - 1].Equals(')') || s[i - 1].Equals('i') || s[i - 1].Equals('π') || s[i - 1].Equals('☺'))))
                        {
                            completeAEle();
                            deepEle[deep].AddOperator(BinaryOperator.Multiply);
                        }
                        num = new InputString("");
                        push();
                        getLetterOperator();
                    }
                    else if (s[i].Equals(')'))
                    {
                        checkOpr();
                        completeAEle();
                        pop(1);
                    }
                    else if (s[i].Equals(','))
                    {
                        checkOpr();
                        completeAEle();
                        if (i > 0 && s[i - 1].Equals(','))
                            deepEle[deep].binOperators[deepEle[deep].binOperators.Count - 1] = BinaryOperator.MatrixRow;
                        else
                            deepEle[deep].AddOperator(BinaryOperator.MatrixEle);
                    }
                    else if (s[i].Equals('+'))
                    {
                        checkOpr();
                        completeAEle();
                        deepEle[deep].AddOperator(BinaryOperator.Plus);
                    }
                    else if (s[i].Equals('-'))
                    {
                        checkOpr();
                        if ((i > 0 && (s[i - 1].Equals(',') || s[i - 1].Equals('('))) || i == 0)
                        {
                            completeAEle();
                            //补0
                            deepEle[deep].AddElement(new Element(new MathValue(0)));
                            deepEle[deep].AddOperator(BinaryOperator.Minus);
                        }
                        else if (i > 0 && (s[i - 1].Equals('*') || s[i - 1].Equals('/') || s[i - 1].Equals('^') || s[i - 1].Equals('√') || s[i - 1].Equals('+') || s[i - 1].Equals('-')))
                        {
                            minus = !minus;
                            completeAEle();
                        }
                        else
                        {
                            completeAEle();
                            deepEle[deep].AddOperator(BinaryOperator.Minus);
                        }
                    }
                    else if (s[i].Equals('*'))
                    {
                        checkOpr();
                        completeAEle();
                        deepEle[deep].AddOperator(BinaryOperator.Multiply);
                    }
                    else if (s[i].Equals('/'))
                    {
                        checkOpr();
                        completeAEle();
                        deepEle[deep].AddOperator(BinaryOperator.Divide);
                    }
                    else if (s[i].Equals('^'))
                    {
                        checkOpr();
                        completeAEle();
                        deepEle[deep].AddOperator(BinaryOperator.Power);
                    }
                    else if (s[i].IsNumberORPoint)
                    {
                        if (i > 0 && ((s[i - 1].Equals('e') && !num.Contains("E")) || s[i - 1].Equals('π') || s[i - 1].Equals('☺') || s[i - 1].Equals('?')))
                            throw new Exception(CalculationResources.GetString("error_2"));
                        getLetterTwoEleOperator();
                        if (num.Count == 0) push();
                        getLetterOperator();
                        num += s[i];
                    }
                    else if (s[i].Equals('e') || s[i].Equals('π') || s[i].Equals('☺') || s[i].Equals('?') || s[i].Equals('T') || (s[i].Equals('i') && (i == 0 || (i > 0 && !s[i - 1].Equals('s')))))
                    {
                        if ((num.Count > 0 && !opr.Contains('r') && !opr.Contains('m')) || (i > 0 && (s[i - 1].Equals('e') || s[i - 1].Equals('π') || s[i - 1].Equals('☺') || s[i - 1].Equals('?') || s[i - 1].Equals('i'))))
                        {
                            if (s[i].Equals('T'))
                                throw new Exception(CalculationResources.GetString("error_3"));
                            completeAEle();
                            deepEle[deep].AddOperator(BinaryOperator.Multiply);
                        }
                        getLetterTwoEleOperator();
                        push();
                        getLetterOperator();
                        completeAEle(new InputString(s[i]));
                    }
                    else if (s[i].Equals('!'))
                    {
                        if (i > 0 && s[i - 1].Equals('!'))
                            deepEle[deep].UnitOperator = UnitaryOperator.dfact;
                        else
                            deepEle[deep].UnitOperator = UnitaryOperator.fact;
                    }
                    else if (s[i].Equals('['))
                    {
                        isComment = true;
                        comment = "";
                    }
                    else if (!s[i].Equals(' '))
                    {
                        if (num.Count > 0 && opr == "" && !s[i].Equals('n') && !s[i].Equals('m') && !s[i].Equals('y'))
                        {
                            completeAEle();
                            deepEle[deep].AddOperator(BinaryOperator.Multiply);
                        }
                        else if (i > 0 && (s[i - 1].Equals(')') || s[i - 1].Equals('e') || s[i - 1].Equals('π') || s[i - 1].Equals('☺') || s[i - 1].Equals('?') || s[i - 1].Equals('i')) && opr == "" && !s[i].Equals('n') && !s[i].Equals('m') && !s[i].Equals('y'))
                            deepEle[deep].AddOperator(BinaryOperator.Multiply);
                        opr += s[i];
                    }
                }
                else
                {
                    if (s[i].Equals(']'))
                    {
                        if (!lastZhuanYi)
                        {
                            isComment = false;
                            Comments.Add(comment);
                        }
                        else
                        {
                            comment += s[i];
                            lastZhuanYi = false;
                        }
                    }
                    else if (s[i].Equals('\\')) {
                        if (lastZhuanYi)
                            comment += s[i];
                        lastZhuanYi = !lastZhuanYi;
                    }
                    else {
                        comment += s[i];
                        lastZhuanYi = false;
                    }
                }
            }
            completeAEle();
            if (deep > 0)
            {
                try
                {
                    string ykh = "";
                    for (int i = 0; i < deep; i++) ykh += ")";
                    var temp = new Element(s + new InputString(ykh));
                }
                catch (Exception e)
                {
                    if (e.Message == CalculationResources.GetString("error_4"))
                        throw new Exception(CalculationResources.GetString("error_5"));
                    else if (e.Message.Contains(CalculationResources.GetString("error_7")) || e.Message.Contains(CalculationResources.GetString("error_8")))
                        throw;
                }
                throw new Exception(CalculationResources.GetString("error_6"));
            }
            if (opr != "") throw new Exception(CalculationResources.GetString("error_4"));
            tryToMakeItSafe(this);
            getBetter1(this);
            getBetter2(this);
            getBetter3(this);
            getBetter4(this);
            GroupPowers(this);
            getMatrix(this);
            getSingler(this);
        }

        private static void RemoveWhitespaceOutsideComments(InputString text)
        {
            bool comment = false, escaped = false;
            for (int index = 0; index < text.Count; index++)
            {
                char c = text[index].C;
                if (comment)
                {
                    if (c == ']' && !escaped) comment = false;
                    escaped = c == '\\' && !escaped;
                }
                else if (c == '[') comment = true;
                else if (char.IsWhiteSpace(c)) text.RemoveAt(index--);
            }
        }

        //TODO:这个机制有天生缺陷，可控力太差，应逐层填补空缺。
        private void tryToMakeItSafe(Element ele)
        {
            foreach (var e in ele.children)
                tryToMakeItSafe(e);
            if (ele.binOperators.Count > 0 && (ele.binOperators[ele.binOperators.Count - 1] == BinaryOperator.MatrixRow || ele.binOperators[ele.binOperators.Count - 1] == BinaryOperator.MatrixEle))
            {
                int lasthave = 0;
                int totalrow = 0;
                bool lastrow = true;
                for (int i = ele.binOperators.Count - 1; i >= 0; i--)
                {
                    if (lastrow && ele.binOperators[i] == BinaryOperator.MatrixEle)
                        lasthave++;
                    else if (lastrow && ele.binOperators[i] != BinaryOperator.MatrixEle)
                    {
                        if (ele.binOperators[i] == BinaryOperator.MatrixRow)
                            lastrow = false;
                        else
                        {
                            totalrow = lasthave;
                            break;
                        }
                    }
                    else if (!lastrow && ele.binOperators[i] == BinaryOperator.MatrixEle)
                        totalrow++;
                    else if (!lastrow && ele.binOperators[i] != BinaryOperator.MatrixEle)
                        break;
                }
                if (totalrow == 0) totalrow = lasthave;
                int less = totalrow - lasthave + 1;
                if (less > 1)
                {
                    if (ele.binOperators.Count == ele.children.Count - 1)
                        throw new Exception(CalculationResources.GetString("error_7") + CalculationResources.GetString("error_9") + less);
                    else
                        throw new Exception(CalculationResources.GetString("error_8") + CalculationResources.GetString("error_9") + less);
                }
            }
            if (ele.binOperators.Count != ele.children.Count - 1 && !(ele.binOperators.Count == 0 && ele.children.Count == 0))
            {
                if (ele.binOperators.Count > 0 && ele.binOperators[ele.binOperators.Count - 1] == BinaryOperator.MatrixEle)
                {
                    int lasthave = 0;
                    int totalrow = 0;
                    bool lastrow = true;
                    for (int i = ele.binOperators.Count - 1; i >= 0; i--)
                    {
                        if (lastrow && ele.binOperators[i] == BinaryOperator.MatrixEle)
                            lasthave++;
                        else if (lastrow && ele.binOperators[i] != BinaryOperator.MatrixEle)
                        {
                            if (ele.binOperators[i] == BinaryOperator.MatrixRow)
                                lastrow = false;
                            else
                            {
                                totalrow = lasthave;
                                break;
                            }
                        }
                        else if (!lastrow && ele.binOperators[i] == BinaryOperator.MatrixEle)
                            totalrow++;
                        else if (!lastrow && ele.binOperators[i] != BinaryOperator.MatrixEle)
                            break;
                    }
                    if (totalrow == 0) totalrow = lasthave;
                    int less = totalrow - lasthave + 1;
                    throw new Exception(CalculationResources.GetString("error_8") + CalculationResources.GetString("error_9") + less);
                }
                else
                    throw new Exception(CalculationResources.GetString("error_4"));
            }
        }

        private void completeAEle(InputString s = null)
        {
            if (s == null)
            {
                if (num.Count>0)
                {
                    if (minus)
                    {
                        if (deepEle[deep].UnitOperator == UnitaryOperator.none)
                            num = new InputString('-') + num;
                        else
                            deepEle[deep].isMinus = true;
                        minus = false;
                    }
                    deepEle[deep].Value.Value = num;
                    num = new InputString("");
                    pop(1);
                }
            }
            else
            {
                //if (minus)
                //{
                //    s = new InputString('-') + s;
                //    minus = false;
                //}
                deepEle[deep].Value.Value = s;
                deepEle[deep].isMinus = minus;
                if (minus) minus = false;
                pop(1);
            }
        }

        private void getBetter1(Element e)
        {
            if (
                (e.binOperators.Contains(BinaryOperator.Plus) || e.binOperators.Contains(BinaryOperator.Minus) || e.binOperators.Contains(BinaryOperator.Multiply) || e.binOperators.Contains(BinaryOperator.Divide) || e.binOperators.Contains(BinaryOperator.MatrixEle) || e.binOperators.Contains(BinaryOperator.MatrixRow) || e.binOperators.Contains(BinaryOperator.Mod) || e.binOperators.Contains(BinaryOperator.Ncr) || e.binOperators.Contains(BinaryOperator.Npr)) 
                && (e.binOperators.Contains(BinaryOperator.Power) || e.binOperators.Contains(BinaryOperator.Yroot))
                )
            {
                try
                {
                    List<Element> tE = new List<Element>();
                    List<BinaryOperator> tO = new List<BinaryOperator>();
                    int oldi = -1;

                    int ii = 0;
                    while (ii < e.binOperators.Count)
                    {
                        if (e.binOperators[ii] == BinaryOperator.Power || e.binOperators[ii] == BinaryOperator.Yroot)
                        {
                            if (tO.Count == 0)
                            {
                                tE.Add(e.children[ii]);
                                oldi = ii;
                            }
                            tO.Add(e.binOperators[ii]);
                            tE.Add(e.children[ii + 1]);
                            ii++;
                        }
                        else
                        {
                            if (tO.Count > 0)
                            {
                                e.binOperators.RemoveRange(oldi, tO.Count);
                                e.children.RemoveRange(oldi, tO.Count);
                                Element t = new Element(tE, tO, UnitaryOperator.none);
                                e.children[oldi] = t;
                                tO.Clear();
                                tE.Clear();
                                ii = oldi + 1;
                            }
                            else ii++;
                        }
                    }

                    if (tO.Count > 0)
                    {
                        e.binOperators.RemoveRange(oldi, tO.Count);
                        e.children.RemoveRange(oldi, tO.Count);
                        Element t = new Element(tE, tO, UnitaryOperator.none);
                        e.children[oldi] = t;
                        tO.Clear();
                        tE.Clear();
                    }
                }
                catch
                {
                    throw new Exception(CalculationResources.GetString("error_10"));
                }
            }

            for (int i = 0; i < e.children.Count; i++)
            {
                if (e.children[i].binOperators.Count > 0)
                    getBetter1(e.children[i]);
            }
        }

        private void getBetter2(Element e)
        {
            if (
                (e.binOperators.Contains(BinaryOperator.Plus) || e.binOperators.Contains(BinaryOperator.Minus) || e.binOperators.Contains(BinaryOperator.Multiply) || e.binOperators.Contains(BinaryOperator.Divide) || e.binOperators.Contains(BinaryOperator.MatrixEle) || e.binOperators.Contains(BinaryOperator.MatrixRow) || e.binOperators.Contains(BinaryOperator.Mod))
                && (e.binOperators.Contains(BinaryOperator.Ncr) || e.binOperators.Contains(BinaryOperator.Npr))
                )
            {
                try
                {
                    List<Element> tE = new List<Element>();
                    List<BinaryOperator> tO = new List<BinaryOperator>();
                    int oldi = -1;

                    int ii = 0;
                    while (ii < e.binOperators.Count)
                    {
                        if (e.binOperators[ii] == BinaryOperator.Ncr || e.binOperators[ii] == BinaryOperator.Npr)
                        {
                            if (tO.Count == 0)
                            {
                                tE.Add(e.children[ii]);
                                oldi = ii;
                            }
                            tO.Add(e.binOperators[ii]);
                            tE.Add(e.children[ii + 1]);
                            ii++;
                        }
                        else
                        {
                            if (tO.Count > 0)
                            {
                                e.binOperators.RemoveRange(oldi, tO.Count);
                                e.children.RemoveRange(oldi, tO.Count);
                                Element t = new Element(tE, tO, UnitaryOperator.none);
                                e.children[oldi] = t;
                                tO.Clear();
                                tE.Clear();
                                ii = oldi + 1;
                            }
                            else ii++;
                        }
                    }

                    if (tO.Count > 0)
                    {
                        e.binOperators.RemoveRange(oldi, tO.Count);
                        e.children.RemoveRange(oldi, tO.Count);
                        Element t = new Element(tE, tO, UnitaryOperator.none);
                        e.children[oldi] = t;
                        tO.Clear();
                        tE.Clear();
                    }
                }
                catch
                {
                    throw new Exception(CalculationResources.GetString("error_10"));
                }
            }

            for (int i = 0; i < e.children.Count; i++)
            {
                if (e.children[i].binOperators.Count > 0)
                    getBetter2(e.children[i]);
            }
        }

        private void getBetter3(Element e)
        {
            if ((e.binOperators.Contains(BinaryOperator.Plus) || e.binOperators.Contains(BinaryOperator.Minus) || e.binOperators.Contains(BinaryOperator.MatrixEle) || e.binOperators.Contains(BinaryOperator.MatrixRow)) && (e.binOperators.Contains(BinaryOperator.Multiply) || e.binOperators.Contains(BinaryOperator.Divide) || e.binOperators.Contains(BinaryOperator.Mod)))
            {
                try
                {
                    List<Element> tE = new List<Element>();
                    List<BinaryOperator> tO = new List<BinaryOperator>();
                    int oldi = -1;

                    int ii = 0;
                    while (ii < e.binOperators.Count)
                    {
                        if (e.binOperators[ii] == BinaryOperator.Multiply || e.binOperators[ii] == BinaryOperator.Divide || e.binOperators[ii] == BinaryOperator.Mod)
                        {
                            if (tO.Count == 0)
                            {
                                tE.Add(e.children[ii]);
                                oldi = ii;
                            }
                            tO.Add(e.binOperators[ii]);
                            tE.Add(e.children[ii + 1]);
                            ii++;
                        }
                        else
                        {
                            if (tO.Count > 0)
                            {
                                e.binOperators.RemoveRange(oldi, tO.Count);
                                e.children.RemoveRange(oldi, tO.Count);
                                Element t = new Element(tE, tO, UnitaryOperator.none);
                                e.children[oldi] = t;
                                tO.Clear();
                                tE.Clear();
                                ii = oldi + 1;
                            }
                            else ii++;
                        }
                    }

                    if (tO.Count > 0)
                    {
                        e.binOperators.RemoveRange(oldi, tO.Count);
                        e.children.RemoveRange(oldi, tO.Count);
                        Element t = new Element(tE, tO, UnitaryOperator.none);
                        e.children[oldi] = t;
                        tO.Clear();
                        tE.Clear();
                    }
                }
                catch
                {
                    throw new Exception(CalculationResources.GetString("error_10"));
                }
            }

            for (int i = 0; i < e.children.Count; i++)
            {
                if (e.children[i].binOperators.Count > 0)
                    getBetter3(e.children[i]);
            }
        }

        private void getBetter4(Element e)
        {
            if ((e.binOperators.Contains(BinaryOperator.MatrixEle) || e.binOperators.Contains(BinaryOperator.MatrixRow)) && (e.binOperators.Contains(BinaryOperator.Plus) || e.binOperators.Contains(BinaryOperator.Minus)))
            {
                try
                {
                    List<Element> tE = new List<Element>();
                    List<BinaryOperator> tO = new List<BinaryOperator>();
                    int oldi = -1;

                    int ii = 0;
                    while (ii < e.binOperators.Count)
                    {
                        if (e.binOperators[ii] == BinaryOperator.Plus || e.binOperators[ii] == BinaryOperator.Minus)
                        {
                            if (tO.Count == 0)
                            {
                                tE.Add(e.children[ii]);
                                oldi = ii;
                            }
                            tO.Add(e.binOperators[ii]);
                            tE.Add(e.children[ii + 1]);
                            ii++;
                        }
                        else
                        {
                            if (tO.Count > 0)
                            {
                                e.binOperators.RemoveRange(oldi, tO.Count);
                                e.children.RemoveRange(oldi, tO.Count);
                                Element t = new Element(tE, tO, UnitaryOperator.none);
                                e.children[oldi] = t;
                                tO.Clear();
                                tE.Clear();
                                ii = oldi + 1;
                            }
                            else ii++;
                        }
                    }

                    if (tO.Count > 0)
                    {
                        e.binOperators.RemoveRange(oldi, tO.Count);
                        e.children.RemoveRange(oldi, tO.Count);
                        Element t = new Element(tE, tO, UnitaryOperator.none);
                        e.children[oldi] = t;
                        tO.Clear();
                        tE.Clear();
                    }
                }
                catch
                {
                    throw new Exception(CalculationResources.GetString("error_10"));
                }
            }

            for (int i = 0; i < e.children.Count; i++)
            {
                if (e.children[i].binOperators.Count > 0)
                    getBetter4(e.children[i]);
            }
        }

        private void getSingler(Element e)
        {
            if (e.Children.Count == 1 && (e.Children[0].Value.IsNull || e.Children[0].Value.IsMatrix) && e.UnitOperator == UnitaryOperator.none)
            {
                var t = new Element();
                t.Value = e.Children[0].Value;
                t.Children = e.Children[0].Children;
                t.BinOperators = e.Children[0].BinOperators;
                t.UnitOperator = e.Children[0].UnitOperator;
                t.ValueString = e.Children[0].ValueString;
                e.Value = t.Value;
                e.Children = t.Children;
                e.BinOperators = t.BinOperators;
                e.UnitOperator = t.UnitOperator;
                e.ValueString = t.ValueString;
                getSingler(e);
            }

            foreach (var ele in e.Children)
            {
                getSingler(ele);
            }
        }

        private void getMatrix(Element e)
        {
            try
            {
                if (e.BinOperators.Contains(BinaryOperator.MatrixEle) || e.BinOperators.Contains(BinaryOperator.MatrixRow))
                {
                    int m = 0, n = 1, mm = 0;
                    foreach (var o in e.BinOperators)
                    {
                        if (o == BinaryOperator.MatrixEle)
                        {
                            if (n == 1) m++;
                            mm++;
                        }
                        else if (o == BinaryOperator.MatrixRow)
                        {
                            if (n == 1) m++;
                            n++;
                            if (mm != m - 1)
                                throw new Exception(CalculationResources.GetString("error_11"));
                            mm = 0;
                        }
                    }
                    if (n == 1) m++;

                    var mtr = new Matrix(n, m);
                    for (int i = 0; i < m * n; i++)
                    {
                        getMatrix(e.Children[i]);
                        mtr[i / m][i % m] = e.Children[i];
                    }
                    e.Value.Mtr = mtr;
                    e.Children.Clear();
                    e.BinOperators.Clear();
                }
                else
                {
                    foreach (var ele in e.Children)
                    {
                        getMatrix(ele);
                    }
                }
            }
            catch
            {
                throw new Exception(CalculationResources.GetString("error_11"));
            }
        }

        private void push()
        {
            deep++;
            Element t = new Element();
            deepEle.Add(t);
        }

        private void pop(int times)
        {
            for (int i = 0; i < times; i++)
            {
                if (deep == 0)
                    throw new Exception(CalculationResources.GetString("error_12"));
                deepEle[deep - 1].AddElement(deepEle[deep]);
                deepEle.Remove(deepEle[deep]);
                deep--;
            }
        }

        public void AddElement(Element ele)
        {
            children.Add(ele);
        }

        public void AddOperator(BinaryOperator otr)
        {
            binOperators.Add(otr);
        }

    }
}
