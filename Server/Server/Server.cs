using WiimoteLib;
using System;
using System.Net;
using System.Text;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;

namespace WiiServer
{
    public class TrollServer
    {
        private long lastUpdate;            // Time when the capture was last sampled
        private long startCapture;          // Start time of capture (for debug purposes only)
        private int updateFreq = 50;        // Accelerometer sample rate; in Hz
        private int port = 9050;            // Port to open the server on
        private int vecAmount = 900;        // Amount of capture vecs to take into account per Wiimote

        // Capture variables
        Thread captureThread;
        List<bool> capturingList = new List<bool>();
        List<List<Vector3>> vecList = new List<List<Vector3>>();
        List<int> curVec = new List<int>();
        List<List<Vector3>> gestureList = new List<List<Vector3>>();

        private IPEndPoint ipep;
        private UdpClient newsock;
        string response;
        IPEndPoint sender;
        WiimoteState state;
        public WiimoteCollection collection;
        bool hasQuit = false;
        Thread workerThread;
        byte[] data = new byte[1024];
        List<string> _output = new List<string>();
        WiiServerApp main;
        private long sampInterval;
        GestureRecognition gesture;

        public delegate void outputDelegate(string message);

        public TrollServer(WiiServerApp m)
        {
            main = m;
            PushMessage("Welcome to the TrollDom WiiMote server!");

            // Create a collection of Wiimote devices
            collection = new WiimoteCollection();

            collection.FindAllWiimotes();
            PushMessage("Found " + collection.Count + " Wiimote"+((collection.Count > 1) ? "s" : ""));

            // Connect to all the controllers
            foreach (Wiimote wiiMote in collection)
            {
                vecList.Add(new List<Vector3>());
                for (int j = 0; j < vecAmount; j++) { vecList[vecList.Count - 1].Add(new Vector3(0,0,0)); }
                curVec.Add(0);

                capturingList.Add(false);
                gestureList.Add(new List<Vector3>());
                wiiMote.Connect();
                wiiMote.SetReportType(InputReport.ExtensionAccel, true);
                //wiiMote.PlayAudioFile("C:\\snd\\mariekSound.yadpcm");
                //wiiMote.PlayTone(0x10, 0x40, 0xC3, 1);
                wiiMote.SetLEDs(true, false, false, false);
            }

            PushMessage("Waiting for client..");

            // Create the gesture recognition instance
            gesture = new GestureRecognition();

            // Create the server
            ipep = new IPEndPoint(IPAddress.Any, port);
            newsock = new UdpClient(ipep);

            // Set the last capture update ticks to current time
            lastUpdate = DateTime.Now.Ticks;

            // Setup the capture and server threads
            workerThread = new Thread(mainLoopCaller);
            workerThread.Start();

            // Amount of ticks for every capture update (10k ticks in a millisecond)
            sampInterval = 10000000 / updateFreq;

            captureThread = new Thread(captureLoop);
            captureThread.Start();
        }

        /// <summary>
        /// This one will keep calling the capture loop
        /// </summary>
        private void captureLoop()
        {
            // Infinite loop until end of program, running on different thread
            while (true)
            {
                // Get the current ticks
                long ticks = DateTime.Now.Ticks;

                // Check if we need to sample again
                if (ticks >= lastUpdate + sampInterval)
                {
                    // Loop through the collection to see if we're capturing data from any of the WiiMotes
                    for (int i = 0; i < collection.Count; i++)
                    {
                        if (capturingList[i])
                        {
                            // Get the current WiiMote state
                            state = collection[i].WiimoteState;
                            
                            // Get a vector from the list
                            Vector3 target;
                            int cVec = curVec[i];

                            if (cVec < vecAmount)
                            {
                                target = vecList[i][curVec[i]];
                                target.x = state.AccelState.Values.X;
                                target.y = state.AccelState.Values.Y;
                                target.z = state.AccelState.Values.Z;
                            }
                            else
                            {
                                target = new Vector3(state.AccelState.Values.X, state.AccelState.Values.Y, state.AccelState.Values.Z);
                            }
                            // Just get the values from the WiiMote, don't really need the raw ones since they'll require more processing
                            gestureList[i].Add(target);
                            curVec[i]++;
                        }
                    }

                    // Set the last update to the current ticks
                    lastUpdate = ticks;
                }
            }
        }

        private void sendToClient(string msg)
        {
            data = Encoding.ASCII.GetBytes(msg);
            newsock.Send(data, data.Length, sender);
        }

