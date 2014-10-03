﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace SecureTcp
{

    public class Secure_Stream : IDisposable
    {
        public TcpClient Client;
        byte[] _MySessionKey;
        private int _Buffer_Size = 1024 * 1024 * 8;//dont want to reallocate large chunks of memory if I dont have to
        byte[] _Buffer;
        public long Received_Total;
        public long Sent_Total;

        public long Received_BPS = 1024 * 100;
        public long Sent_BPS = 1024 * 100;

        private List<long> _Bytes_Received_in_Window;
        private List<long> _Bytes_Sent_in_Window;

        private const int _Window_Size = 5000;//5 seconds
        private DateTime _WindowCounter = DateTime.Now;

        public Secure_Stream(TcpClient c, byte[] sessionkey)
        {
            Client = c;
            Client.NoDelay = true;
            _Buffer = new byte[_Buffer_Size];// 8 megabytes buffer
            _MySessionKey = sessionkey;
            Sent_BPS = Received_BPS=Sent_Total = Received_Total = 0;
            _Bytes_Received_in_Window = new List<long>();
            _Bytes_Sent_in_Window = new List<long>();
        }
        public void Dispose()
        {
            if(Client != null)
                Client.Close();
            Client = null;
        }
        public void Encrypt_And_Send(Tcp_Message m)
        {

            Write(m, Client.GetStream());
            var l = m.length;
            _Bytes_Sent_in_Window.Add(l);
            Sent_Total += l;
            UpdateCounters();
        }
        public Tcp_Message Read_And_Unencrypt()
        {
            var r= Read(Client.GetStream());
            var l = r.length;
            Received_Total += l;
            _Bytes_Received_in_Window.Add(l);
            UpdateCounters();
            return r;
        }

        private void UpdateCounters()
        {
            if((DateTime.Now - _WindowCounter).TotalMilliseconds > _Window_Size)
            {
                Sent_BPS = (int)(((double)_Bytes_Sent_in_Window.Sum()) / ((double)(_Window_Size / 1000)));
                Received_BPS = (int)(((double)_Bytes_Received_in_Window.Sum()) / ((double)(_Window_Size / 1000)));

                Debug.WriteLine("Received: " + SizeSuffix(Received_BPS));
                Debug.WriteLine("Sent: " + SizeSuffix(Sent_BPS));

                _Bytes_Received_in_Window.Clear();
                _Bytes_Sent_in_Window.Clear();
                _WindowCounter = DateTime.Now;
            }
        }
        static readonly string[] SizeSuffixes = { "bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB" };
        public static string SizeSuffix(Int64 value)
        {
            if(value <= 0)
                return "0 bytes";
            int mag = (int)Math.Log(value, 1024);
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            return string.Format("{0:n1} {1}", adjustedSize, SizeSuffixes[mag]);
        }
        protected void Write(Tcp_Message m, NetworkStream stream)
        {
            try
            {
                using(AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                {
                    aes.KeySize = _MySessionKey.Length * 8;
                    aes.Key = _MySessionKey;
                    aes.GenerateIV();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var iv = aes.IV;
                    stream.Write(iv, 0, iv.Length);
                    using(ICryptoTransform encrypt = aes.CreateEncryptor())
                    {
                        var sendbuffer= Tcp_Message.ToBuffer(m);
                        var encryptedbytes = encrypt.TransformFinalBlock(sendbuffer, 0, sendbuffer.Length);
                  
                        stream.Write(BitConverter.GetBytes(encryptedbytes.Length), 0, 4);
                        stream.Write(encryptedbytes, 0, encryptedbytes.Length);
                    }
                }
            } catch(Exception e)
            {
                Debug.WriteLine(e.Message);
            }
        }
        protected Tcp_Message Read(NetworkStream stream)
        {
            try
            {
                if(stream.DataAvailable)
                {
                    var iv = new byte[16];
                    stream.Read(iv, 0, 16);

                    var b = BitConverter.GetBytes(0);
                    stream.Read(b, 0, b.Length);
                    var len = BitConverter.ToInt32(b, 0);
             
                    using(AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
                    {
                        aes.KeySize = _MySessionKey.Length * 8;
                        aes.Key = _MySessionKey;
                        aes.IV = iv;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;

                        ReadExact(stream, _Buffer, 0, len);
                       
                        using(ICryptoTransform decrypt = aes.CreateDecryptor())
                        {
                            var arrybuf = decrypt.TransformFinalBlock(_Buffer, 0, len);
                            return Tcp_Message.FromBuffer(arrybuf);

                        }
                    }
                } else
                    return null;
            } catch(Exception e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }

        }
        private static void ReadExact(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int read;
            while(count > 0 && (read = stream.Read(buffer, offset, count)) > 0)
            {
                offset += read;
                count -= read;
            }
            if(count != 0)
                throw new System.IO.EndOfStreamException();
        }
    }
}
