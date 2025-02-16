﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.Windows.Forms;
using DPUruNet;
using System.Reflection;
using System.ComponentModel;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;


namespace UareUSampleCSharp
{
    public partial class Form_Main : Form
    {
        /// <summary>
        /// Holds fmds enrolled by the enrollment GUI.
        /// </summary>
        public Dictionary<int, Fmd> Fmds
        {
            get { return fmds; }
            set { fmds = value; }
        }
        private Dictionary<int, Fmd> fmds = new Dictionary<int, Fmd>();
        
        /// <summary>
        /// Reset the UI causing the user to reselect a reader.
        /// </summary>
        public bool Reset
        {
            get { return reset; }
            set { reset = value; }
        }
        private bool reset;

        public Form_Main()
        {
            InitializeComponent();
        }

        // When set by child forms, shows s/n and enables buttons.
        public Reader CurrentReader
        {
            get { return currentReader; }
            set
            {
                currentReader = value;
                SendMessage(Action.UpdateReaderState, value);
            }
        }
        private Reader currentReader;

        #region Click Event Handlers

        private Registration _registration;
        private void registration_Click(object sender, EventArgs e)
        {
            if (_registration == null)
            {
                _registration = new Registration();
                _registration._sender = this;
            }

            _registration.ShowDialog();

            _registration.Dispose();
            _registration = null;
        }

        private ShowPersons _showPersons;
        private void btnShowPersons_Click(object sender, EventArgs e)
        {
            if (_showPersons == null)
            {
                _showPersons = new ShowPersons();
                //_showPersons._sender = this;
            }

            _showPersons.ShowDialog();

            _showPersons.Dispose();
            _showPersons = null;
        }

        private SearchPerson _searchPerson;
        private void btnSearchRecord_Click(object sender, EventArgs e)
        {
            if (_searchPerson == null)
            {
                _searchPerson = new SearchPerson();
                _searchPerson._sender = this;
            }

            _searchPerson.ShowDialog();

            _searchPerson.Dispose();
            _searchPerson = null;
        }

        private ReaderSelection _readerSelection;
        private void btnReaderSelect_Click(System.Object sender, System.EventArgs e)
        {
            if (_readerSelection == null)
            {
                _readerSelection = new ReaderSelection();
                _readerSelection.Sender = this;
            }

            _readerSelection.ShowDialog();

            _readerSelection.Dispose();
            _readerSelection = null;
        }

        private Identification _identification;
        private void btnIdentify_Click(System.Object sender, System.EventArgs e)
        {
            if (_identification == null)
            {
                _identification = new Identification();
                _identification._sender = this;
            }

            _identification.ShowDialog();

            _identification.Dispose();
            _identification = null;
        }
        #endregion

