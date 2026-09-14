using System.Collections.Generic;
using System.Linq;

namespace RvtChecker
{
    /// <summary>
    /// Модель одного набора проверки параметров.
    /// Содержит правила поиска элементов и проверяемые параметры.
    /// </summary>
    /// <summary>
    /// Модель набора проверки.
    /// Если уже есть отдельный файл ObjCheck.cs, удалить этот класс из данного файла.
    /// </summary>
    public class ObjCheck
    {
        /// <summary>
        /// Название проверки.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Категории для поиска элементов.
        /// </summary>
        public List<ObjCategory> Categories { get; set; } = new List<ObjCategory>();

        /// <summary>
        /// Параметры для поиска элементов.
        /// </summary>
        public List<ObjParamCondition> Conditions { get; set; } = new List<ObjParamCondition>();

        /// <summary>
        /// Проверяемые параметры.
        /// </summary>
        public List<ObjParamCondition> CheckConditions { get; set; } = new List<ObjParamCondition>();

        /// <summary>
        /// Логика объединения категорий и параметров поиска: "и" / "или".
        /// </summary>
        public string CategoriesAndParams { get; set; } = "и";

        /// <summary>
        /// Конструктор по умолчанию для XML-сериализации.
        /// </summary>
        public ObjCheck()
        {
        }

        /// <summary>
        /// Создаёт проверку с заданным именем.
        /// </summary>
        public ObjCheck(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Создаёт копию проверки с новым именем.
        /// </summary>
        public ObjCheck Clone(string newName)
        {
            return new ObjCheck
            {
                Name = newName,
                CategoriesAndParams = CategoriesAndParams,
                Categories = Categories?.Select(CloneCategory).ToList() ?? new List<ObjCategory>(),
                Conditions = Conditions?.Select(CloneCondition).ToList() ?? new List<ObjParamCondition>(),
                CheckConditions = CheckConditions?.Select(CloneCondition).ToList() ?? new List<ObjParamCondition>()
            };
        }

        /// <summary>
        /// Создаёт копию категории.
        /// </summary>
        private ObjCategory CloneCategory(ObjCategory category)
        {
            if (category == null)
                return new ObjCategory();

            return new ObjCategory(category.Name)
            {
                BuiltIn = category.BuiltIn
            };
        }

        /// <summary>
        /// Создаёт копию условия параметра.
        /// </summary>
        private ObjParamCondition CloneCondition(ObjParamCondition condition)
        {
            if (condition == null)
                return new ObjParamCondition();

            return new ObjParamCondition(
                condition.BoolCondition,
                condition.ParamName,
                condition.Condition,
                condition.ParamValue);
        }
    }
}