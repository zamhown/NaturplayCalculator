using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NaturplayCalculator.Model;

namespace NaturplayCalculator.NatureModel
{
    public abstract class NatureTerm : Element
    {
        /*
        public abstract int Sign { get; }

        protected string tex = "";
        protected string texDictOrder = "";
        public string ToTex(bool dictOrder = false)
        {
            if (dictOrder)
            {
                if (texDictOrder == "")
                    texDictOrder = toTexDictOrder();
                return texDictOrder;
            }
            else
            {
                if (tex == "")
                    tex = toTex();
                return tex;
            }
        }
        protected abstract string toTex();
        protected abstract string toTexDictOrder();

        public abstract int GetFactor();
        public abstract NatureTerm ReduceFactor(int factor);

        protected NatureTerm simplified = null;
        public NatureTerm Simplify()
        {
            if (simplified == null)
                simplified = simplify();
            return simplified;
        }
        protected abstract NatureTerm simplify();

        protected NatureTerm signSimplified = null;
        public NatureTerm SignSimplify()
        {
            if (signSimplified == null)
                signSimplified = signSimplify();
            return signSimplified;
        }
        protected abstract NatureTerm signSimplify();

        public static NatureTerm operator -(NatureTerm t)
        {
            return t.negate();
        }
        protected abstract NatureTerm negate();

        public NatureTerm Abs()
        {
            if (Sign < 0)
                return negate();
            else
                return this;
        }

        public abstract double Calc();
        */
    }
}
