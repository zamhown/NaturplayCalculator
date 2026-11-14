using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Xml;
using JsonParser;

namespace NaturplayCalculator.Model
{
    public sealed class ContentResult
    {
        public CalculationResult Calculation { get; internal set; }
        public JsonElement Json { get; internal set; }
        public XmlDocument Xml { get; internal set; }
        public string Kind => Calculation != null ? "math" : Json != null ? "json" : "xml";
    }
    public sealed class ContentEvaluator
    {
        public ContentResult Evaluate(string text, CalculationOptions options, CancellationToken token)
        {
            if (text == null) throw new ArgumentNullException(nameof(text));
            if (text.Length > JSON.MaxInputLength) ExpressionLimits.Fail();
            token.ThrowIfCancellationRequested();
            Exception calculationError;
            try { return new ContentResult { Calculation = new CalculationEngine().Evaluate(text, options, token) }; }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { calculationError = ex; }
            try
            {
                var json = JSON.Parse(text);
                int remaining = 2048;
                CheckJson(json, ref remaining, token);
                return new ContentResult { Json = json };
            }
            catch (OperationCanceledException) { throw; }
            catch (MathException) { throw; }
            catch (Exception) { /* A non-JSON expression may still be XML. */ }
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = JSON.MaxInputLength };
                using (var reader = XmlReader.Create(new StringReader(text), settings))
                {
                    int count = 0;
                    while (reader.Read())
                    {
                        token.ThrowIfCancellationRequested();
                        // Count attribute rows and grouping nodes rendered by the tree view.
                        count += 2 + reader.AttributeCount + (reader.HasAttributes ? 1 : 0);
                        if (count > 2048 || reader.Depth > 64) ExpressionLimits.Fail();
                    }
                }
                var document = new XmlDocument { XmlResolver = null };
                using (var reader = XmlReader.Create(new StringReader(text), settings)) document.Load(reader);
                return new ContentResult { Xml = document };
            }
            catch (OperationCanceledException) { throw; }
            catch (MathException) { throw; }
            catch (XmlException) { }
            ExceptionDispatchInfo.Capture(calculationError).Throw();
            throw new InvalidOperationException();
        }
        private static void CheckJson(JsonElement value, ref int remaining, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (--remaining < 0) ExpressionLimits.Fail();
            if (value is JsonArray array) foreach (var child in array.Elements) CheckJson(child, ref remaining, token);
            if (value is JsonObject obj) foreach (var property in obj.KeyValue) CheckJson(property.Value, ref remaining, token);
        }
    }
}
