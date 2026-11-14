using System;
using System.Globalization;
using NaturplayCalculator.History;
using NaturplayCalculator.Input;
using NaturplayCalculator.Model;
using Xunit;

namespace NaturplayCalculator.Tests
{
    public class SessionAndInputTests
    {
        [Fact]
        public void RestoredAnsExpressionKeepsOriginalResultAndContext()
        {
            var engine = new CalculationEngine();
            var previous = engine.Evaluate("2+3").Answer;
            var answer = engine.Evaluate("Ans*2", new CalculationOptions { PreviousAnswer = previous }).Answer;
            var session = new CalculatorSession { Draft = "Ans*2", Answer = answer, DraftPreviousAnswer = previous, DraftUseRadians = false };
            session.History.Add(new HistoryRecord { Input = "Ans*2", Answer = answer, PreviousAnswer = previous, UseRadians = false });
            var restored = SessionCodec.Read(SessionCodec.Write(session));
            Assert.Equal(10, restored.Answer.Cmx.Re);
            Assert.Equal(5, restored.DraftPreviousAnswer.Cmx.Re);
            Assert.False(restored.DraftUseRadians);
            var record = Assert.Single(restored.History);
            Assert.Equal(10, engine.Evaluate(record.Input, new CalculationOptions { PreviousAnswer = record.PreviousAnswer, UseRadians = record.UseRadians }).Answer.Cmx.Re);
        }
        [Theory]
        [InlineData("de-DE")][InlineData("fr-FR")]
        public void ComplexMatrixHistoryRoundTripsAcrossCultures(string culture)
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                var answer = new CalculationEngine().Evaluate("(1E-20i,2.5,,3,4)").Answer;
                var restored = SessionCodec.Read(SessionCodec.Write(new CalculatorSession { Answer = answer }));
                Assert.Equal(answer.ToString(), restored.Answer.ToString());
            }
            finally { CultureInfo.CurrentCulture = previous; }
        }
        [Fact]
        public void LegacyEntriesArePreservedWithoutInventingContext()
        {
            var session = new CalculatorSession();
            session.History.Add(new HistoryRecord { Input = "Ans*2", Kind = "legacy" });
            var record = Assert.Single(SessionCodec.Read(SessionCodec.Write(session)).History);
            Assert.False(record.HasCalculationContext); Assert.Null(record.Answer); Assert.Null(record.PreviousAnswer);
        }
        [Fact]
        public void HistoryIsBoundedAndUnknownSchemaIsRejected()
        {
            var session = new CalculatorSession();
            for (int i = 0; i < 150; i++) session.History.Add(new HistoryRecord { Input = i.ToString() });
            var serialized = SessionCodec.Write(session);
            Assert.Equal(100, SessionCodec.Read(serialized).History.Count);
            Assert.Throws<FormatException>(() => SessionCodec.Read(serialized.Replace("\"version\":" + SessionCodec.Version, "\"version\":99")));
            Assert.ThrowsAny<Exception>(() => SessionCodec.Read("{broken"));
        }

        [Theory]
        [InlineData(false)][InlineData(true)]
        public void DraftResultIsStoredExplicitlyEvenWhenItsTextMatchesHistory(bool solved)
        {
            var session = new CalculatorSession
            {
                Draft = "Ans*2", Answer = new MathValue(10), DraftPreviousAnswer = new MathValue(10),
                DraftIsSolved = solved, DraftAnswer = solved ? new MathValue(20) : null
            };
            session.History.Add(new HistoryRecord { Input = "Ans*2", Answer = new MathValue(10), PreviousAnswer = new MathValue(5) });
            var restored = SessionCodec.Read(SessionCodec.Write(session));
            Assert.Equal(solved, restored.DraftIsSolved);
            Assert.Equal(solved ? (double?)20 : null, restored.DraftAnswer?.Cmx.Re);
            Assert.Equal(10, Assert.Single(restored.History).Answer.Cmx.Re);
        }
        [Theory]
        [InlineData("123", 0, 0, false, "23", 0)]
        [InlineData("123", 2, 0, false, "12", 2)]
        [InlineData("123", 3, 0, false, "123", 3)]
        [InlineData("123", 0, 0, true, "123", 0)]
        [InlineData("123", 3, 0, true, "12", 2)]
        [InlineData("12345", 1, 3, false, "15", 1)]
        [InlineData("a😀b", 1, 0, false, "ab", 1)]
        [InlineData("a😀b", 3, 0, true, "ab", 1)]
        public void DeleteUsesCaretAndSelection(string text, int caret, int selection, bool backward, string expected, int expectedCaret)
        {
            var result = InputEditor.Delete(text, caret, selection, backward);
            Assert.Equal(expected, result.Text); Assert.Equal(expectedCaret, result.Caret);
        }
        [Theory]
        [InlineData("2+3", "2+34", "4")]
        [InlineData("2+3", "2+3*2", "Ans*2")]
        [InlineData("2+3", "4", "4")]
        public void TypingAfterAnAnswerBeginsAConsistentNewCalculation(string before, string candidate, string expected)
        {
            var edit = InputEditor.ApplyTypedText(before, candidate, candidate.Length, true);
            Assert.Equal(expected, edit.Text); Assert.Equal(expected.Length, edit.Caret); Assert.True(edit.StartsNewCalculation);
        }
        [Theory]
        [InlineData("ncalc://1/2", "1/2")]
        [InlineData("ncalc://calculate?expression=1%2B2", "1+2")]
        [InlineData("ncalc://calculate?expression=(1,2,,3,4)%5ET", "(1,2,,3,4)^T")]
        public void ActivationPreservesTheCompleteExpression(string uri, string expected)
        {
            Assert.Equal(expected, ActivationInput.Parse(new Uri(uri)));
        }

        [Theory]
        [InlineData("12345", 1, 3, "ab", "1ab5", 3)]
        [InlineData("123", 1, 0, "+", "1+23", 2)]
        [InlineData("123", 0, 3, "4", "4", 1)]
        public void KeypadAndPasteReplaceTheSelectedRange(string text, int caret, int length, string inserted, string expected, int end)
        {
            var result = InputEditor.Insert(text, caret, length, inserted);
            Assert.Equal(expected, result.Text); Assert.Equal(end, result.Caret);
        }
    }
}
