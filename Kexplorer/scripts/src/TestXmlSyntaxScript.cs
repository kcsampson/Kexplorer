using System;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Xsl;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Summary description for BeyondCompareSetLeftSideScript.
	/// </summary>
	public class TestXmlSyntaxScript : BaseFileScript
	{
		public TestXmlSyntaxScript()
		{
			this.Active = true;
			this.Description = "Test the selected Xml, or Xslt if it can be opened as such.";
			this.LongName = "XML - Test Xml or Xslt";

			this.ValidExtensions = new string[] {".xml",".bds",".xslt",".config" };

			
		}

		public override void Run(KExplorerNode folder, FileInfo[] files)
		{

			bool error = false;
			foreach ( FileInfo fInfo in files )
			{
				
				if ( fInfo.Extension.ToLower().Equals( "xslt" ) )
				{
					System.Xml.Xsl.XslTransform transform = new XslTransform();

					try 
					{
						transform.Load( fInfo.FullName );
					} 
					catch ( Exception e )
					{
						MessageBox.Show( "Error: " + e.Message, "Xslt Error File: " + fInfo.Name );
						error = true;
					}

				} 
				else 
				{
					try 
					{
						XmlDocument doc = new XmlDocument();
						doc.Load( fInfo.FullName );
					} 
					catch (Exception e )
					{
						MessageBox.Show( "Error: " + e.Message, "Xml Error File: " + fInfo.Name, MessageBoxButtons.OK, MessageBoxIcon.Error );
						error = true;
					}
				}																 
			}

		
			if ( !error )
			{
				MessageBox.Show( "All good!", "XML/XSL Checker", MessageBoxButtons.OK);
			}
		}


	}
}
