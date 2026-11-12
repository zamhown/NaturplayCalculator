using System;
using System.Globalization;
using JsonParser;
using NaturplayCalculator.Model;
using NaturplayCalculator.Expressions;
using System.Linq;

namespace NaturplayCalculator.History
{
    public static class SessionCodec
    {
        public const int Version = 3;
        public const int MaxFileLength = 16 * 1024 * 1024;

        public static string Write(CalculatorSession session)
        {
            session.Trim();
            var root = new JsonObject();
            root.Add("version", Version);
            root.Add("mode", session.Mode.ToString());
            root.Add("draftMode", session.DraftMode.ToString());
            root.Add("draft", session.Draft);
            root.Add("answer", WriteValue(session.Answer));
            root.Add("draftPreviousAnswer", WriteValue(session.DraftPreviousAnswer));
            root.Add("draftUseRadians", session.DraftUseRadians);
            root.Add("draftIsSolved", session.DraftIsSolved);
            root.Add("draftAnswer", WriteValue(session.DraftAnswer));
            var history = new JsonArray();
            foreach (var record in session.History)
            {
                var item = new JsonObject();
                item.Add("input", record.Input);
                item.Add("kind", record.Kind);
                item.Add("mode", record.Mode.ToString());
                item.Add("answer", WriteValue(record.Answer));
                item.Add("previousAnswer", WriteValue(record.PreviousAnswer));
                item.Add("useRadians", record.UseRadians);
                item.Add("createdAt", record.CreatedAt.ToString("O", CultureInfo.InvariantCulture));
                history.Push(item);
            }
            root.Add("history", history);
            var text = JSON.Stringify(root);
            if (text.Length > MaxFileLength) throw new FormatException("Session exceeds its storage limit.");
            return text;
        }

