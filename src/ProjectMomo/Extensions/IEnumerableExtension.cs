using System.Collections.Generic;
using System.Linq;

namespace ProjectMomo.Extensions
{
    public static class IEnumerableExtension
    {
        public static IEnumerable<T> OrEmptyIfNull<T>(this IEnumerable<T> collection)
        {
            return collection ?? Enumerable.Empty<T>();
        }
    }
}