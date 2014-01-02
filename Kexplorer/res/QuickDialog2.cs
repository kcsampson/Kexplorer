using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;

namespace Kexplorer.res
{
	/// <summary>
	/// Summary description for QuickDialog2.
	/// </summary>
	public class QuickDialog2 : System.Windows.Forms.Form
	{
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textBox1;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.TextBox textBox2;
		private System.Windows.Forms.Button button1;
		private System.Windows.Forms.Button button2;
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.Container components = null;

		public QuickDialog2()
		{
			//
			// Required for Windows Form Designer support
			//
			InitializeComponent();

			//
			// TODO: Add any constructor code after InitializeComponent call
			//
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
			this.label1 = new System.Windows.Forms.Label();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.button1 = new System.Windows.Forms.Button();
			this.button2 = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// label1
			// 
			this.label1.Location = new System.Drawing.Point(32, 24);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(224, 23);
			this.label1.TabIndex = 0;
			this.label1.Text = "label1";
			// 
			// textBox1
			// 
			this.textBox1.Location = new System.Drawing.Point(32, 48);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(272, 20);
			this.textBox1.TabIndex = 1;
			this.textBox1.Text = "textBox1";
			// 
			// label2
			// 
			this.label2.Location = new System.Drawing.Point(32, 104);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(216, 23);
			this.label2.TabIndex = 2;
			this.label2.Text = "label2";
			// 
			// textBox2
			// 
			this.textBox2.Location = new System.Drawing.Point(32, 128);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(272, 20);
			this.textBox2.TabIndex = 3;
			this.textBox2.Text = "textBox2";
			// 
			// button1
			// 
			this.button1.DialogResult = System.Windows.Forms.DialogResult.OK;
			this.button1.Location = new System.Drawing.Point(64, 176);
			this.button1.Name = "button1";
			this.button1.TabIndex = 4;
			this.button1.Text = "&Ok";
			// 
			// button2
			// 
			this.button2.DialogResult = System.Windows.Forms.DialogResult.Cancel;
			this.button2.Location = new System.Drawing.Point(192, 176);
			this.button2.Name = "button2";
			this.button2.TabIndex = 5;
			this.button2.Text = "&Cancel";
			// 
			// QuickDialog2
			// 
			this.AcceptButton = this.button1;
			this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
			this.CancelButton = this.button2;
			this.ClientSize = new System.Drawing.Size(352, 222);
			this.Controls.Add(this.button2);
			this.Controls.Add(this.button1);
			this.Controls.Add(this.textBox2);
			this.Controls.Add(this.textBox1);
			this.Controls.Add(this.label2);
			this.Controls.Add(this.label1);
			this.Name = "QuickDialog2";
			this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
			this.Text = "QuickDialog2";
			this.ResumeLayout(false);

		}
		#endregion


		/// <summary>
		/// Return a string array of textbox1 and textbox2
		/// or Null if the user cancelled.
		/// </summary>
		/// <param name="title"></param>
		/// <param name="label1"></param>
		/// <param name="initString1"></param>
		/// <param name="label2"></param>
		/// <param name="initString2"></param>
		/// <returns></returns>
		public static string[] DoQuickDialog( string title
				, string label1, string initString1
				, string label2, string initString2 )
		{
			QuickDialog2 qd = new QuickDialog2();

			
			qd.Text = title;

			qd.label1.Text = label1;

			qd.textBox1.Text= initString1;
			
			qd.label2.Text = label2;

			qd.textBox2.Text= initString2;
			

			
			if ( qd.ShowDialog( KMultiForm.Instance()) == DialogResult.OK )
			{
				return new string[] {qd.textBox1.Text, qd.textBox2.Text };
			}
			return null;
		}


	}
}
