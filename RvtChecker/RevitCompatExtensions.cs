using System;
using System.Collections.Generic;
using System.Reflection;
using Autodesk.Revit.DB;


namespace RvtChecker
{
    /// <summary>
    /// Extension методы для кросс-версионной совместимости Revit API.
    /// Кэширование PropertyInfo выполняется отдельно для каждого типа (ElementId, WorksetId и т.д.),
    /// чтобы избежать TargetException при вызове GetValue на несовместимых типах.
    /// </summary>
    public static class RevitCompatExtensions
    {
        private class PropertyInfoCache
        {
            public PropertyInfo ValueProp;
            public PropertyInfo IntProp;
        }

        private static readonly Dictionary<Type, PropertyInfoCache> _cache = new Dictionary<Type, PropertyInfoCache>();
        private static readonly object _lock = new object();

        private static PropertyInfoCache GetCache(Type idType)
        {
            lock (_lock)
            {
                if (!_cache.TryGetValue(idType, out var cache))
                {
                    cache = new PropertyInfoCache
                    {
                        ValueProp = idType.GetProperty("Value"),       // Revit 2024+
                        IntProp = idType.GetProperty("IntegerValue")   // Revit <= 2023
                    };
                    _cache[idType] = cache;
                }
                return cache;
            }
        }

        /// <summary>
        /// Безопасно возвращает числовое значение ID элемента для любой версии Revit.
        /// </summary>
        /// <param name="id">ElementId для преобразования.</param>
        /// <returns>Числовое значение ID или -1 если null.</returns>
        public static long GetIdValue(this ElementId id)
        {
            if (id == null) return -1;
            return GetIdValueInternal(id, id.GetType());
        }

        /// <summary>
        /// Безопасно возвращает числовое значение WorksetId для любой версии Revit.
        /// В Revit API WorksetId не наследуется от ElementId, поэтому требуется отдельная перегрузка.
        /// </summary>
        /// <param name="id">WorksetId для преобразования.</param>
        /// <returns>Числовое значение ID или -1 если null.</returns>
        public static long GetIdValue(this WorksetId id)
        {
            if (id == null) return -1;
            return GetIdValueInternal(id, id.GetType());
        }

        private static long GetIdValueInternal(object id, Type idType)
        {
            var cache = GetCache(idType);

            if (cache.ValueProp != null)
            {
                var val = cache.ValueProp.GetValue(id);
                if (val is long l) return l;
                if (val is int i) return i;
            }

            if (cache.IntProp != null)
            {
                var val = cache.IntProp.GetValue(id);
                if (val is int i) return i;
            }

            return id.GetHashCode(); // Fallback
        }
    }
}