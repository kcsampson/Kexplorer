using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace Kexplorer.res
{
	/// <summary>
	/// Summary description for DbConnectionDialog.
	/// </summary>
	public class DbConnectionDialog : System.Windows.Forms.Form
	{
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
		private System.Windows.Forms.Button bDelete;
		private System.Windows.Forms.TextBox tbName;
		private System.Windows.Forms.ComboBox cbProvider;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.TextBox tbUserName;
		private System.Windows.Forms.TextBox tbPwd;
		private System.Windows.Forms.Label lbConnString;
		private System.Windows.Forms.ComboBox cbUserArea;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.Button bShowConnString;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.TextBox tbDatabase;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.TextBox tbDataSource;
		private System.Windows.Forms.Label label8;

		private IDbConnection iDbConnection;
		private System.Windows.Forms.Label lbConnEncrypted;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public DbConnectionDialog( IDbConnection newIDBConnection)
		{
			this.iDbConnection = newIDBConnection;
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			//
			// TODO: Add any constructor code after InitializeComponent call
			//

			this.InitFromConnection();
			if ( this.iDbConnection.IsNew )
			{
				this.bDelete.Enabled = false;
				
			}
		}

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		protected override void Dispose( bool disposing )
		{
			if( disposing )
			{
				if(components != null)
				{
					components.Dispose();
				}
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code
		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.bDelete = new System.Windows.Forms.Button();
			this.tbName = new System.Windows.Forms.TextBox();
			this.cbProvider = new System.Windows.Forms.ComboBox();
			this.label1 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.tbUserName = new System.Windows.Forms.TextBox();
			this.tbPwd = new System.Windows.Forms.TextBox();
			this.lbConnString = new System.Windows.Forms.Label();
			this.cbUserArea = new System.Windows.Forms.ComboBox();
			this.label5 = new System.Windows.Forms.Label();
			this.bShowConnString = new System.Windows.Forms.Button();
			this.label6 = new System.Windows.Forms.Label();
			this.tbDatabase = new System.Windows.Forms.TextBox();
			this.label7 = new System.Windows.Forms.Label();
			this.tbDataSource = new System.Windows.Forms.TextBox();
			this.label8 = new System.Windows.Forms.Label();
			this.lbConnEncrypted = new System.Windows.Forms.Label();
			this.SuspendLayout();
			// 
			// button1
			// 
			this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.button1.Location = new System.Drawing.Point(104, 368);
			this.button1.Name = "button1";
			this.button1.TabIndex = 0;
			this.button1.Text = "Accept";
			// 
			// button2
			// 
			this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.button2.Location = new System.Drawing.Point(280, 368);
			this.button2.Name = "button2";
			this.button2.TabIndex = 1;
			this.button2.Text = "Cancel";
			// 
			// bDelete
			// 
			this.bDelete.DialogResult = System.Windows.Forms.DialogResult.No;
			this.bDelete.Location = new System.Drawing.Point(336, 160);
			this.bDelete.Name = "bDelete";
			this.bDelete.TabIndex = 2;
			this.bDelete.Text = "Delete";
			// 
			// tbName
			// 
			this.tbName.Location = new System.Drawing.Point(136, 112);
			this.tbName.Name = "tbName";
			this.tbName.Size = new System.Drawing.Size(128, 20);
			this.tbName.TabIndex = 3;
			this.tbName.Text = "";
			// 
			// cbProvider
			// 
			this.cbProvider.Items.AddRange(new object[] {
															"MySql",
															"Oracle"});
			this.cbProvider.Location = new System.Drawing.Point(136, 144);
			this.cbProvider.Name = "cbProvider";
			this.cbProvider.Size = new System.Drawing.Size(128, 21);
			this.cbProvider.TabIndex = 4;
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(8, 112);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(72, 23);
			this.label1.TabIndex = 5;
			this.label1.Text = "Name:";
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(8, 144);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(64, 23);
			this.label2.TabIndex = 6;
			this.label2.Text = "Provider:";
			// 
			// label3
			// 
			this.label3.Location = new System.Drawing.Point(8, 184);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(72, 23);
			this.label3.TabIndex = 7;
			this.label3.Text = "UserName:";
			// 
			// label4
			// 
			this.label4.Location = new System.Drawing.Point(8, 232);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(56, 23);
			this.label4.TabIndex = 8;
			this.label4.Text = "Pwd:";
			// 
			// tbUserName
			// 
			this.tbUserName.Location = new System.Drawing.Point(136, 184);
			this.tbUserName.Name = "tbUserName";
			this.tbUserName.Size = new System.Drawing.Size(128, 20);
			this.tbUserName.TabIndex = 9;
			this.tbUserName.Text = "";
			// 
			// tbPwd
			// 
			this.tbPwd.Location = new System.Drawing.Point(136, 224);
			this.tbPwd.Name = "tbPwd";
			this.tbPwd.Size = new System.Drawing.Size(128, 20);
			this.tbPwd.TabIndex = 10;
			this.tbPwd.Text = "";
			// 
			// lbConnString
			// 
			this.lbConnString.Location = new System.Drawing.Point(16, 16);
			this.lbConnString.Name = "lbConnString";
			this.lbConnString.Size = new System.Drawing.Size(424, 16);
			this.lbConnString.TabIndex = 11;
			// 
			// cbUserArea
			// 
			this.cbUserArea.Items.AddRange(new object[] {
															"Production",
															"Service"});
			this.cbUserArea.Location = new System.Drawing.Point(136, 256);
			this.cbUserArea.Name = "cbUserArea";
			this.cbUserArea.Size = new System.Drawing.Size(128, 21);
			this.cbUserArea.TabIndex = 12;
			// 
			// label5
			// 
			this.label5.Location = new System.Drawing.Point(8, 264);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(64, 23);
			this.label5.TabIndex = 13;
			this.label5.Text = "User Area:";
			// 
			// bShowConnString
			// 
			this.bShowConnString.Location = new System.Drawing.Point(304, 112);
			this.bShowConnString.Name = "bShowConnString";
			this.bShowConnString.Size = new System.Drawing.Size(112, 23);
			this.bShowConnString.TabIndex = 14;
			this.bShowConnString.Text = "ShowConnString";
			this.bShowConnString.Click += new System.EventHandler(this.bShowConnString_Click);
			// 
			// label6
			// 
			this.label6.Location = new System.Drawing.Point(8, 296);
			this.label6.Name = "label6";
			this.label6.TabIndex = 15;
			this.label6.Text = "Database (mysql)";
			// 
			// tbDatabase
			// 
			this.tbDatabase.Location = new System.Drawing.Point(136, 296);
			this.tbDatabase.Name = "tbDatabase";
			this.tbDatabase.Size = new System.Drawing.Size(168, 20);
			this.tbDatabase.TabIndex = 16;
			this.tbDatabase.Text = "";
			// 
			// label7
			// 
			this.label7.Location = new System.Drawing.Point(8, 328);
			this.label7.Name = "label7";
			this.label7.TabIndex = 17;
			this.label7.Text = "Data Source:";
			// 
			// tbDataSource
			// 
			this.tbDataSource.Location = new System.Drawing.Point(136, 328);
			this.tbDataSource.Name = "tbDataSource";
			this.tbDataSource.Size = new System.Drawing.Size(176, 20);
			this.tbDataSource.TabIndex = 18;
			this.tbDataSource.Text = "";
			// 
			// label8
			// 
			this.label8.Location = new System.Drawing.Point(320, 328);
			this.label8.Name = "label8";
			this.label8.Size = new System.Drawing.Size(152, 24);
			this.label8.TabIndex = 19;
			this.label8.Text = "(MySQL Host) or Oracle TNSName";
			// 
			// lbConnEncrypted
			// 
			this.lbConnEncrypted.Location = new System.Drawing.Point(8, 40);
			this.lbConnEncrypted.Name = "lbConnEncrypted";
			this.lbConnEncrypted.Size = new System.Drawing.Size(440, 23);
			this.lbConnEncrypted.TabIndex = 20;
			// 
			// DbConnectionDialog
			// 
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.ClientSize = new System.Drawing.Size(480, 406);
			this.Controls.Add(this.lbConnEncrypted);
			this.Controls.Add(this.label8);
			this.Controls.Add(this.tbDataSource);
			this.Controls.Add(this.label7);
			this.Controls.Add(this.tbDatabase);
			this.Controls.Add(this.label6);
			this.Controls.Add(this.bShowConnString);
			this.Controls.Add(this.label5);
			this.Controls.Add(this.cbUserArea);
			this.Controls.Add(this.lbConnString);
			this.Controls.Add(this.tbPwd);
			this.Controls.Add(this.tbUserName);
			this.Controls.Add(this.tbName);
			this.Controls.Add(this.label4);
			this.Controls.Add(this.label3);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Controls.Add(this.cbProvider);
			this.Controls.Add(this.bDelete);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Name = "DbConnectionDialog";
			this.Text = "DbConnectionDialog";
			this.ResumeLayout(false);

		}
		#endregion



		/// <summary>
		/// Does the dialog.  Returns the DialogResult
		/// OK, Cancel, No = Delete button.
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static DialogResult DoQuickDialog( IDbConnection input )
		{

			DbConnectionDialog connDialog = new DbConnectionDialog( input);

			DialogResult dr =  connDialog.ShowDialog( KMultiForm.Instance() );

			if ( dr != DialogResult.Cancel )
			{
				connDialog.CopyUserInputToDbConnection();

			}

			return dr;


		}



		private void InitFromConnection()
		{

			IDbConnection input = this.iDbConnection;
			DbConnectionDialog connDialog = this;
			connDialog.tbName.Text = input.Name;
			connDialog.tbPwd.Text = input.Pwd;
			connDialog.tbUserName.Text = input.UserName;
			connDialog.tbDatabase.Text = input.Database;
			connDialog.tbDataSource.Text = input.DataSource;


			if ( input.Provider != null && input.Provider.Length > 0  )
			{
				
				connDialog.cbProvider.SelectedItem = input.Provider;

			}

			if ( input.UserArea != null && input.UserArea.Length > 0 )
			{
				connDialog.cbUserArea.SelectedItem = input.UserArea;
			}


			if ( input.ConnectionString != null && input.ConnectionString.Length > 0)
			{
				connDialog.lbConnString.Text = input.ConnectionString;
				connDialog.lbConnEncrypted.Text = input.ConnStringEncrypted;
			}			
		}

		private void bShowConnString_Click(object sender, System.EventArgs e)
		{

			this.BeginInvoke(new InThreadRunner(this.UpdateConnStringFromUserEntry));

		}

		private void UpdateConnStringFromUserEntry()
		{
			this.CopyUserInputToDbConnection();

			this.lbConnString.Text = this.iDbConnection.ConnectionString;
			this.lbConnEncrypted.Text = this.iDbConnection.ConnStringEncrypted;
		}


		private void CopyUserInputToDbConnection()
		{
			IDbConnection input = this.iDbConnection;
			DbConnectionDialog connDialog = this;
			//MessageBox.Show("Later", "Under Construction", MessageBoxButtons.OK);
			input.Name = connDialog.tbName.Text;
			input.Pwd = connDialog.tbPwd.Text;
			input.UserName = connDialog.tbUserName.Text;
			input.Database = connDialog.tbDatabase.Text;
			input.DataSource = connDialog.tbDataSource.Text;
				

			if ( connDialog.cbProvider.SelectedItem != null )
			{
				input.Provider = connDialog.cbProvider.SelectedItem.ToString();
			}

			if ( connDialog.cbUserArea.SelectedItem != null )
			{
				input.UserArea = connDialog.cbUserArea.SelectedItem.ToString();
			}		
		}		
		


		

	}


	public delegate void InThreadRunner();

	public interface IDbConnection
	{
		string Name { get; set;}
		string UserName { get; set; }
		string Pwd { get; set; }
		string MappingSchema { get; set; }
		string UserArea { get; set; }
		string Provider { get; set; }
		string ConnectionString { get; }
		string ConnStringEncrypted { get; }
		string Database { get; set;}
		string DataSource { get; set; }
		bool IsNew{ get; }
	}
}
