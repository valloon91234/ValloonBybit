using IniParser;
using IO.Swagger.Model;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using Valloon.Trading;

namespace Notify
{
    public partial class Form1 : Form
    {
        public static readonly string INI_FILENAME = "_config.ini";
        FileIniDataParser IniDataParser = new FileIniDataParser();

        public Form1()
        {
            InitializeComponent();
            Color backColor = Color.FromArgb(29, 38, 49);
            this.BackColor = backColor;
            numericUpDown1.BackColor = backColor;
            numericUpDown2.BackColor = backColor;
            textBox_Symbol.BackColor = backColor;
            textBox_Price.BackColor = backColor;
            textBox_Message.BackColor = backColor;
            trackBar1.BackColor = backColor;

            trackBar1_Scroll(null, null);
        }

        private void notifyIcon1_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            this.Show();
            this.Activate();
        }

        private WMPLib.WindowsMediaPlayer Player = new WMPLib.WindowsMediaPlayer();

        private void PlayFile(String url)
        {
            Player = new WMPLib.WindowsMediaPlayer();
            Player.PlayStateChange += Player_PlayStateChange;
            Player.URL = url;
            Player.controls.play();
        }

        private void Player_PlayStateChange(int NewState)
        {
            if ((WMPLib.WMPPlayState)NewState == WMPLib.WMPPlayState.wmppsStopped)
            {
                //Actions on stop
            }
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            Player.controls.stop();
        }

        public void Exit()
        {
            notifyIcon1.Visible = false;
            Process.GetCurrentProcess().Kill();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Exit();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            this.TopMost = checkBox1.Checked;
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
            this.Opacity = trackBar1.Value / 100d;
        }

        List<SymbolTickInfo> tickerList;
        string ErrorMessage;
        DateTime? LastConnectedTime;
        DateTime? LastNotifyTime;

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!File.Exists(INI_FILENAME))
            {
                File.WriteAllText(INI_FILENAME, "[CONFIG]");
            }
            var iniData = IniDataParser.ReadFile(INI_FILENAME);
            textBox_Symbol.Text = iniData["CONFIG"]["SYMBOL"];
            try
            {
                numericUpDown1.DecimalPlaces = int.Parse(iniData["CONFIG"]["DECIMALS"]);
                numericUpDown1.Value = decimal.Parse(iniData["CONFIG"]["MIN"]);
            }
            catch { }
            try
            {
                numericUpDown2.DecimalPlaces = int.Parse(iniData["CONFIG"]["DECIMALS"]);
                numericUpDown2.Value = decimal.Parse(iniData["CONFIG"]["MAX"]);
            }
            catch { }

            new Thread(() =>
            {
                var apiHelper = new BybitLinearApiHelper();
                while (true)
                {
                    try
                    {
                        tickerList = apiHelper.GetTickerList();
                        ErrorMessage = null;
                        LastConnectedTime = BybitLinearApiHelper.ServerTime;
                    }
                    catch (Exception ex)
                    {
                        ErrorMessage = ex.Message;
                    }
                    Thread.Sleep(3000);
                }
            }).Start();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            if (tickerList != null)
            {
                var symbol = textBox_Symbol.Text.Trim();
                var ticker = tickerList.Where(t => t.Symbol == symbol).FirstOrDefault();
                var tickerBTC = tickerList.Where(t => t.Symbol == "BTCUSDT").FirstOrDefault();
                if (ticker != null)
                {
                    var lastPrice = ticker.LastPrice.Value;
                    this.Text = $"{lastPrice}  /  {ticker.MarkPrice.Value.ToString().Substring(ticker.MarkPrice.Value.ToString().Length - 2)}  /  {tickerBTC.LastPrice:F0}";
                    textBox_Price.Text = $"{lastPrice}";
                    if (LastConnectedTime != null && (BybitLinearApiHelper.ServerTime - LastConnectedTime.Value).TotalMilliseconds < 5000)
                    {
                        var now = DateTime.Now;
                        var iniData = IniDataParser.ReadFile(INI_FILENAME);
                        bool playSound = int.Parse(iniData["CONFIG"]["SOUND"]) > 0;
                        if (lastPrice <= numericUpDown1.Value)
                        {
                            if (LastNotifyTime == null)
                            {
                                notifyIcon1.ShowBalloonTip(0, $"{lastPrice}\r\n", $"{lastPrice} <= {numericUpDown1.Value}", ToolTipIcon.Info);
                                FlashWindow.Flash(this);
                                if (playSound)
                                    PlayFile("down.mp3");
                                LastNotifyTime = now;
                            }
                            textBox_Price.ForeColor = Color.FromArgb(255, 65, 88);
                        }
                        else if (lastPrice >= numericUpDown2.Value && numericUpDown2.Value > numericUpDown1.Value)
                        {
                            if (LastNotifyTime == null)
                            {
                                notifyIcon1.ShowBalloonTip(0, $"{lastPrice}\r\n", $"{lastPrice} >= {numericUpDown2.Value}", ToolTipIcon.Info);
                                FlashWindow.Flash(this);
                                if (playSound)
                                    PlayFile("up.mp3");
                                LastNotifyTime = now;
                            }
                            textBox_Price.ForeColor = Color.FromArgb(0, 218, 133);
                        }
                        else
                        {
                            textBox_Price.ForeColor = Color.FromArgb(227, 247, 237);
                            LastNotifyTime = null;
                        }
                    }
                    if (ticker != null)
                        textBox_Message.Text = $"{ticker.MarkPrice.Value}  /  {tickerBTC.LastPrice}    [{LastConnectedTime:yyyy-MM-dd HH:mm:ss}]";
                }
                else
                {
                    ErrorMessage = "Invalid Symbol";
                    textBox_Message.Text = ErrorMessage;
                }
            }
        }

        private void textBox_Symbol_TextChanged(object sender, EventArgs e)
        {
            var iniData = IniDataParser.ReadFile(INI_FILENAME);
            iniData["CONFIG"]["SYMBOL"] = textBox_Symbol.Text;
            IniDataParser.WriteFile(INI_FILENAME, iniData);
        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            var iniData = IniDataParser.ReadFile(INI_FILENAME);
            iniData["CONFIG"]["MIN"] = numericUpDown1.Value.ToString();
            IniDataParser.WriteFile(INI_FILENAME, iniData);
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            var iniData = IniDataParser.ReadFile(INI_FILENAME);
            iniData["CONFIG"]["MAX"] = numericUpDown2.Value.ToString();
            IniDataParser.WriteFile(INI_FILENAME, iniData);
        }
    }
}
