using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace CUEPlayer
{
	static class Program
	{
		/// <summary>
		/// The main entry point for the application.
		/// </summary>
		[STAThread]
		static void Main()
		{
			Application.SetDefaultFont(new System.Drawing.Font("Microsoft Sans Serif", 8.25F));
            Application.EnableVisualStyles();
			Application.SetCompatibleTextRenderingDefault(false);
			Application.Run(new frmCUEPlayer());
		}
	}
}
