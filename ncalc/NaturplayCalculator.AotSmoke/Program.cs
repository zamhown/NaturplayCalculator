using System;
using JsonParser;
using Naturplay.Tex;
using NaturplayCalculator.Expressions;
using NaturplayCalculator.History;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;

// Exercise the portable dependency graph after trimming and native compilation.
var engine = new CalculationEngine();
var result = engine.Evaluate("1/3+1/6", new CalculationOptions { Mode = CalculationMode.Symbolic });
Require(result.Value.Code == "1/2", "Exact calculation");
Require(engine.Evaluate("2^3^2").Value.Code == "512", "Numeric calculation");
var session = new CalculatorSession { Draft = "1/3+1/6", Answer = result.Value, Mode = CalculationMode.Symbolic };
var restored = SessionCodec.Read(SessionCodec.Write(session));
Require(restored.Answer.Code == "1/2" && restored.Draft == session.Draft, "History codec");
const string escaped = "C:\\tmp\\\"中文😀\n";
Require(StringHelper.JsonStringToString(StringHelper.StringToJsonString(escaped)) == escaped, "JSON scalar codec");
Require(JSON.Stringify(JSON.Parse("{name:'value'}")) == "{\"name\":\"value\"}", "JSON compatibility");
var tex = new ExpressionTexFormatter().Format(result.Value.Expression, true);
var layout = new TexLayoutEngine().Render(tex.Latex);
Require(layout.Commands.Count > 0, "TeX layout");
var caretMap = new FormulaCaretMap(session.Draft, tex, layout);
Require(caretMap.Slots.Count > 0, "Portable caret mapping");
Console.WriteLine("Native AOT smoke passed: calculation, history, JSON, TeX and caret mapping.");

static void Require(bool condition, string scenario)
{
    if (!condition) throw new InvalidOperationException(scenario + " failed.");
}
