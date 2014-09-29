﻿using DesktopSharing_Viewer.Code;
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
using System.Windows.Forms;

namespace DesktopSharing
{
    public partial class Form1 : Form
    {
        DesktopSharing_Viewer.Code.Viewer_Loop _Viewer_Loop;

        public Form1()
        {
            InitializeComponent();
            FormClosing += Form1_FormClosing;
            _Viewer_Loop = new DesktopSharing_Viewer.Code.Viewer_Loop();
            _Viewer_Loop.Update_Image = Update_Image;
            _Viewer_Loop.New_Image = New_Image;
            var t = new InputListener(pictureBox1.Handle);
            t.InputMouseEvent += _Viewer_Loop.OnMouseEvent;
            t.InputKeyEvent += _Viewer_Loop.OnKeyEvent;
            Application.AddMessageFilter(t);

        }

        void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            _Viewer_Loop.Stop();

        }
        private void Update_Image(Point p, byte[] m)
        {
            pictureBox1.Invoke((MethodInvoker)delegate
            {
                try
                {
                    using(var memo = new MemoryStream(m))
                    using(var imgregion = Bitmap.FromStream(memo))
                    using(var g = Graphics.FromImage(pictureBox1.Image))
                    {
                        g.DrawImage(imgregion, p);
                        g.Flush();
                    }
                    pictureBox1.Invalidate();
                } catch(Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            });
        }
        private void New_Image(byte[] m)
        {
            pictureBox1.Invoke((MethodInvoker)delegate
            {
                try
                {
                    if(pictureBox1.Image != null)
                        pictureBox1.Image.Dispose();
                    using(var memo = new MemoryStream(m))
                    {
                        pictureBox1.Image = Bitmap.FromStream(memo);
                    }
                } catch(Exception e)
                {
                    Debug.WriteLine(e.Message);
                }
            });
        }
        private void button1_Click(object sender, EventArgs e)
        {
            _Viewer_Loop.Start();


        }
        private void button2_Click(object sender, EventArgs e)
        {
            _Viewer_Loop.Stop();
        }


    }
}
