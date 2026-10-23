using FluentValidation;
namespace TechSpherex.CleanArchitecture.Application.Features.RepairCases.Estimates;

/*
    Field rules shared by the estimate validators; lengths follow RepairEstimateConfiguration and
    RepairEstimateLineConfiguration. Currency: VND or USD (the depot quotes in these two, inference).
*/
public static class RepairEstimateRules
{
    public static readonly string[] Currencies = ["VND", "USD"];

    // 999 999 999 999 999.99 fits numeric(18,2); a single item above this is a typing mistake
    public const decimal MaxAmount = 999_999_999_999m;

    public static IRuleBuilderOptions<T, string> EstimateCurrency<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .Must(c => c is not null && Currencies.Contains(c.Trim().ToUpperInvariant()))
            .WithMessage($"Currency must be one of: {string.Join(", ", Currencies)}.");
}

public sealed class RepairEstimateLineInputValidator : AbstractValidator<RepairEstimateLineInput>
{
    public RepairEstimateLineInputValidator()
    {
        RuleFor(x => x.DamageDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.RepairMethod).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).InclusiveBetween(0m, RepairEstimateRules.MaxAmount);
        RuleFor(x => x.Payer).IsInEnum();
        RuleFor(x => x.PayerName).MaximumLength(200);
    }
}
