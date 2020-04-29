using Jacobi.Vst.Interop.Host;
using System.Windows.Forms;
using System.Drawing;

namespace VSTAudioProcessor
{
    public partial class InvisibleForm : Form
    {
        protected readonly VstPluginContext VstPluginContext;

        public InvisibleForm(VstPluginContext vstPluginContext)
        {
            InitializeComponent();
            VstPluginContext = vstPluginContext;
            
            this.Shown += ThisFormShow;
        }
        
        protected void ThisFormShow(object sender, System.EventArgs e)
        {
            if (VstPluginContext.PluginCommandStub.EditorGetRect(out var wndRect))
            {
                this.Size = this.SizeFromClientSize(new Size(wndRect.Width, wndRect.Height));
                VstPluginContext.PluginCommandStub.EditorOpen(this.Handle);
            }
        }

        public new DialogResult ShowDialog(IWin32Window owner)
        {
            if (VstPluginContext.PluginCommandStub.EditorGetRect(out var wndRect))
            {
                this.Size = this.SizeFromClientSize(new Size(wndRect.Width, wndRect.Height));
                VstPluginContext.PluginCommandStub.EditorOpen(this.Handle);
            }

            return base.ShowDialog(owner);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);

            if (e.Cancel == false)
            {
                VstPluginContext.PluginCommandStub.EditorClose();
            }
        }
    }
}