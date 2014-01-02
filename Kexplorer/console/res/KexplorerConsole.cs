using System;
using System.Collections;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Kexplorer.console
{
	/// <summary>
	/// Summary description for Console.
	/// </summary>
	public class KExplorerConsole : System.Windows.Forms.UserControl
	{
		private Form mainForm = null;

		private StreamReader outputReader = null;
		private StreamReader errorReader = null;
		private StreamWriter inputWriter = null;

		private Process cmd = new Process();

		private Thread readerThread = null;
		private Thread errorReaderThread = null;
		private bool killed = false;

		private string tempText = null;

		private char lastChar = ' ';
		private bool isError = false;

		private string xlock = " ";

		private string ib = "";

		private DataTable table = null;

		private DataView view = null;

		private InvokeDelegate keyWriterInvoker = null;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.DataGrid dataGrid1;



		/// <summary> 
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public KExplorerConsole( Form newMainForm )
		{
			// This call is required by the Windows.Forms Form Designer.
			InitializeComponent();

			// TODO: Add any initialization after the InitializeComponent call
			this.mainForm = newMainForm;

		

		}

		/// <summary> 
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			this.killed = true;
			if ( this.cmd != null )
			{

				this.readerThread.Abort();
				this.errorReaderThread.Abort();
				this.cmd.Kill();
			}
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Component Designer generated code
		/// <summary> 
		/// Required method for Designer support - do not modify 
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.dataGrid1 = new System.Windows.Forms.DataGrid();
			((System.ComponentModel.ISupportInitialize)(this.dataGrid1)).BeginInit();
			this.SuspendLayout();
			// 
			// textBox1
			// 
			this.textBox1.Dock = System.Windows.Forms.DockStyle.Bottom;
			this.textBox1.Location = new System.Drawing.Point(0, 476);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(992, 20);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "";
			this.textBox1.KeyUp += new System.Windows.Forms.KeyEventHandler(this.textBox1_KeyUp);
			// 
			// dataGrid1
			// 
			this.dataGrid1.DataMember = "";
			this.dataGrid1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.dataGrid1.HeaderForeColor = System.Drawing.SystemColors.ControlText;
			this.dataGrid1.Location = new System.Drawing.Point(0, 0);
			this.dataGrid1.Name = "dataGrid1";
			this.dataGrid1.Size = new System.Drawing.Size(992, 476);
			this.dataGrid1.TabIndex = 2;
			// 
			// KExplorerConsole
			// 
			this.Controls.Add(this.dataGrid1);
			this.Controls.Add(this.textBox1);
			this.Name = "KExplorerConsole";
			this.Size = new System.Drawing.Size(992, 496);
			((System.ComponentModel.ISupportInitialize)(this.dataGrid1)).EndInit();
			this.ResumeLayout(false);

		}
		#endregion

		public void Initialize()
		{
			this.Initialize( "C:\\" );
		}

		//-----------------------------------------------------------------------------//
		/// <summary>
		/// Kicks off a cmd.exe in a process.  Starts a thread to monitor
		/// stdout and stderr
		/// </summary>
		public void Initialize( string workingDir )
		{
			
			this.InitializeTable();

			this.killed = false;

			ProcessStartInfo psI = new ProcessStartInfo("cmd");
			psI.FileName = "cmd.exe";		
			psI.WorkingDirectory = workingDir;
			psI.UseShellExecute = false;
			psI.RedirectStandardInput = true;
			psI.RedirectStandardOutput = true;
			psI.RedirectStandardError = true;
			psI.CreateNoWindow = true;
			cmd.StartInfo = psI;
			cmd.Start();
			this.inputWriter = cmd.StandardInput;
			this.outputReader = cmd.StandardOutput;
			this.errorReader = cmd.StandardError;
			this.inputWriter.AutoFlush = true;


			this.readerThread = new Thread( new ThreadStart( this.ReaderThreadRunner));
			this.errorReaderThread = new Thread( new ThreadStart( this.ErrorReaderThreadRunner));


			this.readerThread.Start();
			this.errorReaderThread.Start();

			cmd.Start();

			Thread.Sleep(10);

			this.inputWriter.WriteLine("");
	

			
		}

		/// <summary>
		/// Let's an outside agency for a command onto the command line.
		/// </summary>
		/// <param name="command"></param>
		public void TypeCommand( string command )
		{

			this.inputWriter.WriteLine( command );
			this.inputWriter.WriteLine("");
		    				
		}


		public void InitializeTable()
		{
			this.table = new DataTable("Konsole");
			DataColumn c = null;
			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "cout";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "otype";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);

			c = new DataColumn();
			c.DataType = System.Type.GetType("System.String");
			c.ColumnName = "dstamp";
			c.ReadOnly = true;
			c.Unique = false;
			table.Columns.Add(c);


			

			
			// Insert code to create and populate columns.
			this.view = new DataView(table);	

			this.mainForm.Invoke(new InvokeDelegate(this.SetDataToDataGrid));

			DataGridTableStyle ts = new DataGridTableStyle();
			ts.MappingName = "Konsole";




			
			DataGridTextBoxColumn cout = new KonsoleColumnStyle( this.table );
			cout.MappingName = "cout";
			cout.HeaderText = "cout";
			ts.GridColumnStyles.Add( cout );


			DataGridTextBoxColumn dstamp = new KonsoleColumnStyle( this.table );
			dstamp.MappingName = "dstamp";
			dstamp.HeaderText = "dstamp";
			ts.GridColumnStyles.Add( dstamp);

		/*	KonsoleColumnStyle kstyle = new KonsoleColumnStyle( this.table );
			kstyle.MappingName = "otype";
			kstyle.HeaderText = "otype";
			kstyle.Width = 50;
			ts.GridColumnStyles.Add( kstyle ); */

			this.dataGrid1.TableStyles.Clear();
			this.dataGrid1.TableStyles.Add(ts);
			this.dataGrid1.TableStyles["Konsole"].GridColumnStyles["cout"].Width = 750;

			this.dataGrid1.TableStyles["Konsole"].GridColumnStyles["dstamp"].Width = 100;

					
		}


		/// <summary>
		/// Monitors stdout, writes lines to the datagrid.
		/// </summary>
		public void ReaderThreadRunner()
		{

			InvokeDelegate invoker = new InvokeDelegate( this.WriteTextToLabel );
			while ( !this.killed )
			{
				string output = this.outputReader.ReadLine();
				lock ( this.xlock )
				{

					this.tempText = output;
					this.isError = false;

					this.mainForm.BeginInvoke(invoker );


					while ( this.tempText != null )
					{
						Thread.Sleep(1);
					}
				}



			}
		}


		/// <summary>
		/// Monitors stderr, writes lines to the datagrid.
		/// </summary>
		public void ErrorReaderThreadRunner()
		{

			InvokeDelegate invoker = new InvokeDelegate( this.WriteTextToLabel );
			while ( !this.killed )
			{
				string output = this.errorReader.ReadLine();
				if ( output != null )
				{
					Console.WriteLine( output );
				}


				lock( this.xlock )
				{
					this.tempText = output;
					this.isError = true;

					this.mainForm.BeginInvoke(invoker );
				


					while ( this.tempText != null )
					{
						Thread.Sleep(1);
					}
				}



			}
		}

		private void SetDataToDataGrid()
		{
			this.dataGrid1.CaptionText = "Konsole";
			this.dataGrid1.DataSource = this.view;

		}

		public void WriteTextToLabel()
		{
			lock( this.table )
			{
				DataRow row = this.table.NewRow();
				row["cout"] = this.tempText;
				row["otype"] = (this.isError) ? "E" : "I";
				row["dstamp"] = System.DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss.fff");

				
				
				this.tempText = null;

				this.table.Rows.Add( row );

				this.dataGrid1.CurrentRowIndex = this.table.Rows.Count-1;

				if  ( this.isError )
				{
					this.dataGrid1.Select( this.dataGrid1.CurrentRowIndex );

					this.dataGrid1.SelectionBackColor = Color.Red;

				}

			
									}


		
		}



		private void textBox1_KeyUp(object sender, KeyEventArgs e)
		{
			if ( e.KeyCode == Keys.Enter )
			{
				string temp = this.textBox1.Text;
				this.textBox1.Text = "";

				this.inputWriter.WriteLine( temp );
				this.inputWriter.WriteLine("");
		    				
			}
		}
	}



	public class KonsoleColumnStyle : DataGridTextBoxColumn
	{

		private DataTable data = null;

		public KonsoleColumnStyle( DataTable newData ) : base()
		{
			this.data = newData;

		}
	
		protected override void Paint(Graphics g, Rectangle Bounds, CurrencyManager Source,
			int RowNum, Brush BackBrush, Brush ForeBrush, 
			bool AlignToRight) 
		{
			

    

			string otype = null;
			string cValue = null;
			


			DataRow row = this.data.Rows[ RowNum ];
			if ( row != null )
			{
				otype = (string)row["otype"];
			}
			cValue = (string) this.GetColumnValueAtRow(Source, RowNum); 
			
			if(otype != null && otype.Equals("E"))
			{
				BackBrush = Brushes.Coral;
			}
			else
			{
				BackBrush = Brushes.White;
			}


			g.FillRectangle(BackBrush, Bounds.X, Bounds.Y, Bounds.Width, Bounds.Height);

			System.Drawing.Font font = new Font(System.Drawing.FontFamily.GenericSansSerif, 
				(float)8.25 );
			g.DrawString( cValue, font, Brushes.Black, Bounds.X, Bounds.Y);
		}

		protected override void Paint(Graphics g, Rectangle bounds, CurrencyManager source, int rowNum)
		{
			this.Paint( g, bounds, source, rowNum, false );
		}

		protected override void Paint(Graphics g, Rectangle bounds, CurrencyManager source, int rowNum, bool alignToRight)
		{
			this.Paint(g, bounds, source, rowNum, alignToRight);
		}



	}
}
