using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TeamViewerController
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        public static TcpClient client;
        byte[] buffer = new byte[1];
        Timer t = new Timer();

        private void Form2_Load(object sender, EventArgs e)
        {

            t.Enabled = true;
            t.Interval = 1000;
            t.Tick += (object s,  EventArgs eargs) =>
            {

            };
        }

        public void ClientDisconnected()
        {
            try
            {
                client.Close();
                client.GetStream().Close();
                t.Stop();
            }
            catch (Exception ex)
            {

            }
        }



        static string IPstring;
        static int connectToPort;
        Image bitmapImage;
        private void button1_Click(object sender, EventArgs e)
        {
            SendCommand(textBox1.Text);
        }

        void SendCommand(string message)
        {
            if (message.Substring(0, 4) == "cmd:" && message.Length >= 5)
            {
                Debug.WriteLine("{cmd}" + message.Substring(4) + "{/cmd}");
                writeMessage("{cmd}" + message.Substring(4) + "{/cmd}");
                return;
            }

            writeMessage(message);
            if (message == "{Screenshot}")
            {

                Debug.WriteLine("Begin Read");
                var BitmapData = getData(client, 1024 * 128);
                Debug.WriteLine("End Read");

                MemoryStream ms = new MemoryStream();
                ms.Write(BitmapData, 0, BitmapData.Length);
                bitmapImage = new Bitmap(ms, false);
                ms.Dispose();

                pictureBox1.Image = bitmapImage;
            }
            else if (message == "{KillTV}")
            {

            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            if (textBox3.Text.Contains(":"))
            {
                string[] ConnectionStrings = textBox3.Text.Split(':');
                IPstring = ConnectionStrings[0];
                connectToPort = int.Parse(ConnectionStrings[1]);

                client = new TcpClient();

                client = tryConnect().Result;

                if (client.Connected)
                {
                    t.Start();
                    label4.Text = "Connected";
                }

                Debug.WriteLine($"IP: {IPstring} | Port: {connectToPort}");
            }
            else
            {
                MessageBox.Show("Improper Formatting!", "Connection Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }


        public static int tryConnectTime = 1000;
        public static async Task<TcpClient> tryConnect()
        {
            try
            {
                if (client == null)
                {
                    client = new TcpClient();
                }

                var connectionTask = client.ConnectAsync(IPAddress.Parse(IPstring), connectToPort).ContinueWith(task =>
                {
                    return task.IsFaulted ? null : client;
                }, TaskContinuationOptions.ExecuteSynchronously);
                var timeoutTask = Task.Delay(tryConnectTime).ContinueWith<TcpClient>(task => null, TaskContinuationOptions.ExecuteSynchronously);
                var resultTask = Task.WhenAny(connectionTask, timeoutTask).Unwrap();
                resultTask.Wait();
                var resultTcpClient = await resultTask;

                return resultTcpClient;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                Console.ReadLine();
                
                return null;
            }
        }

        public string getDataAsString(TcpClient client)
        {
            byte[] bytes = getData(client);
            if (bytes != null)
            {
                return Encoding.ASCII.GetString(bytes);
            }
            else
            {
                return null;
            }
        }

        public byte[] getData(TcpClient client)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] fileSizeBytes = new byte[4];
                int bytes = stream.Read(fileSizeBytes, 0, fileSizeBytes.Length);
                Debug.WriteLine("BYTES TO GET: " + bytes);
                int dataLength = BitConverter.ToInt32(fileSizeBytes, 0);

                int bytesLeft = dataLength;
                byte[] data = new byte[dataLength];

                int buffersize = 1024;
                int bytesRead = 0;

                while (bytesLeft > 0)
                {
                    int curDataSize = Math.Min(buffersize, bytesLeft);
                    if (client.Available < curDataSize)
                    {
                        curDataSize = client.Available;
                    }

                    bytes = stream.Read(data, bytesRead, curDataSize);
                    bytesRead += curDataSize;
                    bytesLeft -= curDataSize;
                    Debug.WriteLine("DATA REMAINING: " + curDataSize);
                }

                return data;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public byte[] getData(TcpClient client, int customBufferSize)
        {
            try
            {
                NetworkStream stream = client.GetStream();
                byte[] fileSizeBytes = new byte[4];
                int bytes = stream.Read(fileSizeBytes, 0, fileSizeBytes.Length);
                int dataLength = BitConverter.ToInt32(fileSizeBytes, 0);

                int bytesLeft = dataLength;
                byte[] data = new byte[dataLength];

                int bytesRead = 0;

                while (bytesLeft > 0)
                {
                    int curDataSize = Math.Min(customBufferSize, bytesLeft);
                    if (client.Available < curDataSize)
                    {
                        curDataSize = client.Available;
                    }

                    bytes = stream.Read(data, bytesRead, curDataSize);
                    bytesRead += curDataSize;
                    bytesLeft -= curDataSize;
                    Debug.WriteLine("DATA REMAINING: " + curDataSize);
                }

                return data;
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        public void writeMessage(string input)
        {
            try
            {
                if (client == null)
                {
                    throw new ObjectDisposedException(client.ToString());
                }
                NetworkStream ns = client.GetStream();
                byte[] message = Encoding.ASCII.GetBytes(input);
                ns.Write(message, 0, message.Length);
            }
            catch (Exception ex)
            {

            }
        }

        public void sendData(byte[] data)
        {
            try
            {
                if (client == null)
                {
                    throw new ObjectDisposedException(client.ToString());
                }
                NetworkStream ns = client.GetStream();
                ns.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {

            }
        }

        private void Form2_FormClosing(object sender, FormClosingEventArgs e)
        {
            Application.Exit();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            SendCommand("{OpenTV}");
            Debug.WriteLine("Turning on TeamViewer");
        }

        private void button4_Click(object sender, EventArgs e)
        {
            SendCommand("{KillTV}");
            Debug.WriteLine("Turning off TeamViewer");
        }

        private void button5_Click(object sender, EventArgs e)
        {
            SendCommand("{ScreenON}");
            Debug.WriteLine("Turning on Screen");
        }

        private void button6_Click(object sender, EventArgs e)
        {
            SendCommand("{ScreenOFF}");
            Debug.WriteLine("Turning off Screen");
        }

        private void button7_Click(object sender, EventArgs e)
        {
            SendCommand("{UnlockKeyboard}");
            Debug.WriteLine("Unlocking Keyboard");
        }

        private void button8_Click(object sender, EventArgs e)
        {
            SendCommand("{LockKeyboard}");
            Debug.WriteLine("Locking Keyboard");
        }

        private void button9_Click(object sender, EventArgs e)
        {
            SendCommand("{Screenshot}");
            Debug.WriteLine("Taking Screenshot");
        }
    }
}
