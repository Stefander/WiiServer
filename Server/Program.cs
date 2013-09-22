using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Timers;
using System.Threading;
using WiimoteLib;

namespace WiiServer
{
    public class WiiServerApp : Form
    {
        private NotifyIcon  trayIcon;
        private ContextMenu trayMenu;
        System.Drawing.Icon icnTask;
        TrollServer server;
        public TrollStatus status = new TrollStatus();
        System.Timers.Timer testTimer;
        public delegate void testDelegate(bool toggle, int index);
        public delegate void extensionDelegate();
        List<Vector3> gestureDump = new List<Vector3>();
        List<ArrayList> wiiTabPage = new List<ArrayList>();
        int activePage;

        public WiiServerApp()
        {
            // Create context menu with 2 options
            trayMenu = new ContextMenu();
            trayMenu.MenuItems.Add("Status", OnStatus);
            trayMenu.MenuItems.Add("Exit", OnExit);

            // Load troll icon
            icnTask = new Icon(System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("WiiServer.Resources.troll.ico"));

            // Create a new taskbar icon
            trayIcon = new NotifyIcon();
            trayIcon.Text = "TrollDom WiiMote Server";
            trayIcon.Icon = icnTask;
            trayIcon.ContextMenu = trayMenu;
            trayIcon.Visible = true;

            testTimer = new System.Timers.Timer(200);
            testTimer.Elapsed += new ElapsedEventHandler(finishTimer);

            status.exitBox.Checked = Properties.Settings.Default.RespondToExitSignal;

            server = new TrollServer(this);

            Visible = false;

            // Create tab pages for every detected Wiimote
            for (int i = 0; i < server.collection.Count; i++)
            {
                TabPage wiiTab = new TabPage("Wiimote " + i);
                wiiTabPage.Add(new ArrayList());
                
                // Test vibration button (0)
                Button testBtn = new Button();
                testBtn.Text = "Test rumble";
                testBtn.Size = new Size(100, 30);
                testBtn.Click += new System.EventHandler(testWii);
                testBtn.Location = new System.Drawing.Point(10, 35);
                wiiTabPage[i].Add(testBtn);

                // Test speaker button (1)
                Button testBtn2 = new Button();
                testBtn2.Text = "Test speaker";
                testBtn2.Size = new Size(100, 30);
                testBtn2.Click += new System.EventHandler(testSpeaker);
                testBtn2.Location = new System.Drawing.Point(10, 70);
                wiiTabPage[i].Add(testBtn2);

                // Extension label (2)
                Label extLabel = new Label();
                extLabel.Size = new Size(130, 30);
                extLabel.Location = new System.Drawing.Point(10, 10);
                extLabel.Text = "Extension: "+server.collection[i].WiimoteState.ExtensionType.ToString();
                server.collection[i].WiimoteExtensionChanged += new EventHandler<WiimoteExtensionChangedEventArgs>(changedExtension);
                wiiTabPage[i].Add(extLabel);

                // Add the tab page to the TabControl and the controls to the page
                status.wiiTabs.TabPages.Add(wiiTab);
                wiiTab.Controls.Add(testBtn);
                wiiTab.Controls.Add(testBtn2);
                wiiTab.Controls.Add(extLabel);
            }

            // When we changed the active index, update the active index
            status.wiiTabs.SelectedIndexChanged += new EventHandler(wiiTabs_SelectedIndexChanged);
            activePage = status.wiiTabs.SelectedIndex;
        }

        void changedExtension(object sender, WiimoteExtensionChangedEventArgs e)
        {
            // Invoke the method if the handle is created (direct cross thread form function calls are not allowed)
            if (((Label)wiiTabPage[activePage][2]).IsHandleCreated)
                ((Label)wiiTabPage[activePage][2]).Invoke(new extensionDelegate(changeValues), null);
            else
                changeValues();
        }

