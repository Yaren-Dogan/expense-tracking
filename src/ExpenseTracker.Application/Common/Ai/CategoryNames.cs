using ExpenseTracker.Domain.Enums;

namespace ExpenseTracker.Application.Common.Ai;

/// <summary>Turkish category names, used wherever text is shown to a model (prompts, embeddings, tool results).</summary>
public static class CategoryNames
{
    public static string Turkish(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Market => "Market",
        ExpenseCategory.Food => "Yemek",
        ExpenseCategory.Transport => "Ulaşım",
        ExpenseCategory.Bills => "Fatura",
        ExpenseCategory.Entertainment => "Eğlence",
        ExpenseCategory.Health => "Sağlık",
        ExpenseCategory.Clothing => "Giyim",
        _ => "Diğer"
    };

    /// <summary>The text that represents an expense in the vector index.</summary>
    public static string EmbeddingText(ExpenseCategory category, string? note) =>
        string.IsNullOrWhiteSpace(note) ? Turkish(category) : $"{Turkish(category)}: {note}";
}
