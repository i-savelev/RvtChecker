using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using System.Xml.Serialization;


namespace RvtChecker
{
    public class IsUtils
    {
        /// <summary>
        /// Add one row to System.Windows.Forms.DataGridView
        /// </summary>
        /// <param name="dataGridView"></param>
        public static void DeleteRow(DataGridView dataGridView)
        {
            foreach (DataGridViewRow row in dataGridView.SelectedRows)
            {
                dataGridView.Rows.Remove(row);
            }
        }

        /// <summary>
        /// Add one row to System.Windows.Forms.DataGridView
        /// </summary>
        /// <param name="dataGridView"></param>
        public static void AddRow(DataGridView dataGridView)
        {
            dataGridView.Rows.Add();
        }

        
        
    }
}