        /// <summary>
        /// This method will update all the values on the status page if it's open
        /// </summary>
        private void changeValues()
        {
            // If the status window is not visible, return
            if (!status.Visible)
                return;

            // Update the extension type
            ((Label)wiiTabPage[activePage][2]).Text = "Extension: " + server.collection[activePage].WiimoteState.ExtensionType.ToString();

            // Update the amount of Wii controllers
            status.wiiControllerAmount.Text = server.collection.Count.ToString();

            // TODO: Update the amount of tabs
        }

        /// <summary>
        /// This method will make the selected Wiimote play a very annoying yet amazing tone
        /// </summary>
        private void testSpeaker(object sender, EventArgs e) 
        { 
            server.collection[activePage].PlayTone(0x10, 0x40, 0xC3); 
            server.PushMessage("Testing speaker " + activePage);
        }

        /// <summary>
        /// This method is called when pressing the test rumble button
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void testWii(object sender, EventArgs e)
        {
            testTimer.Start();
            server.PushMessage("Testing rumble " + activePage);
            ((Button)wiiTabPage[activePage][0]).Invoke(new testDelegate(toggleTest), new Object[] { true, activePage });
        }

        /// <summary>
        /// Main loop
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            // Check if process is already open
            if (!IsProcessOpen()) 
                Application.Run(new WiiServerApp());
            else 
            {
                string procName = Process.GetCurrentProcess().ProcessName;

                // Don't show the annoying warnings for now
                /*if (!procName.Contains("vshost") && Process.GetProcessesByName(procName + ".vshost").Length > 0)
                    MessageBox.Show("Please close Visual Studio before running a standalone version of TrollDom Wiimote Server.", "Wiimote Server");
                else
                    MessageBox.Show("Wiimote TrollDom Server is already running!", "Wiimote Server");
                */
                return; 
            }
        }

        /// <summary>
        /// This method is called when the application loads up
        /// </summary>
        /// <param name="e"></param>
        protected override void OnLoad(EventArgs e)
        {
            // Hide the form
            Visible       = false;
            ShowInTaskbar = false;

            base.OnLoad(e);
        }

        /// <summary>
        /// Checks whether the server is already open, can't have multiple servers at the same time since they use the same port :)
        /// </summary>
        /// <returns></returns>
        public static bool IsProcessOpen()
        {
            string proc = Process.GetCurrentProcess().ProcessName;
            string procName = (proc.Contains("vshost")) ? proc.Split('.')[0] : proc;
            return (Process.GetProcessesByName(procName).Length + Process.GetProcessesByName(procName+".vshost").Length > 1);
        }

        private void OnStatus(object sender, EventArgs e)
        {
            status.Show();

            if(server.collection.Count > 0)
                changeValues();

            // Drag it to the foreground if it wasn't already
            status.TopMost = true;
            status.Focus();
            status.BringToFront();
            status.TopMost = false;
        }

        private void OnExit(object sender, EventArgs e)
        {
            // If we're quitting, disconnect all the controllers and turn off the LEDs
            foreach (Wiimote wiiMote in server.collection)
            {
                wiiMote.SetLEDs(false, false, false, false);
                wiiMote.Disconnect();
            }
            trayIcon.Visible = false;
            Environment.Exit(1);
        }

        private void wiiTabs_SelectedIndexChanged(Object o, EventArgs e)
        {
            activePage = status.wiiTabs.SelectedIndex;
        }

        private void finishTimer(object sender, System.Timers.ElapsedEventArgs e) { testTimer.Stop(); ((Button)wiiTabPage[activePage][0]).Invoke(new testDelegate(toggleTest), new Object[] { false, activePage }); }

        public void QuitServer() { OnExit(null, null); }

        public void toggleTest(bool toggle, int index) { ((Button)wiiTabPage[index][0]).Enabled = !toggle; server.collection[activePage].SetRumble(toggle); }


        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                // Release the icon resource.
                 icnTask.Dispose();
            }

            base.Dispose(isDisposing);
        }
    }
}