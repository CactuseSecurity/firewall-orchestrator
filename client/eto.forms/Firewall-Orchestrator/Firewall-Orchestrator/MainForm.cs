using System;
using Eto.Forms;
using Eto.Drawing;
using System.Data.Common;
using System.Threading.Tasks;
using System.Runtime.Serialization.Json;
using System.Collections.ObjectModel;
using System.Text;
using System.Xml;
using System.IO;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Generic;
using System.Data;

namespace Firewall_Orchestrator
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();

            // Because of a bug in Eto this needs to be here
            ReportButton.Click += ReportButton_Click; 
        }

        private async void ReportButton_Click(object sender, EventArgs e)
        {
            DbConnection ServerConnection = new DbConnection();
            string QueryResultJson = await ServerConnection.TestQuery();

            //TestTabControl.Pages.Add(new TabPage { Text = "Query Server Answer", Content = new Scrollable { Content = QueryResult } });

            DataSet QueryResultDataSet = JsonReader.ReadString(QueryResultJson);
            DataTableCollection QueryResultTables = QueryResultDataSet.Tables;

            DataTable QueryResultTable0 = QueryResultTables[0];
            DataColumnCollection QueryResultTable0Columns = QueryResultTable0.Columns;

            for (int i = 0; i < QueryResultTable0Columns.Count; i++)
            {
                int j = i;

                TableGridView.Columns.Add(new GridColumn
                {
                    DataCell = new TextBoxCell { Binding = Binding.Property<DataRow, string>(Row => Row[j].ToString()) },
                    HeaderText = QueryResultTable0Columns[i].ColumnName
                });
            }

            TableGridView.DataStore = QueryResultTable0.Select();
        }
    }
}
