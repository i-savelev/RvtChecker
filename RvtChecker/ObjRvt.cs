using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace RvtChecker
{
    /// <summary>
    /// Универсальный класс-обёртка для элемента Revit.
    /// Используется для чтения параметров и проверки условий.
    /// </summary>
    public class ObjRvt
    {
        /// <summary>
        /// Элемент Revit.
        /// </summary>
        public Element elem;

        /// <summary>
        /// Возвращает значение параметра как строку.
        /// Сначала ищет параметр в экземпляре, затем в типе элемента.
        /// </summary>
        /// <param name="parName">Имя параметра.</param>
        /// <returns>Строковое значение параметра или пустая строка.</returns>
        public virtual string GetParamAsString(string parName)
        {
            var parameter = FindParameter(parName);
            return GetParameterValueAsString(parameter);
        }

        /// <summary>
        /// Проверяет одно условие для элемента.
        /// </summary>
        /// <param name="condition">Условие параметра.</param>
        /// <returns>True, если элемент проходит условие.</returns>
        public virtual bool CheckCondition(ObjParamCondition condition)
        {
            if (condition == null)
                return true;

            string conditionName = condition.Condition?.Trim().ToLowerInvariant() ?? "";
            string expectedValue = condition.ParamValue?.Trim() ?? "";
            string actualValue = GetParamAsString(condition.ParamName);

            switch (conditionName)
            {
                case "имеет значение":
                    return HasValue(actualValue);

                case "равно":
                    return string.Equals(
                        actualValue?.Trim() ?? "",
                        expectedValue,
                        StringComparison.OrdinalIgnoreCase);

                case "содержит":
                    if (actualValue == null)
                        return false;

                    return actualValue.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) >= 0;

                case "не содержит":
                    if (actualValue == null)
                        return true;

                    return actualValue.IndexOf(expectedValue, StringComparison.OrdinalIgnoreCase) < 0;

                case "не равно":
                    return !string.Equals(
                        actualValue?.Trim() ?? "",
                        expectedValue,
                        StringComparison.OrdinalIgnoreCase);

                default:
                    return false;
            }
        }

        /// <summary>
        /// Проверяет список условий с учётом логических операторов "и" / "или".
        /// </summary>
        /// <param name="conditions">Список условий.</param>
        /// <returns>True, если элемент проходит все условия.</returns>
        public virtual bool CheckConditions(IList<ObjParamCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return true;

            var andResults = new List<bool>();
            var orResults = new List<bool>();

            foreach (var condition in conditions)
            {
                if (condition == null)
                    continue;

                bool result = CheckCondition(condition);

                string boolCondition = condition.BoolCondition?.Trim().ToLowerInvariant();

                if (boolCondition == "или")
                    orResults.Add(result);
                else
                    andResults.Add(result);
            }

            bool andPassed = andResults.Count == 0 || andResults.All(x => x);
            bool orPassed = orResults.Count == 0 || orResults.Any(x => x);

            return andPassed && orPassed;
        }

        /// <summary>
        /// Проверяет, есть ли в строке значения полезное значение.
        /// Пустая строка и "none" считаются отсутствием значения.
        /// </summary>
        /// <param name="value">Строковое значение параметра.</param>
        /// <returns>True, если значение присутствует.</returns>
        private bool HasValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (value.Trim().Equals("none", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Ищет параметр сначала в экземпляре, затем в типе элемента.
        /// </summary>
        /// <param name="parName">Имя параметра.</param>
        /// <returns>Найденный параметр или null.</returns>
        private Parameter FindParameter(string parName)
        {
            if (elem == null || string.IsNullOrWhiteSpace(parName))
                return null;

            var parameter = elem.LookupParameter(parName);
            if (parameter != null)
                return parameter;

            var typeId = elem.GetTypeId();
            if (typeId == null)
                return null;

            var type = elem.Document.GetElement(typeId);
            if (type == null)
                return null;

            return type.LookupParameter(parName);
        }

        /// <summary>
        /// Возвращает строковое значение параметра с учётом типа хранения.
        /// </summary>
        /// <param name="parameter">Параметр элемента.</param>
        /// <returns>Строковое значение параметра или пустая строка.</returns>
        private string GetParameterValueAsString(Parameter parameter)
        {
            if (parameter == null)
                return "";

            if (!parameter.HasValue)
                return "";

            switch (parameter.StorageType)
            {
                case StorageType.String:
                    return parameter.AsString() ?? parameter.AsValueString() ?? "";

                case StorageType.Double:
                    return parameter.AsValueString() ?? parameter.AsDouble().ToString();

                case StorageType.Integer:
                    return parameter.AsValueString() ?? parameter.AsInteger().ToString();

                case StorageType.ElementId:
                    return parameter.AsValueString() ?? "";

                default:
                    return "";
            }
        }
    }
}