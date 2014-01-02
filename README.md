Kexplorer - 
C# Windows Form Application
=========

KExplorer is a Multi-Tabbed Windows Explorer alternative I started back in 2004. 

As a programmer, I constantly work hundreds of files spread out all over my hard-drive and network drives. 
I need to be able to work with these files as efficiently as possible.  Windows Explorer has two big drawbacks
that effect my productivity.  First, it can be slow.  Sometimes, there is a 30 to 60 second freeze-up when I right mouse click a file to get its context menu.
Second, Windows Explorer does not have tabs.  You must open a completely different windows to have differents looks into your file system.

So I started KExplorer with some basic problems I wanted to solve to improve my productivity:

  1.  Multi-Tabbed User Interface to the File System.  TreeView of Folders on Left - List of File Information on the Right.
  2.  Multi-Threaded - Improve Responsiveness by performing File System Loading in background worker threads.
  3.  Custom Context Menus - Improve responsiveness of Context Menus by eliminating Icons, and, custom programming.
  4.  Add a "Services" tab - Often I want to Stop/Start services.  Wouldn't it be nice if I could have a listing of services in another tab and be able to start/stop then.
  5.  A "Scripting" framework - I want to integrate my own scripts/tools that help me get my work done fast.
		a. Zip/Unzip - Zipping is extremenly east.  
			i. You can go to a folder and say "Next Unzip Location Should be Here"
			ii. Right click on and zip file and say "Unzip To"
		b. Quickly get Full Path File Name, or just the File Name.
			i. Right mouse click - "Name - Full Name to Clipboard"
			ii or Right mouse click - "Name - Short Name to Clipboard.
		c. Xml/Xslt Transforms
			i.  Right click an Xml file and say "This is the source to the next Xslt I run".
			ii. Right click an Xslt file and say "Run this XSLT with the last Xml I said to run with"
		d. Xml Validation - Right click an Xml file and quickly validate it.
		e. Set other file extensions as Zip files.  .docx, .xslx, .ppptx
		b. WinGrep here.... Run WinGrep command right at the current location.
		
		
		
	6. Passive - Don't check for file system changes.  
		i. This can cause performance problems in Windows Explorer
		ii. Force the user to hit F5 - Refresh to see file changes.
		
	7. Don't allow Drag and Drop
		i. Bad things can happend with drag and drop.  Like you can accidentally move thousands of files this way.
		ii. I worked on a large project where field engineers often drag/dropped files out of existence.
		
	

