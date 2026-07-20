using System.Text.Json;
using System.Text.Json.Serialization;

namespace Payments.Domain.Billing;

/// <summary>
/// Serializes <see cref="SubscriptionStatus"/> as its stable lower-case token (e.g. <c>past_due</c>)
/// instead of the C# member name. Applied via <c>[JsonConverter]</c> on the enum so it overrides the
/// Host's global string-enum converter, keeping the frontend contract independent of enum naming.
/// </summary>
public class SubscriptionStatusJsonConverter : JsonConverter<SubscriptionStatus>
{
    public override SubscriptionStatus Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        SubscriptionStatusTokens.FromToken(reader.GetString() ?? string.Empty);

    public override void Write(
        Utf8JsonWriter writer, SubscriptionStatus value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToToken());
}
