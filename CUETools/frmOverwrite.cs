using System.Windows.Forms;

namespace JDP
{
	public partial class frmOverwrite : Form
	{
		public frmOverwrite()
		{
			InitializeComponent();
#if NET10_0_OR_GREATER
            UiHelp.Attach(this);
#endif
		}
	}
}
