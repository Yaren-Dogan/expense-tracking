using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace ExpenseTracker.Application.Common.Ai;

internal static class AiJson
{
    /// <summary>Enums as strings, so a JSON schema sent to the model lists category names, not numbers.</summary>
    public static readonly JsonSerializerOptions Options = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(AIJsonUtilities.DefaultOptions);
        options.Converters.Add(new JsonStringEnumConverter());
        options.MakeReadOnly();
        return options;
    }
}
