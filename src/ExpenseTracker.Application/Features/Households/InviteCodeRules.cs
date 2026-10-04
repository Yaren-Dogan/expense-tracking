using ExpenseTracker.Domain.Households;
using FluentValidation;

namespace ExpenseTracker.Application.Features.Households;

internal static class InviteCodeRules
{
    public static IRuleBuilderOptions<T, string> ValidInviteCode<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(InviteCode.IsWellFormed).WithMessage("Davet kodu 8 karakter olmalı (harf ve rakam).");
}
