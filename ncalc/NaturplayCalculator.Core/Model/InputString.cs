using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NaturplayCalculator.Model
{
    /// <summary>
    /// 字符串类型
    /// </summary>
    public enum InputStringType
    {
        /// <summary>
        /// 默认
        /// </summary>
        None,
        /// <summary>
        /// 自动添加
        /// </summary>
        Auto
    }

    public class InputChar
    {
        public InputStringType Type { get; set; } = InputStringType.None;
        public char C { get; set; } = (char)0;
        public int I { get; set; } = -2;
        public bool IsConst
        {
            get
            {
                return (Convert.ToInt32(C) >= Convert.ToInt32('0') && Convert.ToInt32(C) <= Convert.ToInt32('9') || C == '.' || C == 'e' || C == 'π' || C == '☺' || C == 'T' || C == '?');
            }
        }
        public bool IsNumberORPoint
        {
            get
            {
                return (Convert.ToInt32(C) >= Convert.ToInt32('0') && Convert.ToInt32(C) <= Convert.ToInt32('9') || C == '.');
            }
        }
        public InputChar(char c)
        {
            C = c;
        }
        public InputChar(char c, int i)
        {
            C = c;
            I = i;
        }
        public override string ToString()
        {
            return C.ToString();
        }
        public override bool Equals(object obj)
        {
            return obj != null && this.ToString() == obj.ToString();
        }
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
    }

    public class InputString:List<InputChar>,IConvertible
    {
        private InputStringType _type = InputStringType.None;
        public InputStringType Type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
                foreach (var c in this)
                    c.Type = value;
            }
        }

        public InputString(string s, InputStringType type = InputStringType.None)
        {
            foreach (var c in s)
                Add(new InputChar(c));
            Type = type;
        }
        public InputString(char c)
        {
            Add(new InputChar(c));
        }
        public InputString(InputChar c)
        {
            Add(new InputChar(c.C, c.I) { Type = c.Type });
        }
        public void GiveIndex()
        {
            for (int i = 0; i < Count; i++)
                this[i].I = i;
        }
        public override string ToString()
        {
            var s = new StringBuilder();
            foreach (var c in this)
                s.Append(c.C);
            return s.ToString();
        }
        public override bool Equals(object obj)
        {
            return obj != null && this.ToString() == obj.ToString();
        }
        public override int GetHashCode()
        {
            return ToString().GetHashCode();
        }
        public static InputString operator +(InputString a, InputString b)
        {
            var result = new InputString(""); result.Copy(a);
            foreach (var c in b) result.Add(new InputChar(c.C, c.I) { Type = c.Type });
            return result;
        }
        public static InputString operator +(InputString a, InputChar b) => a + new InputString(b);
        public static InputString operator +(InputChar a, InputString b) => new InputString(a) + b;
        public static InputString operator +(string a, InputString b) => new InputString(a) + b;
        public static InputString operator +(InputString a, string b) => a + new InputString(b);
        public InputString Replace(string a,string b)
        {
            if (string.IsNullOrEmpty(a)) throw new ArgumentException("Replacement target cannot be empty.", nameof(a));
            var ia = new InputString(a);
            var ib = new InputString(b);
            int t = IndexOf(ia);
            while (t>-1)
            {
                int originalIndex = this[t].I;
                var replacement = new InputString(b);
                for (int j = 0; j < replacement.Count; j++) replacement[j].I = originalIndex < 0 ? originalIndex : originalIndex + Math.Min(j, ia.Count - 1);
                RemoveRange(t, ia.Count);
                InsertRange(t, replacement);
                t = IndexOf(ia, t + ib.Count);
            }
            return this;
        }
        public int IndexOf(InputString s, int begin = 0)
        {
            for (int i = begin; i < Count - s.Count + 1; i++)
            {
                bool flag = true;
                for (int j = 0; j < s.Count; j++)
                {
                    if (!this[i + j].Equals(s[j]))
                    {
                        flag = false;
                        break;
                    }
                }
                if (flag)
                    return i;
            }
            return -1;
        }
        public bool Contains(string s)
        {
            return IndexOf(new InputString(s)) > -1;
        }
        public InputString Substring(int index,int count)
        {
            var t = new InputString("");
            for (int i = 0; i < count; i++)
                t.Add(new InputChar(this[index + i].C, this[index + i].I) { Type = this[index + i].Type });
            return t;
        }
        public InputString Substring(int index)
        {
            var t = new InputString("");
            for (int i = index; i < Count; i++)
                t.Add(new InputChar(this[i].C, this[i].I) { Type = this[i].Type });
            return t;
        }
        public void Copy(InputString s)
        {
            if (ReferenceEquals(this, s)) return;
            Clear();
            foreach (var c in s) Add(new InputChar(c.C, c.I) { Type = c.Type });
            _type = s._type;
        }

        public TypeCode GetTypeCode() => TypeCode.String;
        public bool ToBoolean(IFormatProvider provider) => Convert.ToBoolean(ToString(), Invariant);
        public byte ToByte(IFormatProvider provider) => Convert.ToByte(ToString(), Invariant);
        public char ToChar(IFormatProvider provider) => Convert.ToChar(ToString(), Invariant);
        public DateTime ToDateTime(IFormatProvider provider) => Convert.ToDateTime(ToString(), Invariant);
        public decimal ToDecimal(IFormatProvider provider) => decimal.Parse(ToString(), System.Globalization.NumberStyles.Float, Invariant);
        public double ToDouble(IFormatProvider provider) => double.Parse(ToString(), System.Globalization.NumberStyles.Float, Invariant);
        public short ToInt16(IFormatProvider provider) => short.Parse(ToString(), Invariant);
        public int ToInt32(IFormatProvider provider) => int.Parse(ToString(), Invariant);
        public long ToInt64(IFormatProvider provider) => long.Parse(ToString(), Invariant);
        public sbyte ToSByte(IFormatProvider provider) => sbyte.Parse(ToString(), Invariant);
        public float ToSingle(IFormatProvider provider) => float.Parse(ToString(), System.Globalization.NumberStyles.Float, Invariant);
        public string ToString(IFormatProvider provider) => ToString();
        public object ToType(Type conversionType, IFormatProvider provider) => Convert.ChangeType(ToString(), conversionType, Invariant);
        public ushort ToUInt16(IFormatProvider provider) => ushort.Parse(ToString(), Invariant);
        public uint ToUInt32(IFormatProvider provider) => uint.Parse(ToString(), Invariant);
        public ulong ToUInt64(IFormatProvider provider) => ulong.Parse(ToString(), Invariant);
        private static IFormatProvider Invariant => System.Globalization.CultureInfo.InvariantCulture;
    }
}
