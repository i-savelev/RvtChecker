using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using RevitLogger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.Serialization;
using Document = Autodesk.Revit.DB.Document;

namespace RvtChecker
{
    /// <summary>
    /// Команда для проверки параметров элементов модели по настраиваемым наборам правил.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CheckParameters : IExternalCommand
    {
        //---PluginsManager---//
        /// <summary>Имя вкладки в PluginsManager.</summary>
        public static string IS_TAB_NAME => "ISTools";
        /// <summary>Отображаемое имя команды.</summary>
        public static string IS_NAME => "Проверка параметров";
        /// <summary>Путь к embedded resource-изображению.</summary>
        public static string IS_IMAGE => "RvtChecker.Resources.check.png";
        /// <summary>Описание команды для пользователя.</summary>
        public static string IS_DESCRIPTION => "Проверка параметров элементов модели по заданным наборам правил";
        //---PluginsManager---//

        /// <summary>
        /// Путь к временному XML-файлу автосохранения проверок.
        /// </summary>
        private readonly string _tempXmlPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Temp",
            "i-savelev",
            "Checks",
            "ChecksTemp.xml");

        /// <summary>
        /// Основная форма плагина.
        /// Если форма будет переименована, заменить SetWorksetsForm на CheckParametersForm.
        /// </summary>
        private CheckParametersForm _window;

        /// <summary>
        /// Список наборов проверок.
        /// </summary>
        private List<ObjCheck> _checksList;

        /// <summary>
        /// Список имён категорий модели для выпадающих списков.
        /// </summary>
        private List<string> _categoriesList;

        /// <summary>
        /// Текущий документ Revit.
        /// </summary>
        private Document _doc;

        /// <summary>
        /// UIDocument для работы с выделением элементов.
        /// </summary>
        private UIDocument _uidoc;

        /// <summary>
        /// Текстовое описание области поиска элементов для отчёта.
        /// </summary>
        private string _elementsScopeText = "все видимые модельные элементы активного вида";

        /// <summary>
        /// Логические операторы для условий.
        /// </summary>
        private readonly List<string> _boolConditionList = new List<string>
        {
            "и",
            "или"
        };

        /// <summary>
        /// Условия для поиска элементов.
        /// </summary>
        private readonly List<string> _searchConditionList = new List<string>
        {
            "равно",
            "содержит",
            "не содержит",
            "не равно"
        };

        /// <summary>
        /// Условия для проверяемых параметров.
        /// </summary>
        private readonly List<string> _checkConditionList = new List<string>
        {
            "Имеет значение",
            "Равно",
            "Содержит"
        };

        /// <summary>
        /// Результаты последней проверки для отображения на вкладке "Результаты".
        /// </summary>
        private List<CheckReportRow> _lastReportRows = new List<CheckReportRow>();

