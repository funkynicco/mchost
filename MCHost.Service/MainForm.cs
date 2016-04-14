using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace MCHost
{
    public partial class MainForm : Form
    {
        private readonly Thread _thread;
        private readonly AutoResetEvent _startServerEvent;
        private readonly ManualResetEvent _stopEvent;
        private Process _process;

        public MainForm()
        {
            InitializeComponent();

            _startServerEvent = new AutoResetEvent(false);
            _stopEvent = new ManualResetEvent(false);

            _thread = new Thread(ServerThread);
            _thread.Start();

            textBox2.KeyDown += TextBox2_KeyDown;
        }

        private void TextBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;

                if (textBox2.Text.Length > 0)
                {
                    var cmd = textBox2.Text;
                    textBox2.Text = "";

                    if (_process != null)
                    {
                        // use
                        LogMessage("> " + cmd);
                        _process.StandardInput.WriteLine(cmd);
                    }
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                    components.Dispose();

                _stopEvent.Set();
                while (!_thread.Join(1000))
                {
                    LogMessage("Cannot join server thread.");
                    Application.DoEvents();
                }
                _startServerEvent.Dispose();
                _stopEvent.Dispose();
            }

            base.Dispose(disposing);
        }

        private void LogMessage(string str)
        {
            var func = new Action<string>((s) =>
            {
                if (textBox1.Text.Length > 0)
                    textBox1.AppendText("\r\n");

                textBox1.AppendText(s);

                textBox1.Select(textBox1.Text.Length, 0);
                textBox1.ScrollToCaret();
            });

            if (InvokeRequired)
                Invoke(func, str);
            else
                func(str);
        }

        private void button1_Click(object sender, EventArgs e)
        {
            _startServerEvent.Set();
            btnStart.Enabled = false;
        }

        private void ServerThread()
        {
            var events = new WaitHandle[] { _startServerEvent, _stopEvent };

            while (true)
            {
                var index = WaitHandle.WaitAny(events);

                if (index == 1)
                    break;

                if (InvokeRequired)
                    Invoke(new Action(() => btnStart.Enabled = false));
                else
                    btnStart.Enabled = false;

                try
                {
                    var psi = new ProcessStartInfo("java", "-Xmx1024M -Xms1024M -jar minecraft_server.1.9.2.jar nogui");
                    psi.UseShellExecute = false;
                    psi.RedirectStandardInput = true;
                    psi.RedirectStandardOutput = true;
                    psi.CreateNoWindow = true;
                    psi.WorkingDirectory = @"C:\Users\Nicco\Desktop\minecraft_test\server";

                    Process process;

                    try
                    {
                        process = Process.Start(psi);

                        if (process == null)
                            throw new Exception("Process.Start returned null");
                    }
                    catch (Exception ex)
                    {
                        LogMessage(ex.Message);
                        LogMessage(ex.StackTrace);
                        continue;
                    }

                    LogMessage("[SYSTEM] java.exe started, process id: " + process.Id);

                    using (process)
                    {
                        process.OutputDataReceived += (sender, e) =>
                            {
                                if (e.Data != null)
                                    LogMessage(e.Data);
                            };

                        process.BeginOutputReadLine();

                        _process = process;

                        while (!process.HasExited)
                        {

                            Thread.Sleep(100);
                        }

                        _process = null;

                        process.CancelOutputRead();
                    }

                    LogMessage("[SYSTEM] Process has exited.");
                }
                finally
                {
                    if (InvokeRequired)
                        Invoke(new Action(() => btnStart.Enabled = true));
                    else
                        btnStart.Enabled = true;
                }
            }
        }
    }
}