        private int serverLoop()
        {
            // Get the response from the client
            data = newsock.Receive(ref sender);
            string incoming = Encoding.ASCII.GetString(data, 0, data.Length);
            String[] values = incoming.Split(' ');

            if (values[0].Equals("shake"))
            {
                PushMessage("User connected!");
                sendToClient("shake " + collection.Count);
            }
            else if (values[0].Equals("g"))
            {
                int wiiIndex = int.Parse(values[1])-1;
                capturingList[wiiIndex] = (values[2].Equals("1"));

                if (!capturingList[wiiIndex])
                {
                    // Get the amount of seconds we captured and output it to the console
                    float secs = (float)(DateTime.Now.Ticks - startCapture) / 10000000;
                    
                    // Get the list of active gestures from fourth argument and parse them into an integer list we can pass to GestureRecognition
                    String[] gestureValues = values[3].Split('|');
                    List<int> gestures = new List<int>();
                    for (int i = 0; i < gestureValues.Length; i++)
                    { 
                        gestures.Add(int.Parse(gestureValues[i])); 
                    }
                    
                    // Send a message to the client with the matched gesture (if one was found)
                    sendToClient("g "+gesture.MatchGesture(gestureList[wiiIndex],gestures,secs));

                    // Clear the list
                    curVec[wiiIndex] = 0;
                    gestureList[wiiIndex].RemoveRange(0, gestureList[wiiIndex].Count);
                    capturingList[wiiIndex] = false;
                }
                else
                {
                    capturingList[wiiIndex] = true;
                    startCapture = DateTime.Now.Ticks;
                    Console.WriteLine("Started capturing (" + wiiIndex + ")");
                    sendToClient("g");
                }
            }
            else if (values[0].Equals("e"))
            {
                PushMessage("User disconnected.");

                // Send the same back to confirm
                sendToClient(incoming);

                // Do we want to keep the program alive?
                if (!Properties.Settings.Default.RespondToExitSignal)
                    return 1;

                return 0;
            }
            else if (values[0].Equals("u"))
            {
                // Get the Wiimote to update
                state = collection[int.Parse(values[1]) - 1].WiimoteState;

                // Generate the response string
                response = convBool(state.ButtonState.A) + " " + convBool(state.ButtonState.B) + " " + convBool(state.ButtonState.Up)
                    + " " + convBool(state.ButtonState.Down) + " " + convBool(state.ButtonState.Left) + " " + convBool(state.ButtonState.Right)
                    + " " + convBool(state.ButtonState.Plus) + " " + convBool(state.ButtonState.Minus) + " " + convBool(state.ButtonState.One)
                    + " " + convBool(state.ButtonState.Two) + " " + convBool(state.ButtonState.Home) + " " + convBool(state.NunchukState.C)
                    + " " + convBool(state.NunchukState.Z) + " " + state.NunchukState.Joystick.X + " " + state.NunchukState.Joystick.Y
                    + " " + state.AccelState.RawValues.X + " " + state.AccelState.RawValues.Y + " " + state.AccelState.RawValues.Z;

                // And send it to the client
                sendToClient(response);
            }
            else if (values[0].Equals("r"))
            {
                // Set the targeted controller to start or stop vibrating
                collection[int.Parse(values[1]) - 1].SetRumble((values[2].Equals("1")));
                
                // Respond with an r
                sendToClient("r");
            }
            else
            {
                Console.WriteLine(incoming);
                Console.WriteLine("Unknown command? Check syntax");
            }

            return 1;
        }

        /// <summary>
        /// Adds the message to the status page
        /// </summary>
        /// <param name="msg">The message we want</param>
        public void PushMessage(string msg)
        {
            // Check if we have a handle and invoke the method on the control, if not just call the method directly
            if (main.status.listBox.IsHandleCreated)
                main.status.listBox.Invoke(new outputDelegate(scrollListbox), msg);
            else
                scrollListbox(msg);
        }


        /// <summary>
        /// This method will add the message to the status list box
        /// </summary>
        /// <param name="message"></param>
        private void scrollListbox(string message)
        {
            _output.Add(message);
            main.status.listBox.DataSource = null;
            main.status.listBox.DataSource = _output;

            // Set the bar to autoscroll
            main.status.listBox.SelectedIndex = main.status.listBox.Items.Count - 1;
            main.status.listBox.SelectedIndex = -1;
        }

        private int convBool(bool inBool) { return (inBool) ? 1 : 0; }

        /// <summary>
        /// This one will keep calling the server loop until we exit
        /// </summary>
        private void mainLoopCaller()
        {
            while (!hasQuit) { hasQuit = (serverLoop() == 0); }

            main.QuitServer();
        }
    }
}