        /// <summary>
        /// Точка входа команды Revit.
        /// </summary>
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            ConfigureLogging(commandData);
            try
            {
                Logger.Info("[CheckParameters] Старт команды");
                _uidoc = commandData.Application.ActiveUIDocument;
                if (_uidoc == null)
                {
                    Logger.Error("[CheckParameters] Нет активного документа");
                    message = "Нет активного документа Revit.";
                    return Result.Failed;
                }
                _doc = _uidoc.Document;
                Logger.Debug($"[CheckParameters] ActiveDocument={_doc?.Title ?? "null"}");

                InitializeData();
                InitializeForm();
                SetupCategoriesGrid();
                SetupParametersGrid();
                SetupChecksGrid();
                SetupCheckConditionsGrid();
                SetupSettings();
                SetupResultsGrid();
                SetupEventHandlers();
                LoadXmlWhenOpen();
                _window.Show();
                Logger.Info("[CheckParameters] Форма открыта успешно");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка выполнения команды");
                message = ex.Message;
                return Result.Failed;
            }
        }

        /// <summary>
        /// Настраивает файловый лог и снимок окружения.
        /// </summary>
        private void ConfigureLogging(ExternalCommandData commandData)
        {
            var logDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Temp",
                "i-savelev",
                "Checks");
            if (!Directory.Exists(logDir))
                Directory.CreateDirectory(logDir);

            Logger.SetLogPath(Path.Combine(logDir, "checks.log"));
            Logger.SetLogLevel(Logger.LogLevel.Debug);
            Logger.Init(
                hostName: "Autodesk Revit",
                hostVersionNumber: commandData.Application.Application.VersionNumber,
                hostBuild: commandData.Application.Application.VersionBuild,
                hasActiveDocument: commandData.Application.ActiveUIDocument != null);
        }

        /// <summary>
        /// Инициализирует начальные данные команды.
        /// </summary>
        private void InitializeData()
        {
            _checksList = new List<ObjCheck>();
            _categoriesList = new List<string>();
            foreach (Category category in _doc.Settings.Categories)
            {
                if (category.CategoryType == CategoryType.Model && !string.IsNullOrWhiteSpace(category.Name))
                {
                    _categoriesList.Add(category.Name);
                }
            }
            _categoriesList.Sort();
            Logger.Debug($"[CheckParameters] Инициализировано категорий модели: {_categoriesList.Count}");
        }

        /// <summary>
        /// Инициализирует и настраивает основную форму.
        /// </summary>
        private void InitializeForm()
        {
            _window = new CheckParametersForm
            {
                Text = "Проверка параметров"
            };
            _window.groupBox1.Text = "Категории";
            _window.groupBox2.Text = "Параметры поиска";
            _window.groupBox3.Text = "Поиск элементов";
            _window.groupBox4.Text = "Наборы проверок";
            _window.groupBox5.Text = "Проверяемые параметры";
        }

        /// <summary>
        /// Настраивает таблицу категорий.
        /// </summary>
        private void SetupCategoriesGrid()
        {
            var columnCategory = new DataGridViewComboBoxColumn
            {
                HeaderText = "Категория",
                DataSource = _categoriesList
            };
            _window.dataGridView1.Columns.Add(columnCategory);
            _window.button7.Text = "+";
            _window.button8.Text = "-";
        }

        /// <summary>
        /// Настраивает таблицу параметров поиска.
        /// </summary>
        private void SetupParametersGrid()
        {
            var columnBoolCondition = new DataGridViewComboBoxColumn
            {
                DataSource = _boolConditionList,
                HeaderText = "и/или"
            };
            var columnParameterName = new DataGridViewColumn
            {
                HeaderText = "Название параметра",
                CellTemplate = new DataGridViewTextBoxCell()
            };
            var columnCondition = new DataGridViewComboBoxColumn
            {
                DataSource = _searchConditionList,
                HeaderText = "Условие"
            };
            var columnValue = new DataGridViewColumn
            {
                HeaderText = "Значение параметра",
                CellTemplate = new DataGridViewTextBoxCell()
            };

            _window.dataGridView2.Columns.Add(columnBoolCondition);
            _window.dataGridView2.Columns.Add(columnParameterName);
            _window.dataGridView2.Columns.Add(columnCondition);
            _window.dataGridView2.Columns.Add(columnValue);
            _window.dataGridView2.Columns[0].Width = 60;
            _window.dataGridView2.Columns[2].Width = 120;
            _window.button2.Text = "+";
            _window.button4.Text = "-";
        }

        /// <summary>
        /// Настраивает таблицу наборов проверок.
        /// </summary>
        private void SetupChecksGrid()
        {
            var columnCheckName = new DataGridViewColumn
            {
                HeaderText = "Название проверки",
                CellTemplate = new DataGridViewTextBoxCell()
            };
            _window.dataGridView3.Columns.Add(columnCheckName);
            _window.button1.Text = "Добавить";
            _window.button5.Text = "Удалить";
            _window.button10.Text = "Переименовать";
            _window.button12.Text = "Копировать";
        }

        /// <summary>
        /// Настраивает таблицу проверяемых параметров.
        /// </summary>
        private void SetupCheckConditionsGrid()
        {
            var columnBoolCondition = new DataGridViewComboBoxColumn
            {
                DataSource = _boolConditionList,
                HeaderText = "и/или"
            };
            var columnParameterName = new DataGridViewColumn
            {
                HeaderText = "Название параметра",
                CellTemplate = new DataGridViewTextBoxCell()
            };
            var columnCondition = new DataGridViewComboBoxColumn
            {
                DataSource = _checkConditionList,
                HeaderText = "Условие"
            };
            var columnValue = new DataGridViewColumn
            {
                HeaderText = "Значение параметра",
                CellTemplate = new DataGridViewTextBoxCell()
            };

            _window.dataGridView4.Columns.Add(columnBoolCondition);
            _window.dataGridView4.Columns.Add(columnParameterName);
            _window.dataGridView4.Columns.Add(columnCondition);
            _window.dataGridView4.Columns.Add(columnValue);
            _window.dataGridView4.Columns[0].Width = 60;
            _window.dataGridView4.Columns[2].Width = 140;
            _window.button15.Text = "+";
            _window.button13.Text = "-";
        }

        /// <summary>
        /// Настраивает таблицу результатов проверок.
        /// </summary>
        private void SetupResultsGrid()
        {
            _window.dataGridViewResults.Columns.Clear();

            var columnName = new DataGridViewTextBoxColumn
            {
                HeaderText = "Название проверки",
                Name = "CheckName",
                Width = 600
            };

            var columnCount = new DataGridViewTextBoxColumn
            {
                HeaderText = "Количество нарушений",
                Name = "FailedCount",
                Width = 150
            };

            var columnSelect = new DataGridViewButtonColumn
            {
                HeaderText = "Выбрать",
                Name = "SelectButton",
                Text = "Выбрать",
                UseColumnTextForButtonValue = true,
                Width = 100
            };

            _window.dataGridViewResults.Columns.Add(columnName);
            _window.dataGridViewResults.Columns.Add(columnCount);
            _window.dataGridViewResults.Columns.Add(columnSelect);
        }

        /// <summary>
        /// Настраивает блок настроек.
        /// </summary>
        private void SetupSettings()
        {
            _window.comboBox1.DataSource = new List<string>
            {
                "и",
                "или"
            };
            _window.button3.Text = "Сохранить проверку";
            _window.button6.Text = "Сохранить шаблон";
            _window.button9.Text = "Загрузить шаблон";
            _window.button11.Text = "Запустить проверку";
        }

        /// <summary>
        /// Подключает обработчики событий UI.
        /// </summary>
        private void SetupEventHandlers()
        {
            _window.button7.Click += (s, e) => IsUtils.AddRow(_window.dataGridView1);
            _window.button8.Click += (s, e) => IsUtils.DeleteRow(_window.dataGridView1);
            _window.button2.Click += (s, e) => IsUtils.AddRow(_window.dataGridView2);
            _window.button4.Click += (s, e) => IsUtils.DeleteRow(_window.dataGridView2);
            _window.button15.Click += (s, e) => IsUtils.AddRow(_window.dataGridView4);
            _window.button13.Click += (s, e) => IsUtils.DeleteRow(_window.dataGridView4);
            _window.button1.Click += (s, e) => AddCheckDialog();
            _window.button5.Click += (s, e) => DeleteCheck();
            _window.button10.Click += (s, e) => RenameCheckDialog();
            _window.button12.Click += (s, e) => CopyCheck();
            _window.button3.Click += (s, e) => SaveCheck();
            _window.button6.Click += (s, e) => SaveXml();
            _window.button9.Click += (s, e) => LoadXml();
            _window.button11.Click += (s, e) => RunChecks();
            _window.buttonSaveExcel.Click += (s, e) => SaveExcelReport();
            _window.dataGridView3.CellClick += SetDataToOtherDatagrid;
            _window.dataGridViewResults.CellContentClick += DataGridViewResults_CellContentClick;
            _window.dataGridView1.DataError += DataGridView_DataError;
            _window.dataGridView2.DataError += DataGridView_DataError;
            _window.dataGridView4.DataError += DataGridView_DataError;

            try
            {
                _window.FormClosed += (s, e) => SavingWhenClosing();
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при подписке на FormClosed");
            }
        }

        /// <summary>
        /// Подавляет ошибки отображения DataGridView, например, если значение отсутствует в ComboBox-списке.
        /// </summary>
        private void DataGridView_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            Logger.Debug(
                $"[CheckParameters] Ошибка отображения DataGridView | Column={e.ColumnIndex} | Row={e.RowIndex} | " +
                $"Message={e.Exception?.Message ?? "null"}");
        }

        /// <summary>
        /// Обработчик клика по кнопке в таблице результатов.
        /// </summary>
        private void DataGridViewResults_CellContentClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            if (e.ColumnIndex == _window.dataGridViewResults.Columns["SelectButton"].Index)
            {
                var reportRow = _lastReportRows[e.RowIndex];
                if (reportRow.FailedCount > 0 && !string.IsNullOrWhiteSpace(reportRow.FailedIds))
                {
                    SelectFailedElementsInRevit(reportRow.FailedIds);
                }
            }
        }

        /// <summary>
        /// Выделяет элементы с нарушениями в Revit.
        /// </summary>
        private void SelectFailedElementsInRevit(string failedIds)
        {
            try
            {
                var idStrings = failedIds.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var elementIds = new List<ElementId>();

                foreach (var idStr in idStrings)
                {
                    if (int.TryParse(idStr.Trim(), out int id))
                    {
                        elementIds.Add(new ElementId(id));
                    }
                }

                if (elementIds.Count > 0)
                {
                    _uidoc.Selection.SetElementIds(elementIds);
                    Logger.Info($"[CheckParameters] Выделено элементов: {elementIds.Count}");
                }
                else
                {
                    TaskDialog.Show("Предупреждение", "Не найдено элементов для выделения");
                    Logger.Warning("[CheckParameters] Нет элементов для выделения");
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при выделении элементов");
                TaskDialog.Show("Ошибка", $"Не удалось выделить элементы:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Заполняет таблицу результатов проверок.
        /// </summary>
        private void PopulateResultsGrid()
        {
            _window.dataGridViewResults.Rows.Clear();

            foreach (var reportRow in _lastReportRows)
            {
                var rowIndex = _window.dataGridViewResults.Rows.Add(
                    reportRow.CheckName,
                    reportRow.FailedCount,
                    reportRow.FailedCount > 0 ? "Выбрать" : "");

                // Делаем кнопку неактивной, если нет нарушений
                var buttonCell = _window.dataGridViewResults.Rows[rowIndex].Cells["SelectButton"] as DataGridViewButtonCell;
                if (buttonCell != null)
                {
                    if (reportRow.FailedCount == 0)
                    {
                        buttonCell.FlatStyle = FlatStyle.Flat;
                        buttonCell.UseColumnTextForButtonValue = false;
                        buttonCell.Value = "";
                    }
                    else
                    {
                        buttonCell.FlatStyle = FlatStyle.Standard;
                        buttonCell.UseColumnTextForButtonValue = true;
                    }
                }
            }

            Logger.Debug($"[CheckParameters] Заполнена таблица результатов: {_lastReportRows.Count} строк");
        }

        /// <summary>
        /// Сохраняет Excel-отчет вручную.
        /// </summary>
        private void SaveExcelReport()
        {
            if (_lastReportRows == null || _lastReportRows.Count == 0)
            {
                TaskDialog.Show("Предупреждение", "Нет данных для сохранения. Сначала выполните проверку.");
                Logger.Warning("[CheckParameters] Попытка сохранить отчет без данных");
                return;
            }

            ExportReportToExcel(_lastReportRows);
        }

        /// <summary>
        /// Заполняет таблицы категорий, параметров поиска и проверяемых параметров при выборе проверки.
        /// </summary>
        private void SetDataToOtherDatagrid(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0)
                return;

            _window.dataGridView1.Rows.Clear();
            _window.dataGridView2.Rows.Clear();
            _window.dataGridView4.Rows.Clear();

            var checkName = _window.dataGridView3[0, e.RowIndex].Value?.ToString();
            var check = _checksList.FirstOrDefault(x => x.Name == checkName);
            if (check == null)
            {
                Logger.Warning($"[CheckParameters] Проверка '{checkName ?? "null"}' не найдена в списке");
                return;
            }

            if (check.Categories != null)
            {
                foreach (var category in check.Categories)
                {
                    if (category == null || string.IsNullOrWhiteSpace(category.Name))
                        continue;
                    if (_categoriesList.Contains(category.Name))
                    {
                        _window.dataGridView1.Rows.Add(category.Name);
                    }
                    else
                    {
                        Logger.Warning($"[CheckParameters] Категория '{category.Name}' отсутствует в текущем документе, пропуск при загрузке в форму");
                    }
                }
            }

            if (check.Conditions != null)
            {
                foreach (var condition in check.Conditions)
                {
                    AddConditionToGrid(_window.dataGridView2, condition, _searchConditionList);
                }
            }

            if (check.CheckConditions != null)
            {
                foreach (var condition in check.CheckConditions)
                {
                    AddConditionToGrid(_window.dataGridView4, condition, _checkConditionList);
                }
            }

            var boolMode = _boolConditionList.FirstOrDefault(
                x => string.Equals(x, check.CategoriesAndParams, StringComparison.OrdinalIgnoreCase));
            _window.comboBox1.Text = boolMode ?? "и";

            Logger.Debug(
                $"[CheckParameters] Загружена проверка '{check.Name}' | " +
                $"Категорий={check.Categories?.Count ?? 0} | " +
                $"Условий поиска={check.Conditions?.Count ?? 0} | " +
                $"Проверяемых условий={check.CheckConditions?.Count ?? 0}");
        }

        /// <summary>
        /// Добавляет условие в таблицу параметров с проверкой допустимых значений.
        /// </summary>
        private void AddConditionToGrid(
            DataGridView grid,
            ObjParamCondition condition,
            List<string> allowedConditions)
        {
            if (condition == null)
                return;

            var boolValue = _boolConditionList.FirstOrDefault(
                x => string.Equals(x, condition.BoolCondition, StringComparison.OrdinalIgnoreCase)) ?? "и";
            var conditionValue = allowedConditions.FirstOrDefault(
                x => string.Equals(x, condition.Condition, StringComparison.OrdinalIgnoreCase)) ?? "";

            grid.Rows.Add(
                boolValue,
                condition.ParamName,
                conditionValue,
                condition.ParamValue);
        }

        /// <summary>
        /// Открывает диалог добавления новой проверки.
        /// </summary>
        private void AddCheckDialog()
        {
            var inputWindow = new InputDialogForm
            {
                Text = "Добавление проверки"
            };
            inputWindow.textBox1.Text = "Укажите название проверки";
            inputWindow.button1.Text = "Ок";
            inputWindow.button1.Click += (s, e) => AddCheck(inputWindow);
            inputWindow.ShowDialog();
        }

        /// <summary>
        /// Добавляет новую проверку в список и в UI.
        /// </summary>
        private void AddCheck(InputDialogForm inputWindow)
        {
            var name = inputWindow.textBox2.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                TaskDialog.Show("Предупреждение", "Название проверки не может быть пустым");
                Logger.Warning("[CheckParameters] Попытка создать проверку с пустым именем");
                return;
            }

            bool exists = _checksList.Any(x => x.Name == name);
            if (exists)
            {
                TaskDialog.Show("Предупреждение", "Проверка с таким названием уже существует");
                Logger.Warning($"[CheckParameters] Проверка с именем '{name}' уже существует");
                return;
            }

            var check = new ObjCheck(name);
            _checksList.Add(check);
            _window.dataGridView3.Rows.Add(check.Name);
            inputWindow.Close();
            SelectLastRow();
            Logger.Info($"[CheckParameters] Создана проверка: '{name}'");
        }

        /// <summary>
        /// Удаляет выбранную проверку из UI и из списка.
        /// </summary>
        private void DeleteCheck()
        {
            var name = GetSelectedCheckName();
            if (string.IsNullOrWhiteSpace(name))
            {
                Logger.Debug("[CheckParameters] Удаление проверки отменено: проверка не выбрана");
                return;
            }

            var currentRow = _window.dataGridView3.CurrentRow;
            if (currentRow != null)
            {
                _window.dataGridView3.Rows.Remove(currentRow);
            }
            _checksList.RemoveAll(x => x.Name == name);
            Logger.Debug($"[CheckParameters] Удалена проверка: '{name}'");
        }

        /// <summary>
        /// Открывает диалог переименования проверки.
        /// </summary>
        private void RenameCheckDialog()
        {
            var name = GetSelectedCheckName();
            if (string.IsNullOrWhiteSpace(name))
            {
                TaskDialog.Show("Предупреждение", "Выберите проверку для переименования");
                Logger.Debug("[CheckParameters] Переименование отменено: проверка не выбрана");
                return;
            }

            var inputWindow = new InputDialogForm
            {
                Text = "Переименование проверки"
            };
            inputWindow.textBox1.Text = "Укажите название проверки";
            inputWindow.textBox2.Text = name;
            inputWindow.button1.Text = "Ок";
            inputWindow.button1.Click += (s, e) => RenameCheck(inputWindow, name);
            inputWindow.ShowDialog();
        }

        /// <summary>
        /// Выполняет переименование проверки.
        /// </summary>
        private void RenameCheck(InputDialogForm inputWindow, string oldName)
        {
            var newName = inputWindow.textBox2.Text?.Trim();
            if (string.IsNullOrWhiteSpace(newName))
            {
                TaskDialog.Show("Предупреждение", "Название проверки не может быть пустым");
                Logger.Warning("[CheckParameters] Попытка переименовать проверку в пустое имя");
                return;
            }

            bool exists = _checksList.Any(x => x.Name == newName && x.Name != oldName);
            if (exists)
            {
                TaskDialog.Show("Предупреждение", "Проверка с таким названием уже существует");
                Logger.Warning($"[CheckParameters] Проверка с именем '{newName}' уже существует");
                return;
            }

            var check = _checksList.FirstOrDefault(x => x.Name == oldName);
            if (check == null)
            {
                Logger.Warning($"[CheckParameters] Проверка '{oldName}' для переименования не найдена");
                inputWindow.Close();
                return;
            }

            check.Name = newName;
            foreach (DataGridViewRow row in _window.dataGridView3.Rows)
            {
                if (row.Cells[0].Value?.ToString() == oldName)
                {
                    row.Cells[0].Value = newName;
                    break;
                }
            }

            inputWindow.Close();
            Logger.Info($"[CheckParameters] Проверка переименована: '{oldName}' -> '{newName}'");
        }

        /// <summary>
        /// Копирует выбранную проверку.
        /// </summary>
        private void CopyCheck()
        {
            var name = GetSelectedCheckName();
            if (string.IsNullOrWhiteSpace(name))
            {
                Logger.Debug("[CheckParameters] Копирование отменено: проверка не выбрана");
                return;
            }

            var original = _checksList.FirstOrDefault(x => x.Name == name);
            if (original == null)
            {
                Logger.Warning($"[CheckParameters] Проверка '{name}' для копирования не найдена");
                return;
            }

            var baseName = $"{original.Name} (1)";
            var newName = GetUniqueCheckName(baseName);
            var copy = original.Clone(newName);
            _checksList.Add(copy);
            _window.dataGridView3.Rows.Add(copy.Name);
            SelectRowByName(copy.Name);
            Logger.Info($"[CheckParameters] Скопирована проверка: '{name}' -> '{copy.Name}'");
        }

        /// <summary>
        /// Сохраняет текущую выбранную проверку из UI в список.
        /// </summary>
        private void SaveCheck()
        {
            var name = GetSelectedCheckName();
            if (string.IsNullOrWhiteSpace(name))
            {
                TaskDialog.Show("Предупреждение", "Выберите проверку для сохранения");
                Logger.Debug("[CheckParameters] Сохранение проверки отменено: проверка не выбрана");
                return;
            }

            var check = _checksList.FirstOrDefault(x => x.Name == name);
            if (check == null)
            {
                Logger.Warning($"[CheckParameters] Проверка '{name}' не найдена в списке при сохранении");
                return;
            }

            check.Categories = ReadCategories();
            check.Conditions = ReadParamConditions(_window.dataGridView2);
            check.CheckConditions = ReadParamConditions(_window.dataGridView4);

            var boolMode = _boolConditionList.FirstOrDefault(
                x => string.Equals(x, _window.comboBox1.Text, StringComparison.OrdinalIgnoreCase));
            check.CategoriesAndParams = boolMode ?? "и";

            _window.textBox1.Text = $"Проверка '{name}' сохранена";
            Logger.Info(
                $"[CheckParameters] Сохранена проверка '{name}' | " +
                $"Категорий={check.Categories?.Count ?? 0} | " +
                $"Условий поиска={check.Conditions?.Count ?? 0} | " +
                $"Проверяемых условий={check.CheckConditions?.Count ?? 0}");
        }

        /// <summary>
        /// Читает категории из таблицы категорий.
        /// </summary>
        private List<ObjCategory> ReadCategories()
        {
            var categoryList = new List<ObjCategory>();
            var allCategories = _doc.Settings.Categories;

            for (int i = 0; i < _window.dataGridView1.Rows.Count; i++)
            {
                var categoryName = _window.dataGridView1.Rows[i].Cells[0].Value?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    Logger.Debug("[CheckParameters] Пропуск пустой строки категории в DataGridView");
                    continue;
                }

                var objCategory = new ObjCategory(categoryName);
                bool found = false;
                foreach (Category category in allCategories)
                {
                    if (category.Name == categoryName && category.CategoryType == CategoryType.Model)
                    {
                        objCategory.BuiltIn = (BuiltInCategory)(int)category.Id.GetIdValue();
                        found = true;
                        break;
                    }
                }

                if (found)
                {
                    categoryList.Add(objCategory);
                }
                else
                {
                    Logger.Warning($"[CheckParameters] Категория '{categoryName}' не найдена в документе, пропуск");
                }
            }

            return categoryList;
        }

        /// <summary>
        /// Читает условия параметров из таблицы условий.
        /// </summary>
        private List<ObjParamCondition> ReadParamConditions(DataGridView grid)
        {
            var conditionsList = new List<ObjParamCondition>();
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                var boolCondition = grid.Rows[i].Cells[0].Value?.ToString()?.Trim() ?? "";
                var paramName = grid.Rows[i].Cells[1].Value?.ToString()?.Trim() ?? "";
                var condition = grid.Rows[i].Cells[2].Value?.ToString()?.Trim() ?? "";
                var paramValue = grid.Rows[i].Cells[3].Value?.ToString()?.Trim() ?? "";

                if (string.IsNullOrWhiteSpace(boolCondition) &&
                    string.IsNullOrWhiteSpace(paramName) &&
                    string.IsNullOrWhiteSpace(condition) &&
                    string.IsNullOrWhiteSpace(paramValue))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(boolCondition))
                    boolCondition = "и";

                conditionsList.Add(new ObjParamCondition(
                    boolCondition,
                    paramName,
                    condition,
                    paramValue));
            }

            return conditionsList;
        }

        /// <summary>
        /// Возвращает имя выбранной проверки.
        /// </summary>
        private string GetSelectedCheckName()
        {
            var currentRow = _window.dataGridView3.CurrentRow;
            if (currentRow != null && currentRow.Cells[0].Value != null)
            {
                return currentRow.Cells[0].Value.ToString();
            }

            foreach (DataGridViewRow row in _window.dataGridView3.SelectedRows)
            {
                if (row.Cells[0].Value != null)
                {
                    return row.Cells[0].Value.ToString();
                }
            }

            return "";
        }

        /// <summary>
        /// Выбирает последнюю строку в списке проверок.
        /// </summary>
        private void SelectLastRow()
        {
            _window.dataGridView3.ClearSelection();
            if (_window.dataGridView3.Rows.Count > 0)
            {
                var row = _window.dataGridView3.Rows[_window.dataGridView3.Rows.Count - 1];
                row.Selected = true;
                try
                {
                    _window.dataGridView3.CurrentCell = row.Cells[0];
                }
                catch (Exception ex)
                {
                    Logger.Debug($"[CheckParameters] Не удалось установить текущую ячейку: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Выбирает строку проверки по имени.
        /// </summary>
        private void SelectRowByName(string name)
        {
            _window.dataGridView3.ClearSelection();
            foreach (DataGridViewRow row in _window.dataGridView3.Rows)
            {
                if (row.Cells[0].Value?.ToString() == name)
                {
                    row.Selected = true;
                    try
                    {
                        _window.dataGridView3.CurrentCell = row.Cells[0];
                    }
                    catch (Exception ex)
                    {
                        Logger.Debug($"[CheckParameters] Не удалось установить текущую ячейку: {ex.Message}");
                    }
                    return;
                }
            }
        }

        /// <summary>
        /// Возвращает уникальное имя проверки.
        /// </summary>
        private string GetUniqueCheckName(string baseName)
        {
            if (!_checksList.Any(x => x.Name == baseName))
                return baseName;

            int suffix = 1;
            string newName;
            do
            {
                newName = $"{baseName} ({suffix})";
                suffix++;
            }
            while (_checksList.Any(x => x.Name == newName));

            return newName;
        }

        /// <summary>
        /// Сохраняет проверки в XML через диалог выбора файла.
        /// </summary>
        private void SaveXml()
        {
            var checks = _checksList.ToArray();
            var serializer = new XmlSerializer(typeof(ObjCheck[]));

            var saveFileDialog = new SaveFileDialog
            {
                Title = "Сохранить шаблон проверок",
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                DefaultExt = ".xml",
                FileName = "Проверки.xml"
            };

            if (saveFileDialog.ShowDialog(_window) != DialogResult.OK)
                return;

            try
            {
                using (var streamWriter = new StreamWriter(saveFileDialog.FileName))
                {
                    serializer.Serialize(streamWriter, checks);
                }
                Logger.Info($"[CheckParameters] Шаблон сохранён в {saveFileDialog.FileName} (проверок: {checks.Length})");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при сохранении XML");
                TaskDialog.Show("Ошибка", $"Не удалось сохранить файл:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Загружает проверки из XML через диалог выбора файла.
        /// </summary>
        private void LoadXml()
        {
            var openFileDialog = new OpenFileDialog
            {
                Title = "Загрузить шаблон проверок",
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*"
            };

            if (openFileDialog.ShowDialog(_window) != DialogResult.OK)
                return;

            try
            {
                LoadChecksFromFile(openFileDialog.FileName);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при загрузке XML");
                TaskDialog.Show("Ошибка", $"Не удалось загрузить файл:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Загружает проверки из временного XML при открытии формы.
        /// </summary>
        private void LoadXmlWhenOpen()
        {
            try
            {
                if (!File.Exists(_tempXmlPath))
                {
                    Logger.Debug("[CheckParameters] Временный XML не найден, пропуск автозагрузки");
                    return;
                }

                var fileInfo = new FileInfo(_tempXmlPath);
                if (fileInfo.Length == 0)
                {
                    Logger.Warning("[CheckParameters] Временный XML пустой, удаляю файл");
                    File.Delete(_tempXmlPath);
                    return;
                }

                LoadChecksFromFile(_tempXmlPath);
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при загрузке временного XML");
            }
        }

        /// <summary>
        /// Загружает список проверок из XML-файла.
        /// </summary>
        private void LoadChecksFromFile(string filePath)
        {
            _window.dataGridView3.Rows.Clear();
            _checksList.Clear();

            var serializer = new XmlSerializer(typeof(ObjCheck[]));
            using (var streamReader = new StreamReader(filePath))
            {
                var checks = serializer.Deserialize(streamReader) as ObjCheck[];
                if (checks == null)
                {
                    Logger.Warning($"[CheckParameters] Файл '{filePath}' не содержит список проверок");
                    return;
                }

                _checksList = checks.OrderBy(x => x.Name).ToList();
                foreach (var check in _checksList)
                {
                    NormalizeCheck(check);
                    _window.dataGridView3.Rows.Add(check.Name);
                }

                Logger.Info($"[CheckParameters] Загружено проверок из '{filePath}': {_checksList.Count}");
            }
        }

        /// <summary>
        /// Приводит проверку к безопасному состоянию после загрузки из XML.
        /// </summary>
        private void NormalizeCheck(ObjCheck check)
        {
            if (check == null)
                return;

            if (check.Categories == null)
                check.Categories = new List<ObjCategory>();
            if (check.Conditions == null)
                check.Conditions = new List<ObjParamCondition>();
            if (check.CheckConditions == null)
                check.CheckConditions = new List<ObjParamCondition>();
            if (string.IsNullOrWhiteSpace(check.CategoriesAndParams))
                check.CategoriesAndParams = "и";
        }

        /// <summary>
        /// Сохраняет проверки во временный XML при закрытии формы.
        /// </summary>
        private void SavingWhenClosing()
        {
            try
            {
                var checks = _checksList.ToArray();
                var serializer = new XmlSerializer(typeof(ObjCheck[]));

                var dir = Path.GetDirectoryName(_tempXmlPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                using (var streamWriter = new StreamWriter(_tempXmlPath))
                {
                    serializer.Serialize(streamWriter, checks);
                }

                Logger.Debug($"[CheckParameters] Временный шаблон сохранён: {_tempXmlPath} (проверок: {checks.Length})");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при автосохранении временного XML");
            }
        }

        /// <summary>
        /// Запускает проверку элементов по всем наборам проверок.
        /// </summary>
        private void RunChecks()
        {
            try
            {
                if (_checksList == null || _checksList.Count == 0)
                {
                    TaskDialog.Show("Проверка параметров", "Нет ни одного набора проверок.");
                    Logger.Warning("[CheckParameters] Запуск проверки отменён: список проверок пуст");
                    return;
                }

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                _window.progressBar1.Value = 0;
                _window.progressBar1.Maximum = Math.Max(1, _checksList.Count);
                _window.progressBar1.Step = 1;

                // Очищаем предыдущие результаты
                _lastReportRows.Clear();
                _window.dataGridViewResults.Rows.Clear();

                var reportRows = new List<CheckReportRow>();
                var visibleModelElements = GetVisibleModelElements();

                Logger.Info(
                    $"[CheckParameters] Старт проверки | Наборов={_checksList.Count} | " +
                    $"Базовых элементов={visibleModelElements.Count} | Область={_elementsScopeText}");

                foreach (var check in _checksList)
                {
                    _window.progressBar1.PerformStep();

                    var failedIds = new List<string>();
                    var categoryIds = new HashSet<BuiltInCategory>();

                    if (check.Categories != null)
                    {
                        foreach (var category in check.Categories)
                        {
                            if (category != null && category.BuiltIn != BuiltInCategory.INVALID)
                                categoryIds.Add(category.BuiltIn);
                        }
                    }

                    bool hasCategories = categoryIds.Count > 0;
                    bool hasSearchConditions = HasValidConditions(check.Conditions);
                    bool useOrLogic = string.Equals(check.CategoriesAndParams, "или", StringComparison.OrdinalIgnoreCase);

                    List<Element> candidateElements;

                    // Если категории заданы и режим "и", сначала ограничиваем видимые элементы категориями.
                    // Если категории не заданы, проверяем все видимые модельные элементы активного вида.
                    if (hasCategories && !useOrLogic)
                    {
                        candidateElements = visibleModelElements
                            .Where(element =>
                            {
                                var builtIn = GetBuiltInCategory(element);
                                return builtIn.HasValue && categoryIds.Contains(builtIn.Value);
                            })
                            .ToList();
                    }
                    else
                    {
                        candidateElements = visibleModelElements;
                    }

                    Logger.Debug(
                        $"[CheckParameters] Проверка '{check.Name}' | Категории={hasCategories} | " +
                        $"Условия поиска={hasSearchConditions} | Логика={check.CategoriesAndParams} | " +
                        $"Кандидатов={candidateElements.Count}");

                    foreach (var element in candidateElements)
                    {
                        bool categoryMatched = false;
                        if (hasCategories)
                        {
                            var builtIn = GetBuiltInCategory(element);
                            categoryMatched = builtIn.HasValue && categoryIds.Contains(builtIn.Value);
                        }

                        var objRvt = new ObjRvt
                        {
                            elem = element
                        };

                        bool searchPassed;
                        if (!hasSearchConditions)
                        {
                            searchPassed = !hasCategories || categoryMatched;
                        }
                        else if (useOrLogic && hasCategories)
                        {
                            string searchLog;
                            searchPassed = categoryMatched || EvaluateConditions(objRvt, check.Conditions, out searchLog);
                        }
                        else
                        {
                            string searchLog;
                            searchPassed = EvaluateConditions(objRvt, check.Conditions, out searchLog);
                        }

                        if (!searchPassed)
                            continue;

                        string checkLog;
                        bool checkPassed = EvaluateConditions(objRvt, check.CheckConditions, out checkLog);
                        if (!checkPassed)
                        {
                            failedIds.Add(element.Id.GetIdValue().ToString());
                        }
                    }

                    reportRows.Add(new CheckReportRow
                    {
                        CheckName = check.Name,
                        SearchConditionsText = BuildSearchConditionsText(check),
                        CheckConditionsText = BuildCheckConditionsText(check),
                        FailedCount = failedIds.Count,
                        FailedIds = string.Join(", ", failedIds)
                    });

                    Logger.Info(
                        $"[CheckParameters] Проверка '{check.Name}' завершена | " +
                        $"Кандидатов={candidateElements.Count} | Нарушений={failedIds.Count}");
                }

                stopwatch.Stop();
                var elapsedSeconds = Math.Round(stopwatch.Elapsed.TotalSeconds, 3);

                _window.textBox1.Text = $"Время проверки: {elapsedSeconds} сек.";
                _window.progressBar1.Value = _window.progressBar1.Maximum;

                // Сохраняем результаты для отображения на вкладке "Результаты"
                _lastReportRows = reportRows;
                PopulateResultsGrid();

                // Переключаемся на вкладку "Результаты"
                _window.tabControl1.SelectedIndex = 1;

                Logger.Info($"[CheckParameters] Проверка завершена за {elapsedSeconds} сек.");
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при запуске проверки");
                TaskDialog.Show("Ошибка", $"Не удалось выполнить проверку:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Возвращает видимые модельные элементы активного вида.
        /// Элементы видов, разрезов, фасадов, листов и спецификаций не включаются.
        /// </summary>
        private List<Element> GetVisibleModelElements()
        {
            try
            {
                var view = _doc.ActiveView;
                if (view == null)
                {
                    _elementsScopeText = "все модельные элементы проекта (нет активного вида)";
                    Logger.Warning("[CheckParameters] Нет активного вида, использую все модельные элементы проекта");
                    return GetAllModelElements();
                }

                Logger.Debug($"[CheckParameters] Активный вид: '{view.Title}' | Тип: {view.ViewType}");

                if (!IsViewSuitableForVisibleCheck(view))
                {
                    _elementsScopeText =
                        $"все модельные элементы проекта (активный вид '{view.Title}' типа {view.ViewType} не подходит для проверки видимых элементов)";
                    Logger.Warning(
                        $"[CheckParameters] Активный вид '{view.Title}' типа {view.ViewType} не подходит для проверки видимых элементов");
                    return GetAllModelElements();
                }

                var elements = new FilteredElementCollector(_doc, view.Id)
                    .WhereElementIsNotElementType()
                    .Cast<Element>()
                    .Where(IsModelElement)
                    .ToList();

                _elementsScopeText = $"все видимые модельные элементы активного вида '{view.Title}'";
                Logger.Debug($"[CheckParameters] Собрано видимых модельных элементов на виде '{view.Title}': {elements.Count}");
                return elements;
            }
            catch (Exception ex)
            {
                _elementsScopeText = "все модельные элементы проекта (ошибка сбора видимых элементов)";
                Logger.Exception(ex, "[CheckParameters] Ошибка сбора видимых элементов, перехожу ко всем модельным элементам проекта");
                return GetAllModelElements();
            }
        }

        /// <summary>
        /// Возвращает все модельные элементы проекта.
        /// Используется как fallback, если активный вид не подходит.
        /// </summary>
        private List<Element> GetAllModelElements()
        {
            var elements = new FilteredElementCollector(_doc)
                .WhereElementIsNotElementType()
                .Cast<Element>()
                .Where(IsModelElement)
                .ToList();

            Logger.Debug($"[CheckParameters] Собрано всех модельных элементов проекта: {elements.Count}");
            return elements;
        }

        /// <summary>
        /// Проверяет, что элемент является модельным элементом,
        /// а не видом, разрезом, фасадом, листом, спецификацией и т.п.
        /// </summary>
        private bool IsModelElement(Element element)
        {
            try
            {
                if (element == null)
                    return false;
                if (element is Autodesk.Revit.DB.View)
                    return false;
                if (element.Category == null)
                    return false;
                if (element.Category.CategoryType != CategoryType.Model)
                    return false;

                var builtIn = GetBuiltInCategory(element);
                if (builtIn.HasValue)
                {
                    switch (builtIn.Value)
                    {
                        case BuiltInCategory.OST_Views:
                        case BuiltInCategory.OST_Viewports:
                        case BuiltInCategory.OST_Sheets:
                        case BuiltInCategory.OST_Schedules:
                        case BuiltInCategory.OST_Cameras:
                        case BuiltInCategory.OST_Lines:
                        case BuiltInCategory.OST_DetailComponents:
                            return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                Logger.Debug($"[CheckParameters] Элемент пропущен как немодельный или ошибочный: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Проверяет, подходит ли активный вид для сбора видимых элементов.
        /// </summary>
        private bool IsViewSuitableForVisibleCheck(Autodesk.Revit.DB.View view)
        {
            if (view is ViewSheet)
                return false;

            switch (view.ViewType)
            {
                case ViewType.Schedule:
                case ViewType.ProjectBrowser:
                case ViewType.SystemBrowser:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Безопасно возвращает BuiltInCategory элемента.
        /// </summary>
        private BuiltInCategory? GetBuiltInCategory(Element element)
        {
            try
            {
                if (element?.Category?.Id == null)
                    return null;
                return (BuiltInCategory)(int)element.Category.Id.GetIdValue();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Проверяет, есть ли в списке условий хотя бы одно осмысленное условие.
        /// </summary>
        private bool HasValidConditions(List<ObjParamCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return false;

            return conditions.Any(x =>
                !string.IsNullOrWhiteSpace(x.ParamName) &&
                !string.IsNullOrWhiteSpace(x.Condition));
        }

        /// <summary>
        /// Проверяет список условий параметров для элемента.
        /// Поддерживает логику "и" / "или".
        /// </summary>
        private bool EvaluateConditions(
            ObjRvt objRvt,
            List<ObjParamCondition> conditions,
            out string conditionLog)
        {
            var andResults = new List<bool>();
            var orResults = new List<bool>();
            var conditionParts = new List<string>();

            if (conditions == null)
                conditions = new List<ObjParamCondition>();

            bool firstCondition = true;
            foreach (var condition in conditions)
            {
                if (IsConditionRowEmpty(condition))
                    continue;

                bool result = EvaluateSingleCondition(objRvt, condition);

                if (string.Equals(condition.BoolCondition, "или", StringComparison.OrdinalIgnoreCase))
                    orResults.Add(result);
                else
                    andResults.Add(result);

                conditionParts.Add(FormatConditionPart(condition, firstCondition));
                firstCondition = false;
            }

            bool andResult = andResults.Count > 0 ? andResults.All(x => x) : true;
            bool orResult = orResults.Count > 0 ? orResults.Any(x => x) : true;

            conditionLog = conditionParts.Count > 0
                ? string.Join(" ", conditionParts)
                : "";

            return andResult && orResult;
        }

        /// <summary>
        /// Проверяет одно условие параметра для элемента.
        /// </summary>
        private bool EvaluateSingleCondition(ObjRvt objRvt, ObjParamCondition condition)
        {
            if (condition == null || string.IsNullOrWhiteSpace(condition.ParamName))
                return false;

            var value = objRvt.GetParamAsString(condition.ParamName) ?? "";
            var conditionName = condition.Condition?.Trim().ToLowerInvariant() ?? "";

            switch (conditionName)
            {
                case "имеет значение":
                    return ParameterHasValue(value);
                case "равно":
                    return ValuesEquals(value, condition.ParamValue);
                case "содержит":
                    return ValuesContains(value, condition.ParamValue);
                case "не равно":
                    return !ValuesEquals(value, condition.ParamValue);
                case "не содержит":
                    return !ValuesContains(value, condition.ParamValue);
                default:
                    Logger.Warning($"[CheckParameters] Неизвестное условие '{condition.Condition}' для параметра '{condition.ParamName}'");
                    return false;
            }
        }

        /// <summary>
        /// Проверяет, что значение параметра не пустое и не равно "none".
        /// </summary>
        private bool ParameterHasValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            var trimmed = value.Trim();
            if (trimmed.Length == 0)
                return false;
            if (trimmed.Equals("none", StringComparison.OrdinalIgnoreCase))
                return false;

            return true;
        }

        /// <summary>
        /// Сравнение значений без учёта регистра и лишних пробелов по краям.
        /// </summary>
        private bool ValuesEquals(string value, string expected)
        {
            value = value?.Trim() ?? "";
            expected = expected?.Trim() ?? "";
            return string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Проверка вхождения подстроки без учёта регистра.
        /// </summary>
        private bool ValuesContains(string value, string expected)
        {
            value = value ?? "";
            expected = expected?.Trim() ?? "";
            if (expected.Length == 0)
                return false;
            return value.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// Проверяет, пустая ли строка условия в таблице.
        /// </summary>
        private bool IsConditionRowEmpty(ObjParamCondition condition)
        {
            if (condition == null)
                return true;

            return string.IsNullOrWhiteSpace(condition.ParamName) &&
                   string.IsNullOrWhiteSpace(condition.Condition) &&
                   string.IsNullOrWhiteSpace(condition.ParamValue);
        }

        /// <summary>
        /// Формирует текст условий поиска для отчёта.
        /// </summary>
        private string BuildSearchConditionsText(ObjCheck check)
        {
            var lines = new List<string>();

            if (check.Categories == null || check.Categories.Count == 0)
            {
                lines.Add($"Элементы: {_elementsScopeText}");
            }
            else
            {
                var categoryNames = check.Categories
                    .Where(x => x != null && !string.IsNullOrWhiteSpace(x.Name))
                    .Select(x => x.Name)
                    .ToList();

                lines.Add(categoryNames.Count > 0
                    ? $"Категории: {string.Join(", ", categoryNames)}"
                    : "Категории: не заданы");
            }

            bool hasCategories = check.Categories != null && check.Categories.Count > 0;
            bool hasSearchConditions = HasValidConditions(check.Conditions);

            if (hasCategories && hasSearchConditions)
            {
                lines.Add($"Логика категорий и параметров: {check.CategoriesAndParams}");
            }

            if (hasSearchConditions)
            {
                lines.Add($"Параметры поиска: {BuildConditionsText(check.Conditions)}");
            }
            else
            {
                lines.Add("Параметры поиска: не заданы");
            }

            return string.Join(Environment.NewLine, lines);
        }

        /// <summary>
        /// Формирует текст проверяемых условий для отчёта.
        /// </summary>
        private string BuildCheckConditionsText(ObjCheck check)
        {
            if (!HasValidConditions(check.CheckConditions))
                return "Проверяемые условия не заданы";

            return BuildConditionsText(check.CheckConditions);
        }

        /// <summary>
        /// Формирует текст списка условий.
        /// </summary>
        private string BuildConditionsText(List<ObjParamCondition> conditions)
        {
            if (conditions == null || conditions.Count == 0)
                return "нет";

            var parts = new List<string>();
            bool first = true;
            foreach (var condition in conditions)
            {
                if (IsConditionRowEmpty(condition))
                    continue;

                parts.Add(FormatConditionPart(condition, first));
                first = false;
            }

            return parts.Count > 0
                ? string.Join(" ", parts)
                : "нет";
        }

        /// <summary>
        /// Формирует одну часть текста условия.
        /// </summary>
        private string FormatConditionPart(ObjParamCondition condition, bool isFirst)
        {
            var boolText = string.IsNullOrWhiteSpace(condition.BoolCondition)
                ? "и"
                : condition.BoolCondition.ToLowerInvariant();

            var conditionName = condition.Condition?.Trim() ?? "";
            string body;

            if (conditionName.Equals("Имеет значение", StringComparison.OrdinalIgnoreCase))
            {
                body = $"\"{condition.ParamName}\" имеет значение";
            }
            else
            {
                body = $"\"{condition.ParamName}\" {conditionName.ToLowerInvariant()} \"{condition.ParamValue}\"";
            }

            return isFirst
                ? body
                : $"{boolText} {body}";
        }

        /// <summary>
        /// Сохраняет результаты проверок в Excel-файл.
        /// </summary>
        private void ExportReportToExcel(List<CheckReportRow> reportRows)
        {
            try
            {
                using (var package = new ExcelPackage())
                {
                    var worksheet = package.Workbook.Worksheets.Add("Отчет");

                    worksheet.Cells[1, 1].Value = "Набор проверок";
                    worksheet.Cells[1, 2].Value = "Условия поиска";
                    worksheet.Cells[1, 3].Value = "Проверяемые условия";
                    worksheet.Cells[1, 4].Value = "Количество нарушений";
                    worksheet.Cells[1, 5].Value = "Список ID";
                    worksheet.Row(1).Style.Font.Bold = true;

                    int row = 2;
                    foreach (var reportRow in reportRows)
                    {
                        var idChunks = SplitTextForExcel(reportRow.FailedIds, 32000);
                        bool firstChunk = true;
                        foreach (var idChunk in idChunks)
                        {
                            worksheet.Cells[row, 1].Value = firstChunk ? reportRow.CheckName : "";
                            worksheet.Cells[row, 2].Value = firstChunk ? reportRow.SearchConditionsText : "";
                            worksheet.Cells[row, 3].Value = firstChunk ? reportRow.CheckConditionsText : "";
                            worksheet.Cells[row, 4].Value = firstChunk ? (object)reportRow.FailedCount : null;
                            worksheet.Cells[row, 5].Value = idChunk;
                            firstChunk = false;
                            row++;
                        }
                    }

                    worksheet.Cells[worksheet.Dimension.Address].Style.WrapText = true;
                    worksheet.Cells[worksheet.Dimension.Address].Style.VerticalAlignment = ExcelVerticalAlignment.Top;
                    worksheet.Column(1).Width = 30;
                    worksheet.Column(2).Width = 50;
                    worksheet.Column(3).Width = 50;
                    worksheet.Column(4).Width = 15;
                    worksheet.Column(5).Width = 80;

                    for (int i = 2; i < row; i++)
                    {
                        worksheet.Row(i).Height = 40;
                    }

                    var saveFileDialog = new SaveFileDialog
                    {
                        Title = "Сохранить отчёт проверки параметров",
                        Filter = "Excel files (*.xlsx)|*.xlsx",
                        DefaultExt = ".xlsx",
                        FileName = $"Проверка параметров {DateTime.Now:yyyy-MM-dd_HH-mm-ss}.xlsx"
                    };

                    if (saveFileDialog.ShowDialog(_window) == DialogResult.OK)
                    {
                        File.WriteAllBytes(saveFileDialog.FileName, package.GetAsByteArray());
                        Logger.Info($"[CheckParameters] Отчёт сохранён: {saveFileDialog.FileName}");
                        TaskDialog.Show("Отчёт", $"Отчёт сохранён:\n{saveFileDialog.FileName}");
                    }
                    else
                    {
                        Logger.Warning("[CheckParameters] Пользователь отменил сохранение отчёта");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Exception(ex, "[CheckParameters] Ошибка при формировании Excel-отчёта");
                TaskDialog.Show("Ошибка", $"Не удалось сохранить отчёт:\n{ex.Message}");
            }
        }

        /// <summary>
        /// Разбивает длинный текст на части, чтобы не превысить ограничение ячейки Excel.
        /// </summary>
        private List<string> SplitTextForExcel(string text, int maxLength)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text))
            {
                result.Add("");
                return result;
            }

            if (text.Length <= maxLength)
            {
                result.Add(text);
                return result;
            }

            int position = 0;
            while (position < text.Length)
            {
                int take = Math.Min(maxLength, text.Length - position);
                int breakPosition = position + take;

                if (breakPosition < text.Length)
                {
                    int commaIndex = text.LastIndexOf(',', breakPosition - 1, take);
                    if (commaIndex > position)
                    {
                        take = commaIndex - position + 1;
                    }
                }

                result.Add(text.Substring(position, take));
                position += take;
            }

            return result;
        }

        /// <summary>
        /// Строка результата проверки для формирования отчёта.
        /// </summary>
        private class CheckReportRow
        {
            public string CheckName { get; set; }
            public string SearchConditionsText { get; set; }
            public string CheckConditionsText { get; set; }
            public int FailedCount { get; set; }
            public string FailedIds { get; set; }
        }
    }
}