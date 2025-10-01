using Microsoft.Extensions.Localization;

namespace IfsahApp.Infrastructure.Services;

// تعريف الـ Interface
public interface IEnumERLocalizer
{
    string LocalizeEnumTitle<TEnum>(TEnum value) where TEnum : Enum;
    string LocalizeEnumDescription<TEnum>(TEnum value) where TEnum : Enum;
}

// تنفيذ الـ Interface
public class EnumERLocalizer : IEnumERLocalizer
{
    private readonly IStringLocalizerFactory _factory;

    public EnumERLocalizer(IStringLocalizerFactory factory)
    {
        _factory = factory;
    }

    private IStringLocalizer GetLocalizer<TEnum>() where TEnum : Enum
    {
        var type = typeof(TEnum);
        var assemblyName = type.Assembly.GetName().Name!;
        return _factory.Create($"Core.Enums.{type.Name}", assemblyName);
    }

    public string LocalizeEnumTitle<TEnum>(TEnum value) where TEnum : Enum
    {
        var localizer = GetLocalizer<TEnum>();
        var key = $"{value}_Title";
        return localizer[key] ?? value.ToString();
    }

    public string LocalizeEnumDescription<TEnum>(TEnum value) where TEnum : Enum
    {
        var localizer = GetLocalizer<TEnum>();
        var key = $"{value}_Description";
        return localizer[key] ?? value.ToString();
    }
}
