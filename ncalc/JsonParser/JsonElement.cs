using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace JsonParser
{
    /// <summary>
    /// Json对象类型
    /// </summary>
    public enum JsonDataType
    {
        Number,
        String,
        Boolean,
        Array,
        Object,
        Null
    }

    /// <summary>
    /// Json元素模拟基类
    /// </summary>
    public abstract class JsonElement
    {
        /// <summary>
        /// 对象类型
        /// </summary>
        public JsonDataType Type { get; protected set; }

        protected JsonElement(JsonDataType type)
        {
            Type = type;
        }
    }

    /// <summary>
    /// Json中的单值模型类
    /// </summary>
    public class JsonValue : JsonElement
    {
        private string valueString = "null";
        private object obj = null;
        /// <summary>
        /// 值
        /// </summary>
        public object Value
        {
            get { return obj; }
            set
            {
                obj = value;
                if (obj == null)
                {
                    valueString = "null";
                    Type = JsonDataType.Null;
                }
                else if (obj is string)
                {
                    valueString = obj.ToString();
                    Type = JsonDataType.String;
                }
                else if (obj is bool)
                {
                    valueString = ((bool)obj) ? "true" : "false";
                    Type = JsonDataType.Boolean;
                }
                else
                {
                    if ((obj is double d && (double.IsNaN(d) || double.IsInfinity(d))) || (obj is float f && (float.IsNaN(f) || float.IsInfinity(f))))
                        throw new FormatException("Non-finite numbers are not JSON values.");
                    if (!(obj is byte || obj is sbyte || obj is short || obj is ushort || obj is int || obj is uint || obj is long || obj is ulong || obj is float || obj is double || obj is decimal || obj is System.Numerics.BigInteger))
                        throw new ArgumentException("Unsupported JSON value type.", nameof(value));
                    valueString = Convert.ToString(obj, System.Globalization.CultureInfo.InvariantCulture);
                    Type = JsonDataType.Number;
                }
            }
        }

        public JsonValue(object obj) : base(JsonDataType.Null)
        {
            Value = obj;
        }

        public override string ToString() => JSON.Stringify(this);

    }

    /// <summary>
    /// Json中的对象模型类
    /// </summary>
    public class JsonObject : JsonElement
    {
        /// <summary>
        /// 键值对列表
        /// </summary>
        public List<JsonKeyValue> KeyValue { get; private set; }

        public JsonObject() : base(JsonDataType.Object)
        {
            KeyValue = new List<JsonKeyValue>();
        }

        /// <summary>
        /// 根据Key获取键值对
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public JsonKeyValue GetKeyValue(string key)
        {
            foreach (JsonKeyValue kv in KeyValue)
                if (kv.Key.Equals(key))
                    return kv;
            return null;
        }

        /// <summary>
        /// 向对象类型添加键值对
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, object value)
        {
            Add(key, new JsonValue(value));
        }

        /// <summary>
        /// 向对象类型添加键值对
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        public void Add(string key, JsonElement value)
        {
            var kv = GetKeyValue(key);
            if (kv != null)
                kv.Value = value;
            else
                KeyValue.Add(new JsonKeyValue(key, value));
        }

        /// <summary>
        /// 根据Key获取Value
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public JsonElement GetValue(string key)
        {
            var kv = GetKeyValue(key);
            if (kv != null)
                return kv.Value;
            return null;
        }

        /// <summary>
        /// 删除键值对
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public bool Remove(string key)
        {
            var kv = GetKeyValue(key);
            if (kv != null)
            {
                KeyValue.Remove(kv);
                return true;
            }
            return false;
        }

        public override string ToString() => JSON.Stringify(this);

    }

    /// <summary>
    /// Json中的数组模型类
    /// </summary>
    public class JsonArray : JsonElement
    {
        /// <summary>
        /// 数组成员
        /// </summary>
        public List<JsonElement> Elements { get; private set; }

        public JsonArray() : base(JsonDataType.Array)
        {
            Elements = new List<JsonElement>();
        }

        /// <summary>
        /// 向集合类型添加成员
        /// </summary>
        /// <param name="obj"></param>
        public void Push(object obj)
        {
            Push(new JsonValue(obj));
        }

        /// <summary>
        /// 向集合类型添加成员
        /// </summary>
        /// <param name="obj"></param>
        public void Push(JsonElement obj)
        {
            Elements.Add(obj);
        }

        public override string ToString() => JSON.Stringify(this);

    }

    /// <summary>
    /// Json表示对象属性的键值对模型类
    /// </summary>
    public class JsonKeyValue
    {
        public string Key { get; private set; }
        public JsonElement Value { get; set; }

        public JsonKeyValue(string key, JsonElement value)
        {
            Key = key;
            Value = value;
        }

        public override string ToString()
        {
            return "\"" + StringHelper.StringToJsonString(Key) + "\":" + Value.ToString();
        }
    }
}
