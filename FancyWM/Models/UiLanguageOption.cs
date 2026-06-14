namespace FancyWM.Models
{
    public sealed class UiLanguageOption(string id, string displayName)
    {
        public string Id { get; } = id;

        public string DisplayName { get; } = displayName;

        public override string ToString() => DisplayName;
    }
}
