using Microsoft.Extensions.Localization;

namespace IfsahApp.Services;

public interface IEnumLocalizer
{
    string LocalizeEnum<TEnum>(TEnum value) where TEnum : Enum;
}

public class EnumLocalizer : IEnumLocalizer
{
    private readonly IStringLocalizerFactory _factory;

    public EnumLocalizer(IStringLocalizerFactory factory)
    {
        _factory = factory;
    }

    public string LocalizeEnum<TEnum>(TEnum value) where TEnum : Enum
    {
        var type = typeof(TEnum);
        var assemblyName = type.Assembly.GetName().Name!;

        // Prefix "Enums." so it looks inside Resources/Enums
        var localizer = _factory.Create($"Enums.{type.Name}", assemblyName);

        return localizer[value.ToString()] ?? value.ToString();
    }
}