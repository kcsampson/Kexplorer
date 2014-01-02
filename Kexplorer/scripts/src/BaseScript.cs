using System;
using System.Windows.Forms;
using Kexplorer.scripting;

namespace Kexplorer.scripts
{
	/// <summary>
	/// Implements just enough to make other scripts more compact
	/// </summary>
	public abstract class BaseScript : IScript
	{

		
		public BaseScript()
		{

		}

		private bool active = true;

		public bool Active
		{
			get { return this.active; }
			set { this.active = value; }
		}

		private string longName = "No Name";

		public string LongName
		{
			get { return this.longName; }
			set { this.longName = value; }
		}

		private string description = "No description.";

		public string Description
		{
			get { return this.description; }
			set { this.description = value; }
		}

		private IScriptHelper scriptHelper = null;

		public void Initialize(IScriptHelper x)
		{
			this.scriptHelper = x;
		}

		public IScriptHelper ScriptHelper
		{
			get { return this.scriptHelper; }
			set { this.scriptHelper = value; }
		}

		private Shortcut scriptShortCut = Shortcut.None;
		public Shortcut ScriptShortCut
		{
			get { return this.scriptShortCut; }
			set { this.scriptShortCut = value; }
		}

	}
}