        public static CalculatorSession Read(string text)
        {
            var root = JSON.Parse(text, MaxFileLength) as JsonObject ?? throw new FormatException("Invalid session.");
            double version = Number(root, "version");
            if (version != 2 && version != Version) throw new FormatException("Unsupported session version.");
            var session = new CalculatorSession
            {
                Mode = version == 2 ? CalculationMode.Numeric : ReadMode(root, "mode"),
                DraftMode = version == 2 ? CalculationMode.Numeric : ReadMode(root, "draftMode"),
                Draft = Text(root, "draft"),
                Answer = ReadValue(root.GetValue("answer")) ?? new MathValue(0),
                DraftPreviousAnswer = ReadValue(root.GetValue("draftPreviousAnswer")) ?? new MathValue(0),
                DraftUseRadians = Boolean(root, "draftUseRadians"),
                DraftIsSolved = Boolean(root, "draftIsSolved"),
                DraftAnswer = ReadValue(root.GetValue("draftAnswer"))
            };
            var history = root.GetValue("history") as JsonArray ?? throw new FormatException("Missing history.");
            if (history.Elements.Count > CalculatorSession.MaxHistory) throw new FormatException("Too many history entries.");
            foreach (var value in history.Elements)
            {
                var item = value as JsonObject ?? throw new FormatException("Invalid history entry.");
                var record = new HistoryRecord
                {
                    Input = Text(item, "input"), Kind = Text(item, "kind"),
                    Mode = version == 2 ? CalculationMode.Numeric : ReadMode(item, "mode"),
                    Answer = ReadValue(item.GetValue("answer")), PreviousAnswer = ReadValue(item.GetValue("previousAnswer")),
                    UseRadians = Boolean(item, "useRadians"),
                    CreatedAt = DateTimeOffset.Parse(Text(item, "createdAt"), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                };
                if (record.Kind != "math" && record.Kind != "json" && record.Kind != "xml" && record.Kind != "legacy") throw new FormatException("Unknown history kind.");
                session.History.Add(record);
            }
            session.Trim();
            return session;
        }

        private static JsonElement WriteValue(CalculationValue value)
        {
            if (value == null) return new JsonValue(null);
            if (value.Numeric != null)
            {
                var numeric = WriteNumeric(value.Numeric);
                if (value.Conditions.Count > 0 && numeric is JsonObject number)
                {
                    var constraints = new JsonArray(); foreach (string condition in value.Conditions) constraints.Push(condition);
                    number.Add("conditions", constraints);
                }
                return numeric;
            }
            var result = new JsonObject();
            result.Add("kind", "symbolic"); result.Add("expression", value.Code);
            var conditions = new JsonArray(); foreach (string condition in value.Conditions) conditions.Push(condition);
            result.Add("conditions", conditions);
            return result;
        }

        private static CalculationValue ReadValue(JsonElement value)
        {
            if (value is JsonValue empty && empty.Type == JsonDataType.Null) return null;
            if (value is JsonObject obj && obj.GetValue("kind") is JsonValue kind && (string)kind.Value == "symbolic")
            {
                string code = Text(obj, "expression");
                var expression = new ExpressionParser().ParseStoredResult(code).Root;
                if (HasReservedAnswer(expression)) throw new FormatException("Stored expressions cannot reference Ans or placeholders.");
                var list = obj.GetValue("conditions") as JsonArray ?? throw new FormatException("Missing expression conditions.");
                if (list.Elements.Count > 64) throw new FormatException("Too many expression conditions.");
                return CalculationValue.FromSymbolic(expression, list.Elements.Select(item => item is JsonValue text && text.Type == JsonDataType.String ? (string)text.Value : throw new FormatException("Invalid expression condition.")));
            }
            var numericValue = ReadNumeric(value);
            if (value is JsonObject numeric && numeric.GetValue("conditions") is JsonArray constraints)
                return CalculationValue.FromNumeric(numericValue, constraints.Elements.Select(item => item is JsonValue text && text.Type == JsonDataType.String ? (string)text.Value : throw new FormatException("Invalid expression condition.")));
            return numericValue;
        }
        private static bool HasReservedAnswer(ExpressionNode node) => node.Kind == ExpressionKind.Symbol && (node.Text == "Ans" || node.Text == "?") || node.Children.Any(HasReservedAnswer);
        private static CalculationMode ReadMode(JsonObject obj, string key)
        {
            if (!Enum.TryParse(Text(obj, key), out CalculationMode mode) || !Enum.IsDefined(typeof(CalculationMode), mode)) throw new FormatException("Invalid calculation mode.");
            return mode;
        }
        private static JsonElement WriteNumeric(MathValue value)
        {
            if (value == null || value.IsNull) return new JsonValue(null);
            var result = new JsonObject();
            if (!value.IsMatrix) { result.Add("real", value.Cmx.Re); result.Add("imaginary", value.Cmx.Im); }
            else
            {
                var rows = new JsonArray();
                for (int i = 0; i < value.Mtr.n; i++)
                {
                    var row = new JsonArray();
                    for (int j = 0; j < value.Mtr.m; j++) row.Push(WriteNumeric(value.Mtr[i][j].Value));
                    rows.Push(row);
                }
                result.Add("matrix", rows);
            }
            return result;
        }

        private static MathValue ReadNumeric(JsonElement value)
        {
            if (value is JsonValue empty && empty.Type == JsonDataType.Null) return null;
            var obj = value as JsonObject ?? throw new FormatException("Invalid stored number.");
            var rows = obj.GetValue("matrix") as JsonArray;
            if (rows == null) return new MathValue(new Complex(Number(obj, "real"), Number(obj, "imaginary")));
            if (rows.Elements.Count < 1 || rows.Elements.Count > ExpressionLimits.MaxMatrixDimension) throw new FormatException("Invalid matrix size.");
            var first = rows.Elements[0] as JsonArray ?? throw new FormatException("Invalid matrix row.");
            if (first.Elements.Count < 1 || first.Elements.Count > ExpressionLimits.MaxMatrixDimension) throw new FormatException("Invalid matrix size.");
            var matrix = new Matrix(rows.Elements.Count, first.Elements.Count);
            for (int i = 0; i < matrix.n; i++)
            {
                var row = rows.Elements[i] as JsonArray;
                if (row == null || row.Elements.Count != matrix.m) throw new FormatException("Invalid matrix shape.");
                for (int j = 0; j < matrix.m; j++)
                {
                    var entry = ReadNumeric(row.Elements[j]);
                    if (entry == null || entry.IsMatrix) throw new FormatException("Invalid matrix entry.");
                    matrix[i][j] = new Element(entry);
                }
            }
            return new MathValue(matrix);
        }
        private static string Text(JsonObject obj, string name)
        {
            var value = obj.GetValue(name) as JsonValue;
            if (value?.Type != JsonDataType.String) throw new FormatException("Missing text field: " + name);
            var text = (string)value.Value;
            if (text.Length > JSON.MaxInputLength) throw new FormatException("Text field exceeds its limit.");
            return text;
        }
        private static double Number(JsonObject obj, string name)
        {
            var value = obj.GetValue(name) as JsonValue;
            if (value?.Type != JsonDataType.Number) throw new FormatException("Missing numeric field: " + name);
            return Convert.ToDouble(value.Value, CultureInfo.InvariantCulture);
        }
        private static bool Boolean(JsonObject obj, string name)
        {
            var value = obj.GetValue(name) as JsonValue;
            if (value?.Type != JsonDataType.Boolean) throw new FormatException("Missing boolean field: " + name);
            return (bool)value.Value;
        }
    }
}