        /// <summary>
        /// Open a device and check result for errors.
        /// </summary>
        /// <returns>Returns true if successful; false if unsuccessful</returns>
        public bool OpenReader()
        {
            reset = false;
            Constants.ResultCode result = Constants.ResultCode.DP_DEVICE_FAILURE;
            try
            {
                // Open reader
                if (currentReader != null)
                    result = currentReader.Open(Constants.CapturePriority.DP_PRIORITY_COOPERATIVE);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Opening the Reader: " + ex.ToString());
            }

            if (result != Constants.ResultCode.DP_SUCCESS)
            {
                if (result == Constants.ResultCode.DP_DEVICE_FAILURE)
                    MessageBox.Show("WARNING: Finger Print Device not Selected or Attached!");
                else
                    MessageBox.Show("Warning: " + result);
                reset = true;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Hookup capture handler and start capture.
        /// </summary>
        /// <param name="OnCaptured">Delegate to hookup as handler of the On_Captured event</param>
        /// <returns>Returns true if successful; false if unsuccessful</returns>
        public bool StartCaptureAsync(Reader.CaptureCallback OnCaptured)
        {
            try
            {
                // Activate capture handler
                if (currentReader != null)
                    currentReader.On_Captured += new Reader.CaptureCallback(OnCaptured);
                else
                    return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error occurred in Activating the Reader: " + ex.ToString());
            }

            // Call capture
            if (!CaptureFingerAsync())
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Cancel the capture and then close the reader.
        /// </summary>
        /// <param name="OnCaptured">Delegate to unhook as handler of the On_Captured event </param>
        public void CancelCaptureAndCloseReader(Reader.CaptureCallback OnCaptured)
        {
            if (currentReader != null)
            {
                currentReader.CancelCapture();

                // Dispose of reader handle and unhook reader events.
                currentReader.Dispose();

                if (reset)
                {
                    CurrentReader = null;
                }
            }
        }

        /// <summary>
        /// Check the device status before starting capture.
        /// </summary>
        /// <returns></returns>
        public void GetStatus()
        {
            Constants.ResultCode result = currentReader.GetStatus();

            if ((result != Constants.ResultCode.DP_SUCCESS))
            {
                reset = true;
                throw new Exception("" + result);   
            }

            if ((currentReader.Status.Status == Constants.ReaderStatuses.DP_STATUS_BUSY))
            {
                Thread.Sleep(50);
            }
            else if ((currentReader.Status.Status == Constants.ReaderStatuses.DP_STATUS_NEED_CALIBRATION))
            {
                currentReader.Calibrate();
            }
            else if ((currentReader.Status.Status != Constants.ReaderStatuses.DP_STATUS_READY))
            {
                throw new Exception("Reader Status - " + currentReader.Status.Status);   
            }
        }
        
        /// <summary>
        /// Check quality of the resulting capture.
        /// </summary>
        public bool CheckCaptureResult(CaptureResult captureResult)
        {
            if (captureResult.Data == null)
            {
                if (captureResult.ResultCode != Constants.ResultCode.DP_SUCCESS)
                {
                    reset = true;
                    throw new Exception(captureResult.ResultCode.ToString());
                }

                // Send message if quality shows fake finger
                if ((captureResult.Quality != Constants.CaptureQuality.DP_QUALITY_CANCELED))
                {
                    throw new Exception("Quality - " + captureResult.Quality);
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Function to capture a finger. Always get status first and calibrate or wait if necessary.  Always check status and capture errors.
        /// </summary>
        /// <param name="fid"></param>
        /// <returns></returns>
        public bool CaptureFingerAsync()
        {
            try
            {
                GetStatus();

                Constants.ResultCode captureResult = currentReader.CaptureAsync(Constants.Formats.Fid.ANSI, Constants.CaptureProcessing.DP_IMG_PROC_DEFAULT, currentReader.Capabilities.Resolutions[0]);
                if (captureResult != Constants.ResultCode.DP_SUCCESS)
                {
                    reset = true;
                    throw new Exception("" + captureResult);            
                }

                return true;
            }
            catch(Exception ex)
            {
                if (ex.ToString().Contains("Object reference not set to an instance of an object"))
                    MessageBox.Show("Device not Selected or Attached!");
                else
                    MessageBox.Show("Error:  " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Create a bitmap from raw data in row/column format.
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public Bitmap CreateBitmap(byte[] bytes, int width, int height)
        {
            byte[] rgbBytes = new byte[bytes.Length * 3];

            for (int i = 0; i <= bytes.Length - 1; i++)
            {
                rgbBytes[(i * 3)] = bytes[i];
                rgbBytes[(i * 3) + 1] = bytes[i];
                rgbBytes[(i * 3) + 2] = bytes[i];
            }
            Bitmap bmp = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);

            for (int i = 0; i <= bmp.Height - 1; i++)
            {
                IntPtr p = new IntPtr(data.Scan0.ToInt64() + data.Stride * i);
                System.Runtime.InteropServices.Marshal.Copy(rgbBytes, i * bmp.Width * 3, p, bmp.Width * 3);
            }

            bmp.UnlockBits(data);

            return bmp;
        }

        #region SendMessage
        private enum Action
        {
           UpdateReaderState 
        }
        private delegate void SendMessageCallback(Action state, object payload);
        private void SendMessage(Action state, object payload)
        {
            if (this.txtReaderSelected.InvokeRequired)
            {
                SendMessageCallback d = new SendMessageCallback(SendMessage);
                this.Invoke(d, new object[] { state, payload });
            }
            else
            {
                switch (state)
                {
                    case Action.UpdateReaderState:
                        if ((Reader)payload != null)
                        {
                            txtReaderSelected.Text = ((Reader)payload).Description.SerialNumber;
                            //btnRegistration.Enabled = true;
                            btnIdentify.Enabled = true;
                        }
                        else
                        {
                            txtReaderSelected.Text = String.Empty;
                            //btnRegistration.Enabled = false;
                            btnIdentify.Enabled = false;
                        }
                        break;
                    default:
                        break;
                }
            }
        }
        #endregion

        private void Label1_Click(object sender, EventArgs e)
        {

        }
    }
}