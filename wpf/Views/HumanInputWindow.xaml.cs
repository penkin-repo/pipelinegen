using System.Windows;

namespace PipelineGenWpf.Views
{
    public partial class HumanInputWindow : Window
    {
        public string EditedText { get; private set; }

        public HumanInputWindow(int stepNumber, string stepTitle, string initialValue, string instruction)
        {
            InitializeComponent();
            TxtStepNum.Text = stepNumber.ToString();
            TxtStepTitle.Text = stepTitle ?? "";
            TxtValue.Text = initialValue ?? "";
            EditedText = initialValue ?? "";

            if (!string.IsNullOrWhiteSpace(instruction))
            {
                InstructionBanner.Visibility = Visibility.Visible;
                TxtInstruction.Text = "✦ " + instruction;
            }
        }

        private void BtnContinue_Click(object sender, RoutedEventArgs e)
        {
            EditedText = TxtValue.Text ?? "";
            DialogResult = true;
            Close();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
