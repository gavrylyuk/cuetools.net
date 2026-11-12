using System.Windows.Forms;

namespace JDP
{
	public partial class frmPassword : Form
	{
		public frmPassword()
		{
			InitializeComponent();
#if NET10_0_OR_GREATER
            UiHelp.Attach(this);
#endif
		}
	}
}