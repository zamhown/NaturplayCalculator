namespace NaturplayMath.Exceptions;

public sealed class CalculationBudgetExceededException : Exception
{
    public CalculationBudgetExceededException() : base("The requested operation budget was exhausted.") { }
}
