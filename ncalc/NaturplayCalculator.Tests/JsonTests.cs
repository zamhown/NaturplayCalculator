using System;
using System.Globalization;
using JsonParser;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class JsonTests
    {
        [Theory]
        [InlineData(@"C:\tmp\")]
        [InlineData("a\\b\t\n\r\0\"中文😀")]
        [InlineData("\\\\\\")]
        public void StringsRoundTripWithoutLoss(string text)
        {
            Assert.Equal(text, StringHelper.JsonStringToString(StringHelper.StringToJsonString(text)));
            var source = new JsonObject(); source.Add("path", text);
            var restored = (JsonObject)JSON.Parse(JSON.Stringify(source));
            Assert.Equal(text, ((JsonValue)restored.GetValue("path")).Value);
        }
        [Fact]
        public void ValidEscapedTrailingBackslashParses()
        {
            var value = (JsonObject)JSON.Parse("{\"path\":\"C:\\\\tmp\\\\\"}");
            Assert.Equal(@"C:\tmp\", ((JsonValue)value.GetValue("path")).Value);
        }
        [Fact]
        public void JsonNumbersAndDatesKeepTheirMeaning()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
                Assert.Equal("{\"n\":1.5,\"id\":9007199254740993,\"date\":\"2026-11-12\"}", JSON.Stringify(JSON.Parse("{\"n\":1.5,\"id\":9007199254740993,\"date\":\"2026-11-12\"}")));
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Theory]
        [InlineData("{} garbage")][InlineData("{\"x\":\"bad\\q\"}")][InlineData("NaN")][InlineData("Infinity")][InlineData("[1,2")]
        public void InvalidJsonFailsExplicitly(string text) { Assert.ThrowsAny<Exception>(() => JSON.Parse(text)); }
        [Fact]
        public void InputLimitsAreEnforced()
        {
            Assert.ThrowsAny<Exception>(() => JSON.Parse(new string('[', 100) + "1" + new string(']',100)));
            Assert.Throws<FormatException>(() => JSON.Parse(new string(' ', JSON.MaxInputLength+1)));
        }
        [Fact]
        public void ExistingSingleQuotedAndUnquotedKeySyntaxStillWorks()
        {
            Assert.Equal("{\"name\":\"value\"}", JSON.Stringify(JSON.Parse("{name:'value'}")));
        }
        [Fact]
        public void StreamingCodecKeepsCommentsDuplicatesAndLargeIntegersCompatible()
        {
            const string json = "/*before*/{a:1,a:2,values:[/*inside*/null,true,9223372036854775808]}/*after*/";
            Assert.Equal("{\"a\":2,\"values\":[null,true,9223372036854775808]}", JSON.Stringify(JSON.Parse(json)));
        }
        [Theory]
        [InlineData("{")][InlineData("[1,")][InlineData("{a:")][InlineData("undefined")]
        public void StreamingCodecRejectsIncompleteOrUnsupportedValues(string text)
        {
            Assert.ThrowsAny<Exception>(() => JSON.Parse(text));
        }
    }
}
