using System.Windows.Forms;

namespace JDP
{
	public partial class frmReport : Form
    {
        public string Message
        {
            get { return txtReport.Text; }
            set { txtReport.Text = value; }
        }

		public frmReport()
		{
			InitializeComponent();
#if NET10_0_OR_GREATER
            UiHelp.Attach(this);
#endif
		}
	}
}