using System.Windows.Forms;

namespace JDP
{
	public partial class frmSubmit : Form
	{
		public frmSubmit()
		{
			InitializeComponent();
#if NET10_0_OR_GREATER
            UiHelp.Attach(this);
#endif
		}
	}
}
