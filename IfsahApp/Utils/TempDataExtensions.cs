using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Newtonsoft.Json; 

namespace IfsahApp.Utils
{
    public static class TempDataExtensions
    {
        public static void Set<T>(this ITempDataDictionary tempData, string key, T value)
        {
            tempData[key] = JsonConvert.SerializeObject(value); // ✅
        }

        public static T? Get<T>(this ITempDataDictionary tempData, string key)
        {
            if (tempData.TryGetValue(key, out object? o) && o is string json)
            {
                return JsonConvert.DeserializeObject<T>(json); // ✅
            }
            return default;
        }
    }
}
