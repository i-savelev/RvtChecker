using Autodesk.Revit.DB;

namespace RvtChecker
{
    /// <summary>
    /// Модель категории элемента для настройки рабочих наборов.
    /// </summary>
    public class ObjCategory
    {
        /// <summary>
        /// Отображаемое имя категории.
        /// </summary>
        public string Name { get; set; } = null;

        /// <summary>
        /// Встроенная категория Revit. 
        /// Инициализируется INVALID, чтобы избежать значения 0, недопустимого для XML-сериализации.
        /// </summary>
        public BuiltInCategory BuiltIn { get; set; } = BuiltInCategory.INVALID;

        public ObjCategory(string name)
        {
            Name = name;
        }

        public ObjCategory()
        {
        }

        /// <summary>
        /// Возвращает массив строк для отображения в DataGridView.
        /// </summary>
        public string[] GetStrings()
        {
            string[] strings = new string[1];
            strings[0] = Name;
            return strings;
        }
    }
}