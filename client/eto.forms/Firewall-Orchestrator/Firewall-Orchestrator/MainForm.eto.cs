using System;
using Eto.Forms;
using Eto.Drawing;
using System.Threading;
using System.CodeDom.Compiler;
using System.Linq;
using System.Collections;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography.X509Certificates;
using System.Runtime.InteropServices;

namespace Firewall_Orchestrator
{
    partial class MainForm : Form
    {
        private const int FormWidth = 400;
        private const int FormHeight = 350;

        private const int LayoutWidth = 1;
        private const int LayoutHeight = 3;

        private TableLayout Layout;
        private TableCell[,] LayoutCell = new TableCell[LayoutWidth, LayoutHeight];
        
        private TreeGridView FirewallTreeView;
        private GridView TableGridView;
        private Button TestButton;
        private Button ReportButton;
        private TabControl TestTabControl;

        private int CurLayoutCellX;
        private int CurLayoutCellY;

        void InitializeComponent()
        {
            #region Form

            Title = "My Eto Form";
            ClientSize = new Size(FormWidth, FormHeight);
            Padding = 10;

            #endregion

            #region Controls

            // Report Button
            ReportButton = new Button();
            ReportButton.Text = "Generate Report";
            ReportButton.Height = 30;

            // Test Button
            TestButton = new Button();
            TestButton.Text = "Test";
            TestButton.Height = 30;

            // Table GridView
            TableGridView = new GridView();

            // TEST

            // Tab Control
            TestTabControl = new TabControl();

            // Tree View
            FirewallTreeView = new TreeGridView();

            TreeGridItem Parent = new TreeGridItem();
            TreeGridItem Child1 = new TreeGridItem();
            TreeGridItem Child2 = new TreeGridItem();

            Parent.Children.Add(Child1);
            Parent.Children.Add(Child2);

            TreeGridItemCollection treeGridItems = new TreeGridItemCollection();
            treeGridItems.Add(Parent);
            treeGridItems.Add(Child1);
            treeGridItems.Add(Child2);

            FirewallTreeView.DataStore = treeGridItems;

            #endregion

            #region Init Layout

            Layout = new TableLayout();
            Layout.Spacing = new Size(5, 5); // space between each cell
            Layout.Padding = new Padding(10, 5, 10, 10); // space around the table's sides

            #endregion

            #region Layout Cells

            for (int x = 0; x < LayoutWidth; x++)
            {
                for (int y = 0; y < LayoutHeight; y++)
                {
                    LayoutCell[x, y] = new TableCell();
                    LayoutCell[x, y].ScaleWidth = true;
                }
            }

            // Layout Cell 0, 0
            InitCell(TableGridView);

            // Layout Cell 0, 1
            TableLayout ControlButtonTable = new TableLayout();
            ControlButtonTable.Spacing = new Size(5, 5);
            TableRow ControlButtonRow0 = new TableRow();
            ControlButtonRow0.Cells.Add(new TableCell(ReportButton, true));
            ControlButtonRow0.Cells.Add(new TableCell(TestButton, true));
            ControlButtonTable.Rows.Add(ControlButtonRow0);
            InitCell(ControlButtonTable);

            // Layout Cell 0, 2
            InitCell(TestTabControl);
            
            #endregion

            #region Layout Rows

            // Create Layout Rows
            TableRow[] LayoutRow = new TableRow[LayoutHeight];

            for (int y = 0; y < LayoutHeight; y++)
            {
                LayoutRow[y] = new TableRow();

                for (int x = 0; x < LayoutWidth; x++)
                {
                    // Add corresponding Cells to Row
                    LayoutRow[y].Cells.Add(LayoutCell[x, y]);
                }

                Layout.Rows.Add(LayoutRow[y]);
            }

            // Decide which Layout Rows should scale in height
            LayoutRow[0].ScaleHeight = true;

            #endregion

            // Assign Layout to Form
            Content = Layout;

            var quitCommand = new Command { MenuText = "Quit", Shortcut = Application.Instance.CommonModifier | Keys.Q };
            quitCommand.Executed += (sender, e) => Application.Instance.Quit();

            var aboutCommand = new Command { MenuText = "About..." };
            aboutCommand.Executed += (sender, e) => new AboutDialog().ShowDialog(this);

            // create menu
            Menu = new MenuBar
            {
                Items =
                {
					// File submenu
					new ButtonMenuItem { Text = "&Edit", Items = { /* commands/items */ } },
                    new ButtonMenuItem { Text = "&Test", Items = { } }
					// new ButtonMenuItem { Text = "&View", Items = { /* commands/items */ } },
				},
                ApplicationItems =
                {
					// application (OS X) or file menu (others)
					new ButtonMenuItem { Text = "&Preferences..." },
                },
                QuitItem = quitCommand,
                AboutItem = aboutCommand
            };
        }

        private void InitCell(Control control)
        {          
            LayoutCell[CurLayoutCellX, CurLayoutCellY].Control = control;

            if (CurLayoutCellX + 1 >= LayoutWidth)
            {
                CurLayoutCellX = 0;
                CurLayoutCellY++;
            }

            else
            {
                CurLayoutCellX++;
            }
        }
    }
}
