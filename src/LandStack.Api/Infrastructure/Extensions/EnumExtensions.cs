using System;

namespace LandStack.Api.Infrastructure.Extensions
{
    public static class EnumExtensions
    {
        public static string ToCamelCaseString(this Enum e)
        {
            var result = e.ToString();

            if (result != string.Empty && char.IsUpper(result[0]))
            {
                result = char.ToLower(result[0]) + result.Substring(1);
            }

            return result;
        }
    }
}